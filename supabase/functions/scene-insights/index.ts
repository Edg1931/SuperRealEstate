// Supabase Edge Function: scene-insights
//
// Powers the AI "superpower" — receives a captured frame (+ optional room
// measurements, location, and the buyer's stated preferences) and returns
// structured, ready-to-relay insights that the headset/phone renders as cards.
//
// The Anthropic API key lives only here as a function secret, never in the
// Unity client:  supabase secrets set ANTHROPIC_API_KEY=sk-ant-...
//
// Response shape mirrors the C# SceneInsight model (Assets/Scripts/Insights).
// Condition / CodeClearance / Comp insights are returned with isAdvisory=true
// and a disclaimer, enforcing the responsible-AI guardrails in VISION.md.

import Anthropic from "npm:@anthropic-ai/sdk@0.69.0";

const MODEL = "claude-opus-4-8";

const CATEGORIES = [
  "Measurement", "Cost", "Condition", "Light", "Comp",
  "CodeClearance", "Vegetation", "Appliance", "TalkingPoint", "Preference",
] as const;

const SEVERITIES = ["Info", "Suggestion", "Caution"] as const;

// JSON schema for structured output. Note: structured outputs do not support
// numeric min/max or string length constraints — keep the schema to types/enums.
const ANALYSIS_SCHEMA = {
  type: "object",
  additionalProperties: false,
  properties: {
    insights: {
      type: "array",
      items: {
        type: "object",
        additionalProperties: false,
        properties: {
          category: { type: "string", enum: CATEGORIES },
          severity: { type: "string", enum: SEVERITIES },
          title: { type: "string" },
          detail: { type: "string" },
          suggestedTalkingPoint: { type: "string" },
          confidence: { type: "number" },
          isAdvisory: { type: "boolean" },
          disclaimer: { type: "string" },
        },
        required: [
          "category", "severity", "title", "detail",
          "suggestedTalkingPoint", "confidence", "isAdvisory", "disclaimer",
        ],
      },
    },
    // Per-surface recognized CURRENT finishes (paint brand/color, flooring,
    // counters...) so the app can show "what's here + what it costs" and seed a
    // one-tap re-finish. Brand/price are best-effort and advisory.
    surfaces: {
      type: "array",
      items: {
        type: "object",
        additionalProperties: false,
        properties: {
          surfaceKind: { type: "string" },
          materialType: { type: "string" },
          brand: { type: "string" },
          product: { type: "string" },
          colorHex: { type: "string" },
          estimatedUnitCost: { type: "number" },
          unit: { type: "string" },
          confidence: { type: "number" },
          isAdvisory: { type: "boolean" },
          disclaimer: { type: "string" },
          note: { type: "string" },
        },
        required: [
          "surfaceKind", "materialType", "brand", "product", "colorHex",
          "estimatedUnitCost", "unit", "confidence", "isAdvisory", "disclaimer", "note",
        ],
      },
    },
  },
  required: ["insights", "surfaces"],
} as const;

const SYSTEM_PROMPT = `You are the analysis engine for an AR toolkit used by real estate agents during in-person home showings. You receive a photo of part of a home (or its grounds) plus optional context, and you return concise, accurate insights the agent can relay to a buyer.

Guidelines:
- Be specific and grounded in what is visible. Do not invent details you cannot see.
- For each insight, write a short "suggestedTalkingPoint" the agent can say naturally to a client.
- Categories: Measurement, Cost, Condition, Light, Comp, CodeClearance, Vegetation, Appliance, TalkingPoint, Preference.
- ADVISORY RULE: any Condition, CodeClearance, or Comp insight MUST set isAdvisory=true and include a disclaimer making clear it is not a professional determination (e.g. "Worth confirming with a licensed inspector — not a formal assessment."). Never state defects or code violations as fact.
- Do NOT make fair-housing-sensitive characterizations of neighborhoods, demographics, or "safety". Stick to the physical property and neutral public data.
- confidence is 0.0-1.0 reflecting how sure you are.
- If buyer preferences are provided, surface matching features as Preference insights.
- Prefer 3-6 high-value insights over an exhaustive list.

Also populate "surfaces": for each clearly visible finished surface (walls, floor,
ceiling, cabinets, countertops), identify the material and, when reasonably
confident, the brand/product/color (e.g. paint "Sherwin-Williams Agreeable Gray
SW 7029") and an estimated unit cost with its unit (paint -> per_gallon, flooring
-> per_sqft). Brand/product guesses are ADVISORY (isAdvisory=true) with a short
disclaimer to confirm before purchase; set confidence honestly and leave brand/
product empty if you can't tell. This drives "what's here + what it costs" and
one-tap re-finishing.`;

