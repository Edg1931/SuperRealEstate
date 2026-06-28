// Supabase Edge Function: comps
//
// Returns comparable recent sales near a lat/lng for the Property Data feature.
// Backed by RentCast; the RentCast API key lives only here as a function secret,
// never in the Unity client:  supabase secrets set RENTCAST_API_KEY=...
//
// Response shape mirrors the C# Comp model (Assets/Scripts/PropertyData) and is
// nested under "comps" so Unity's JsonUtility can parse it (it cannot read a
// bare top-level JSON array). Everything returned is ADVISORY — comps are
// third-party records, not an appraisal (see VISION.md guardrails).

import {
  corsHeaders,
  GuardError,
  jsonHeaders,
  readJson,
  requireAuth,
  toResponse,
  validateLatLng,
} from "../_shared/guard.ts";

const RENTCAST_BASE = "https://api.rentcast.io/v1";

// Payload cap (defense-in-depth; see docs/SECURITY-AUDIT.md).
const MAX_BODY_BYTES = 64 * 1024; // 64 KB

interface RequestBody {
  lat?: number;
  lng?: number;
}

// Maps to the C# Comp model (snake_case over the wire).
interface Comp {
  address: string;
  price: number;
  beds: number;
  baths: number;
  sqft: number;
  distance_miles: number;
  sold_date: string;
  price_per_sqft: number;
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

  // Comparables come from the RentCast AVM value endpoint, which returns a
  // `comparables` array of nearby properties (with distance + correlation) used
  // for the estimate — the documented way to get comps for a point.
  //
  // NOTE: do NOT use /listings/sale?status=Sold — sale listings only support
  // status Active/Inactive (active market), not sold history. The AVM
  // comparables are the right source for "recent comparable sales nearby".
  // Verified against RentCast docs (developers.rentcast.io, 2026): /avm/value
  // accepts latitude/longitude (+ optional maxRadius, compCount, daysOld) and
  // returns { price, priceRangeLow, priceRangeHigh, comparables: [...] }.
  const params = new URLSearchParams({
    latitude: String(body.lat),
    longitude: String(body.lng),
    maxRadius: "2",        // miles
    compCount: "10",
  });
  const url = `${RENTCAST_BASE}/avm/value?${params.toString()}`;

  let upstream: Response;
  try {
    upstream = await fetch(url, {
      headers: { "X-Api-Key": apiKey, accept: "application/json" },
    });
  } catch (err) {
    const message = err instanceof Error ? err.message : String(err);
    return new Response(JSON.stringify({ error: `RentCast request failed: ${message}` }), {
      status: 502,
      headers: jsonHeaders(),
    });
  }

  if (!upstream.ok) {
    const text = await upstream.text().catch(() => "");
    return new Response(
      JSON.stringify({ error: `RentCast returned ${upstream.status}`, detail: text.slice(0, 500) }),
      { status: 502, headers: jsonHeaders() },
    );
  }

  const raw = await upstream.json().catch(() => null);

  // The AVM value response nests comps under `comparables`. Stay defensive about
  // shape (a bare array, or `listings`) so a plan/endpoint variation degrades
  // gracefully rather than erroring.
  const records: unknown[] = Array.isArray((raw as Record<string, unknown>)?.comparables)
    ? (raw as { comparables: unknown[] }).comparables
    : Array.isArray(raw)
      ? raw
      : Array.isArray((raw as Record<string, unknown>)?.listings)
        ? (raw as { listings: unknown[] }).listings
        : [];

  const comps: Comp[] = records.map((r) => mapComp(r as Record<string, unknown>)).filter(Boolean) as Comp[];

  return new Response(JSON.stringify({ comps }), { status: 200, headers: jsonHeaders() });
  } catch (err) {
    return toResponse(err);
  }
});

// Maps a RentCast AVM comparable to the Comp shape. Verified field names
// (RentCast property/listing schema): formattedAddress, price, bedrooms,
// bathrooms, squareFootage, lotSize, distance, correlation, listedDate,
// lastSeenDate. lastSalePrice/lastSaleDate appear on full property records; we
// fall back to them when present. Extra `??` aliases keep this resilient to
// plan/endpoint variation.
function mapComp(r: Record<string, unknown>): Comp | null {
  if (!r) return null;

  const price = num(r.price ?? r.lastSalePrice ?? r.salePrice ?? r.listPrice);
  const sqft = num(r.squareFootage ?? r.livingArea ?? r.sqft);
  const address = str(r.formattedAddress ?? r.address ?? r.addressLine1);
  if (!address && price === 0) return null; // skip empty records

  return {
    address,
    price,
    beds: num(r.bedrooms ?? r.beds),
    baths: num(r.bathrooms ?? r.baths),
    sqft,
    // AVM comparables include `distance` (miles) from the subject point.
    distance_miles: num(r.distance ?? r.distanceMiles),
    // Comps from the AVM are listing-based; prefer a true sale date when the
    // record carries one, else the last-seen/listed date.
    sold_date: str(r.lastSaleDate ?? r.saleDate ?? r.soldDate ?? r.lastSeenDate ?? r.listedDate),
    price_per_sqft: sqft > 0 ? price / sqft : 0,
  };
}

function num(v: unknown): number {
  const n = typeof v === "string" ? parseFloat(v) : (v as number);
  return Number.isFinite(n) ? (n as number) : 0;
}

function str(v: unknown): string {
  return typeof v === "string" ? v : "";
}
