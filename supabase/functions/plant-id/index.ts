// Supabase Edge Function: plant-id
//
// Identifies a plant/tree/flower from a captured frame and returns structured
// PlantIdentification[] (matching Assets/Scripts/Insights/PlantIdentification).
// Walk up to a plant in AR → name + care + toxicity + cost.
//
// This implementation uses Claude multimodal for an immediately-working,
// advisory result. For botanical-grade species accuracy, front this with
// Pl@ntNet / Plant.id (species + score) and use Claude to enrich care/toxicity.
// Key stays server-side:  supabase secrets set ANTHROPIC_API_KEY=sk-ant-...

import Anthropic from "npm:@anthropic-ai/sdk@0.69.0";

const MODEL = "claude-opus-4-8";

const PLANTS_SCHEMA = {
  type: "object",
  additionalProperties: false,
  properties: {
    plants: {
      type: "array",
      items: {
        type: "object",
        additionalProperties: false,
        properties: {
          commonName: { type: "string" },
          scientificName: { type: "string" },
          type: { type: "string" },          // tree | shrub | flower | grass | groundcover | succulent
          careLevel: { type: "string" },      // easy | moderate | high
          water: { type: "string" },          // low | medium | high
          sun: { type: "string" },            // full | partial | shade
          matureSize: { type: "string" },
          toxicToPetsOrKids: { type: "boolean" },
          invasive: { type: "boolean" },
          pollenAllergy: { type: "string" },  // none | low | moderate | high
          replacementCost: { type: "number" },
          confidence: { type: "number" },
          note: { type: "string" },
        },
        required: [
          "commonName", "scientificName", "type", "careLevel", "water", "sun",
          "matureSize", "toxicToPetsOrKids", "invasive", "pollenAllergy",
          "replacementCost", "confidence", "note",
        ],
      },
    },
  },
  required: ["plants"],
} as const;

const SYSTEM_PROMPT = `You identify plants, trees, and flowers from a photo during a real-estate walkthrough, for an agent to relay to a buyer.

For each clearly visible plant:
- Give common + scientific name and type.
- Provide care level, water and sun needs, and mature size.
- Flag toxicToPetsOrKids and invasive honestly (these matter to buyers with children/pets).
- Give pollenAllergy potential and a rough replacementCost (USD).
- Set confidence honestly (0..1); if unsure of the exact species, give the most likely genus and lower the confidence.
All identifications are ADVISORY — note in "note" anything to confirm with a horticulturist/arborist when it matters (e.g. tree health or removal). Prefer 1-4 plants over an exhaustive list.`;

function corsHeaders(): HeadersInit {
  return {
    "Access-Control-Allow-Origin": "*",
    "Access-Control-Allow-Headers": "authorization, content-type",
    "Access-Control-Allow-Methods": "POST, OPTIONS",
  };
}

Deno.serve(async (req: Request) => {
  if (req.method === "OPTIONS") return new Response("ok", { headers: corsHeaders() });
  if (req.method !== "POST") {
    return new Response("Method not allowed", { status: 405, headers: corsHeaders() });
  }

  const apiKey = Deno.env.get("ANTHROPIC_API_KEY");
  if (!apiKey) {
    return new Response(JSON.stringify({ error: "ANTHROPIC_API_KEY not configured" }), {
      status: 500, headers: { ...corsHeaders(), "content-type": "application/json" },
    });
  }

  let body: { imageBase64?: string; mediaType?: "image/jpeg" | "image/png" };
  try {
    body = await req.json();
  } catch {
    return new Response(JSON.stringify({ error: "Invalid JSON body" }), {
      status: 400, headers: { ...corsHeaders(), "content-type": "application/json" },
    });
  }

  if (!body.imageBase64) {
    return new Response(JSON.stringify({ error: "imageBase64 required" }), {
      status: 400, headers: { ...corsHeaders(), "content-type": "application/json" },
    });
  }

  const client = new Anthropic({ apiKey });
  try {
    const response = await client.messages.create({
      model: MODEL,
      max_tokens: 2000,
      system: SYSTEM_PROMPT,
      messages: [{
        role: "user",
        content: [
          { type: "image", source: { type: "base64", media_type: body.mediaType ?? "image/jpeg", data: body.imageBase64 } },
          { type: "text", text: "Identify the plant(s) in this photo for a property walkthrough." },
        ],
      }],
      output_config: { format: { type: "json_schema", schema: PLANTS_SCHEMA } },
    });

    const text = response.content.find((b) => b.type === "text");
    const json = text && text.type === "text" ? text.text : '{"plants":[]}';
    return new Response(json, { status: 200, headers: { ...corsHeaders(), "content-type": "application/json" } });
  } catch (err) {
    const message = err instanceof Error ? err.message : String(err);
    return new Response(JSON.stringify({ error: message }), {
      status: 502, headers: { ...corsHeaders(), "content-type": "application/json" },
    });
  }
});
