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

import {
  corsHeaders,
  jsonHeaders,
  GuardError,
  readJson,
  requireAuth,
  toResponse,
  validateLatLng,
} from "../_shared/guard.ts";

const RENTCAST_BASE = "https://api.rentcast.io/v1";
const MAX_BODY_BYTES = 64 * 1024; // 64 KB

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

  try {
    if (req.method !== "POST") {
      return new Response("Method not allowed", { status: 405, headers: corsHeaders() });
    }

    requireAuth(req);

    const apiKey = Deno.env.get("RENTCAST_API_KEY");
    if (!apiKey) {
      throw new GuardError(500, "RENTCAST_API_KEY not configured");
    }

    const body = await readJson<RequestBody>(req, MAX_BODY_BYTES);
    if (typeof body.lat !== "number" || typeof body.lng !== "number") {
      throw new GuardError(400, "lat and lng are required numbers");
    }
    validateLatLng(body.lat, body.lng);

    // RentCast AVM: value estimate and (separately) rent estimate. Verified
    // against RentCast docs (developers.rentcast.io, 2026): /avm/value returns
    // { price, priceRangeLow, priceRangeHigh, latitude, longitude, comparables };
    // /avm/rent/long-term returns { rent, rentRangeLow, rentRangeHigh }. Both
    // accept latitude+longitude (and optional property attributes to sharpen the
    // estimate). The `?? value?.value/estimate` aliases below are belt-and-suspenders.
    const params = new URLSearchParams({
      latitude: String(body.lat),
      longitude: String(body.lng),
    });

    let value: Record<string, unknown> | null = null;
    let rent: Record<string, unknown> | null = null;

    const valueResp = await fetch(`${RENTCAST_BASE}/avm/value?${params.toString()}`, {
      headers: { "X-Api-Key": apiKey, accept: "application/json" },
    });
    if (valueResp.ok) {
      value = await valueResp.json().catch(() => null);
    } else {
      const text = await valueResp.text().catch(() => "");
      return new Response(
        JSON.stringify({ error: `RentCast value returned ${valueResp.status}`, detail: text.slice(0, 500) }),
        { status: 502, headers: jsonHeaders() },
      );
    }

    // Rent estimate is best-effort; if it fails we still return the value AVM.
    const rentResp = await fetch(`${RENTCAST_BASE}/avm/rent/long-term?${params.toString()}`, {
      headers: { "X-Api-Key": apiKey, accept: "application/json" },
    });
    if (rentResp.ok) rent = await rentResp.json().catch(() => null);

    const valuation: PropertyValuation = {
      estimate: num(value?.price ?? value?.value ?? value?.estimate),
      low: num(value?.priceRangeLow ?? value?.valueLow ?? value?.low),
      high: num(value?.priceRangeHigh ?? value?.valueHigh ?? value?.high),
      rent_estimate: num(rent?.rent ?? rent?.price ?? rent?.estimate),
    };

    return new Response(JSON.stringify(valuation), { status: 200, headers: jsonHeaders() });
  } catch (err) {
    return toResponse(err);
  }
});

function num(v: unknown): number {
  const n = typeof v === "string" ? parseFloat(v) : (v as number);
  return Number.isFinite(n) ? (n as number) : 0;
}
