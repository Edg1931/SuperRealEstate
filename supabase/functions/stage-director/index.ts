// Supabase Edge Function: stage-director
//
// The AI Staging Director. Given a room's floor outline (meters), a style
// brief, and the stageable items (vendor catalog + the client's own captured
// furniture, all with real dimensions), Claude proposes a furniture layout:
// which items, where (x/z meters), and facing which way. The client validates
// every placement against the real geometry (Staging.StagingDirector +
// FitChecker) before rendering — the AI proposes, geometry disposes.
// Anthropic key stays server-side:
//   supabase secrets set ANTHROPIC_API_KEY=sk-ant-...
//   supabase functions deploy stage-director

import Anthropic from "npm:@anthropic-ai/sdk@0.69.0";
import {
  capString,
  corsHeaders as cors,
  GuardError,
  readJson,
  rateLimit,
  requireAuth,
  toResponse,
} from "../_shared/guard.ts";

const MODEL = "claude-opus-4-8";

const MAX_BODY_BYTES = 256 * 1024;
const MAX_STYLE_CHARS = 500;
const MAX_OUTLINE_POINTS = 64;
const MAX_ITEMS = 60;

interface Point { x?: number; z?: number }
interface Item {
  id?: string; name?: string; category?: string;
  widthM?: number; depthM?: number; heightM?: number;
  priceUsd?: number; userFurniture?: boolean;
}
interface RequestBody { style?: string; outline?: Point[]; items?: Item[] }

const SCHEMA = {
  type: "object",
  additionalProperties: false,
  properties: {
    summary: { type: "string" }, // one or two spoken-friendly sentences about the layout
    placements: {
      type: "array",
      items: {
        type: "object",
        additionalProperties: false,
        properties: {
          itemId: { type: "string" },     // must be an offered item id
          x: { type: "number" },          // footprint center, meters
          z: { type: "number" },
          yawDegrees: { type: "number" }, // rotation about vertical; 0 faces +z
          note: { type: "string" },       // short reason ("anchors the seating area")
        },
        required: ["itemId", "x", "z", "yawDegrees", "note"],
      },
    },
  },
  required: ["summary", "placements"],
} as const;

const SYSTEM_PROMPT = `You are an expert interior stager inside an AR real-estate app. You are given a room's floor outline as a polygon in meters (x east, z north), a style brief, and a list of stageable items with real dimensions in meters. Some items are the client's OWN furniture (userFurniture=true); others are vendor catalog items with prices.

Produce a staging plan as placements (footprint centers in meters, yaw in degrees where 0 faces +z, 90 faces +x).

Rules:
- Every itemId MUST come from the offered items list. Never invent items.
- STRONGLY prefer the client's own furniture first — seeing their own pieces fit is the point. Add catalog items only to complete the look.
- Keep every item fully inside the outline with at least 0.05 m to walls, and leave walkways of ~0.75 m between large pieces.
- Respect real dimensions: don't place a 2.4 m sofa on a 2.0 m wall segment. In small rooms, stage fewer pieces.
- Sensible arrangements: seating anchored to the longest wall facing the room center, tables centered relative to seating, rugs/side pieces supporting the main grouping.
- Match the style brief in what you choose, not just the summary text.
- Keep "summary" to 1–2 natural sentences suitable for speaking aloud; each "note" under ~8 words.
- If nothing can reasonably fit, return an empty placements array and say so in the summary.`;

Deno.serve(async (req: Request) => {
  if (req.method === "OPTIONS") return new Response("ok", { headers: cors() });

  try {
    if (req.method !== "POST") return new Response("Method not allowed", { status: 405, headers: cors() });

    requireAuth(req);
    await rateLimit(req, 100);

    const apiKey = Deno.env.get("ANTHROPIC_API_KEY");
    if (!apiKey) throw new GuardError(500, "ANTHROPIC_API_KEY not configured");

    const body = await readJson<RequestBody>(req, MAX_BODY_BYTES);

    if (!Array.isArray(body.outline) || body.outline.length < 3) {
      throw new GuardError(400, "outline (>= 3 points) required");
    }
    if (body.outline.length > MAX_OUTLINE_POINTS) throw new GuardError(400, "outline too large");
    if (!Array.isArray(body.items) || body.items.length === 0) {
      throw new GuardError(400, "items required");
    }
    if (body.items.length > MAX_ITEMS) throw new GuardError(400, "too many items");
    if (body.style) capString(body.style, MAX_STYLE_CHARS, "style");

    const outline = body.outline
      .map((p) => ({ x: Number(p?.x) || 0, z: Number(p?.z) || 0 }));
    const items = body.items
      .filter((i) => i && typeof i.id === "string" && i.id.length > 0)
      .map((i) => ({
        id: String(i.id).slice(0, 80),
        name: String(i.name ?? "").slice(0, 120),
        category: String(i.category ?? "").slice(0, 60),
        widthM: Number(i.widthM) || 0,
        depthM: Number(i.depthM) || 0,
        heightM: Number(i.heightM) || 0,
        priceUsd: Number(i.priceUsd) || 0,
        userFurniture: i.userFurniture === true,
      }));
    if (items.length === 0) throw new GuardError(400, "no valid items");

    const userText = [
      `Style brief: ${body.style || "comfortable and neutral"}`,
      `Room floor outline (meters): ${JSON.stringify(outline)}`,
      `Stageable items: ${JSON.stringify(items)}`,
    ].join("\n");

    const client = new Anthropic({ apiKey });
    try {
      const response = await client.messages.create({
        model: MODEL,
        max_tokens: 2000,
        system: SYSTEM_PROMPT,
        messages: [{ role: "user", content: [{ type: "text", text: userText }] }],
        output_config: { format: { type: "json_schema", schema: SCHEMA } },
      });
      const text = response.content.find((b) => b.type === "text");
      if (!text || text.type !== "text") {
        console.error("[stage-director] no text block in model response", {
          stopReason: response.stop_reason,
          blockTypes: response.content.map((b) => b.type),
        });
        return new Response('{"summary":"I could not produce a staging plan for this room.","placements":[]}', {
          status: 200, headers: { ...cors(), "content-type": "application/json" },
        });
      }
      return new Response(text.text, { status: 200, headers: { ...cors(), "content-type": "application/json" } });
    } catch (err) {
      const message = err instanceof Error ? err.message : String(err);
      console.error("[stage-director] model call failed:", message);
      return new Response(JSON.stringify({ error: message }), {
        status: 502, headers: { ...cors(), "content-type": "application/json" },
      });
    }
  } catch (err) {
    return toResponse(err);
  }
});