function corsHeaders(): HeadersInit {
  return {
    "Access-Control-Allow-Origin": "*",
    "Access-Control-Allow-Headers": "authorization, content-type",
    "Access-Control-Allow-Methods": "POST, OPTIONS",
  };
}

interface RequestBody {
  imageBase64?: string;
  mediaType?: "image/jpeg" | "image/png";
  measurements?: Record<string, number>;
  latitude?: number;
  longitude?: number;
  headingDegrees?: number;
  buyerPreferences?: string[];
  categoryFilter?: string[];
}

Deno.serve(async (req: Request) => {
  if (req.method === "OPTIONS") {
    return new Response("ok", { headers: corsHeaders() });
  }
  if (req.method !== "POST") {
    return new Response("Method not allowed", { status: 405, headers: corsHeaders() });
  }

  const apiKey = Deno.env.get("ANTHROPIC_API_KEY");
  if (!apiKey) {
    return new Response(JSON.stringify({ error: "ANTHROPIC_API_KEY not configured" }), {
      status: 500,
      headers: { ...corsHeaders(), "content-type": "application/json" },
    });
  }

  let body: RequestBody;
  try {
    body = await req.json();
  } catch {
    return new Response(JSON.stringify({ error: "Invalid JSON body" }), {
      status: 400,
      headers: { ...corsHeaders(), "content-type": "application/json" },
    });
  }

  // Assemble the user turn: the captured frame plus a compact context block.
  const content: Anthropic.ContentBlockParam[] = [];
  if (body.imageBase64) {
    content.push({
      type: "image",
      source: {
        type: "base64",
        media_type: body.mediaType ?? "image/jpeg",
        data: body.imageBase64,
      },
    });
  }
  content.push({
    type: "text",
    text: [
      "Analyze this scene for a real estate showing.",
      body.measurements ? `Room measurements: ${JSON.stringify(body.measurements)}` : "",
      body.latitude != null ? `Location: ${body.latitude}, ${body.longitude} (heading ${body.headingDegrees ?? "?"}°)` : "",
      body.buyerPreferences?.length ? `Buyer preferences: ${body.buyerPreferences.join(", ")}` : "",
      body.categoryFilter?.length ? `Limit to categories: ${body.categoryFilter.join(", ")}` : "",
    ].filter(Boolean).join("\n"),
  });

  const client = new Anthropic({ apiKey });

  try {
    const response = await client.messages.create({
      model: MODEL,
      max_tokens: 4000,
      system: SYSTEM_PROMPT,
      messages: [{ role: "user", content }],
      output_config: { format: { type: "json_schema", schema: ANALYSIS_SCHEMA } },
    });

    const text = response.content.find((b) => b.type === "text");
    const json = text && text.type === "text" ? text.text : '{"insights":[],"surfaces":[]}';

    return new Response(json, {
      status: 200,
      headers: { ...corsHeaders(), "content-type": "application/json" },
    });
  } catch (err) {
    const message = err instanceof Error ? err.message : String(err);
    return new Response(JSON.stringify({ error: message }), {
      status: 502,
      headers: { ...corsHeaders(), "content-type": "application/json" },
    });
  }
});
