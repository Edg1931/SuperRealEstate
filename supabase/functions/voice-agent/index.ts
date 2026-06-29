// Supabase Edge Function: voice-agent
//
// The conversational brain for the AR app. The device does speech-to-text and
// text-to-speech (Android XR / Gemini, or iOS speech); this turns the recognized
// transcript (+ optional room measurements, location, and a snapshot) into a
// short spoken reply AND a structured app action the client runs
// (matching Assets/Scripts/Voice). Anthropic key stays server-side:
//   supabase secrets set ANTHROPIC_API_KEY=sk-ant-...
//   supabase functions deploy voice-agent

import Anthropic from "npm:@anthropic-ai/sdk@0.69.0";
import {
  capBase64,
  capString,
  corsHeaders as cors,
  GuardError,
  readJson,
  rateLimit,
  requireAuth,
  toResponse,
  validateLatLng,
} from "../_shared/guard.ts";

const MODEL = "claude-opus-4-8";

// Payload caps (defense-in-depth; see docs/SECURITY-AUDIT.md).
const MAX_BODY_BYTES = 8 * 1024 * 1024; // 8 MB
const MAX_IMAGE_BYTES = 6 * 1024 * 1024; // ~6 MB decoded
const MAX_TRANSCRIPT_CHARS = 4000;

const ACTION_TYPES = [
  "none", "measure_room", "identify_plant", "estimate_material",
  "recognize_finish", "remove_wall", "stage_furniture", "show_comps",
] as const;

const SCHEMA = {
  type: "object",
  additionalProperties: false,
  properties: {
    reply: { type: "string" }, // concise, natural, spoken aloud
    action: {
      type: "object",
      additionalProperties: false,
      properties: {
        type: { type: "string", enum: ACTION_TYPES },
        target: { type: "string" },   // wall id / item / room (or "")
        material: { type: "string" }, // for estimate_material: mulch/paint/flooring (or "")
        params: { type: "string" },   // extras e.g. "depth=3in" (or "")
      },
      required: ["type", "target", "material", "params"],
    },
  },
  required: ["reply", "action"],
} as const;

const SYSTEM_PROMPT = `You are the hands-free voice assistant inside an AR real-estate app, used by an agent walking a property with buyers.

Turn the user's spoken request into:
1) "reply": a short, natural sentence to speak aloud (no markdown, no lists).
2) "action": one app action to perform, from this set:
   - measure_room: measure the current room.
   - identify_plant: identify a plant/tree (use when they point/ask "what's this plant/tree").
   - estimate_material: how much of a material (set "material": mulch|paint|flooring|sod|gravel|concrete|pavers|fence; put specifics like depth in "params").
   - recognize_finish: identify the finish on a surface ("what paint/floor is this").
   - remove_wall: virtually remove a wall (put a wall reference in "target" if given).
   - stage_furniture: place furniture ("target" = the item, e.g. "sofa").
   - show_comps: show neighborhood comparables.
   - none: pure conversation / answer, no app action.
Pick the single best action; use "none" if it's just a question you can answer in the reply. Leave unused string fields as "". Keep replies under ~2 sentences. If a snapshot/measurements are provided, use them.`;

Deno.serve(async (req: Request) => {
  if (req.method === "OPTIONS") return new Response("ok", { headers: cors() });

  try {
  if (req.method !== "POST") return new Response("Method not allowed", { status: 405, headers: cors() });

  requireAuth(req);
  await rateLimit(req, 400);

  const apiKey = Deno.env.get("ANTHROPIC_API_KEY");
  if (!apiKey) {
    throw new GuardError(500, "ANTHROPIC_API_KEY not configured");
  }

  const body = await readJson<{
    transcript?: string;
    measurements?: Record<string, number>;
    latitude?: number; longitude?: number; imageBase64?: string;
  }>(req, MAX_BODY_BYTES);

  if (!body.transcript) {
    throw new GuardError(400, "transcript required");
  }
  capString(body.transcript, MAX_TRANSCRIPT_CHARS, "transcript");
  capBase64(body.imageBase64, MAX_IMAGE_BYTES, "imageBase64");
  validateLatLng(body.latitude, body.longitude);

  const content: Anthropic.ContentBlockParam[] = [];
  if (body.imageBase64) {
    content.push({ type: "image", source: { type: "base64", media_type: "image/jpeg", data: body.imageBase64 } });
  }
  content.push({
    type: "text",
    text: [
      `User said: "${body.transcript}"`,
      body.measurements ? `Room measurements: ${JSON.stringify(body.measurements)}` : "",
      body.latitude != null ? `Location: ${body.latitude}, ${body.longitude}` : "",
    ].filter(Boolean).join("\n"),
  });

  const client = new Anthropic({ apiKey });
  try {
    const response = await client.messages.create({
      model: MODEL,
      max_tokens: 600,
      system: SYSTEM_PROMPT,
      messages: [{ role: "user", content }],
      output_config: { format: { type: "json_schema", schema: SCHEMA } },
    });
    const text = response.content.find((b) => b.type === "text");
    if (!text || text.type !== "text") {
      console.error("[voice-agent] no text block in model response", {
        stopReason: response.stop_reason,
        blockTypes: response.content.map((b) => b.type),
      });
      return new Response(
        '{"reply":"Sorry, I didn\'t catch that.","action":{"type":"none","target":"","material":"","params":""}}',
        { status: 200, headers: { ...cors(), "content-type": "application/json" } },
      );
    }
    return new Response(text.text, { status: 200, headers: { ...cors(), "content-type": "application/json" } });
  } catch (err) {
    const message = err instanceof Error ? err.message : String(err);
    return new Response(JSON.stringify({ error: message }), {
      status: 502, headers: { ...cors(), "content-type": "application/json" },
    });
  }
  } catch (err) {
    return toResponse(err);
  }
});
