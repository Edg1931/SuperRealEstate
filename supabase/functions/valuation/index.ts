// Supabase Edge Function: valuation
//
// Returns an automated valuation (AVM) for the property at a lat/lng for the
// Property Data feature. Backed by RentCast; the RentCast API key lives only
// here as a function secret, never in the Unity client:
//   supabase secrets set RENTCAST_API_KEY=...
//
// Response shape mirrors the C# PropertyValuation model
// (Assets/Scripts/PropertyData): estimate, low, high, rent_estimate (snake_case).
// ADVISORY: an AVM is an automated estimate with a confidence range, NOT an
// appraisal — the client frames it as a starting point (see VISION.md guardrails).

const RENTCAST_BASE = "https://api.rentcast.io/v1";

function corsHeaders(): HeadersInit {
  return {
    "Access-Control-Allow-Origin": "*",
    "Access-Control-Allow-Headers": "authorization, content-type, apikey",
    "Access-Control-Allow-Methods": "POST, OPTIONS",
  };
}

function jsonHeaders(): HeadersInit {
  return { ...corsHeaders(), "content-type": "application/json" };
}

interface RequestBody {
  lat?: number;
  lng?: number;
}

// Maps to the C# PropertyValuation model (snake_case over the wire).
interface PropertyValuation {
  estimate: number;
  low: number;
  high: number;
  rent_estimate: number;
}

Deno.serve(async (req: Request) => {
  if (req.method === "OPTIONS") {
    return new Response("ok", { headers: corsHeaders() });
  }
  if (req.method !== "POST") {
    return new Response("Method not allowed", { status: 405, headers: corsHeaders() });
  }

  const apiKey = Deno.env.get("RENTCAST_API_KEY");
  if (!apiKey) {
    return new Response(JSON.stringify({ error: "RENTCAST_API_KEY not configured" }), {
      status: 500,
      headers: jsonHeaders(),
    });
  }

  let body: RequestBody;
  try {
    body = await req.json();
  } catch {
    return new Response(JSON.stringify({ error: "Invalid JSON body" }), {
      status: 400,
      headers: jsonHeaders(),
    });
  }

  if (typeof body.lat !== "number" || typeof body.lng !== "number") {
    return new Response(JSON.stringify({ error: "lat and lng are required numbers" }), {
      status: 400,
      headers: jsonHeaders(),
    });
  }

  // RentCast AVM: value estimate and (separately) rent estimate. VERIFY against
  // current RentCast docs: exact paths (/avm/value, /avm/rent/long-term),
  // required params, and the response field names (price/priceRangeLow/
  // priceRangeHigh, rent/rentRangeLow) may differ by plan/version.
  const valueParams = new URLSearchParams({
    latitude: String(body.lat),
    longitude: String(body.lng),
  });

  let value: Record<string, unknown> | null = null;
  let rent: Record<string, unknown> | null = null;

  try {
    const valueResp = await fetch(`${RENTCAST_BASE}/avm/value?${valueParams.toString()}`, {
      headers: { "X-Api-Key": apiKey, accept: "application/json" },
    });
    if (valueResp.ok) value = await valueResp.json().catch(() => null);
    else {
      const text = await valueResp.text().catch(() => "");
      return new Response(
        JSON.stringify({ error: `RentCast value returned ${valueResp.status}`, detail: text.slice(0, 500) }),
        { status: 502, headers: jsonHeaders() },
      );
    }

    // Rent estimate is best-effort; if it fails we still return the value AVM.
    const rentResp = await fetch(`${RENTCAST_BASE}/avm/rent/long-term?${valueParams.toString()}`, {
      headers: { "X-Api-Key": apiKey, accept: "application/json" },
    });
    if (rentResp.ok) rent = await rentResp.json().catch(() => null);
  } catch (err) {
    const message = err instanceof Error ? err.message : String(err);
    return new Response(JSON.stringify({ error: `RentCast request failed: ${message}` }), {
      status: 502,
      headers: jsonHeaders(),
    });
  }

  const valuation: PropertyValuation = {
    estimate: num(value?.price ?? value?.value ?? value?.estimate),
    low: num(value?.priceRangeLow ?? value?.valueLow ?? value?.low),
    high: num(value?.priceRangeHigh ?? value?.valueHigh ?? value?.high),
    rent_estimate: num(rent?.rent ?? rent?.price ?? rent?.estimate),
  };

  return new Response(JSON.stringify(valuation), { status: 200, headers: jsonHeaders() });
});

function num(v: unknown): number {
  const n = typeof v === "string" ? parseFloat(v) : (v as number);
  return Number.isFinite(n) ? (n as number) : 0;
}
