// Supabase Edge Function: parcels
//
// Returns the parcel (lot) record for the lot at a lat/lng for the Property Data
// feature. Backed by Regrid; the Regrid API key lives only here as a function
// secret, never in the Unity client:  supabase secrets set REGRID_API_KEY=...
//
// Response shape mirrors the C# ParcelInfo model (Assets/Scripts/PropertyData):
// apn, lot_size_acres, zoning, and a boundary as a closed ring of plan-view
// {x,y} meters RELATIVE to the queried point (origin = lat/lng), ready to draw
// as a ground overlay. ADVISORY: GIS data is approximate, not a survey
// (see VISION.md guardrails).

import {
  corsHeaders,
  GuardError,
  jsonHeaders,
  readJson,
  requireAuth,
  toResponse,
  validateLatLng,
} from "../_shared/guard.ts";

const REGRID_BASE = "https://app.regrid.com/api/v2";

// Meters per degree of latitude (≈ constant). Longitude is scaled by cos(lat).
const METERS_PER_DEG_LAT = 111_320;

// Payload cap (defense-in-depth; see docs/SECURITY-AUDIT.md).
const MAX_BODY_BYTES = 64 * 1024; // 64 KB

interface RequestBody {
  lat?: number;
  lng?: number;
}

interface Point {
  x: number; // east, meters
  y: number; // north, meters
}

// Maps to the C# ParcelInfo model (snake_case over the wire).
interface ParcelInfo {
  apn: string;
  lot_size_acres: number;
  zoning: string;
  boundary: Point[];
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

  const apiKey = Deno.env.get("REGRID_API_KEY");
  if (!apiKey) {
    throw new GuardError(500, "REGRID_API_KEY not configured");
  }

  const body = await readJson<RequestBody>(req, MAX_BODY_BYTES);

  if (typeof body.lat !== "number" || typeof body.lng !== "number") {
    throw new GuardError(400, "lat and lng are required numbers");
  }
  validateLatLng(body.lat, body.lng);

  // Regrid "Parcels by point" — returns a GeoJSON FeatureCollection for the
  // parcel containing the point. Verified against Regrid docs (support.regrid.com,
  // 2026): the response is a FeatureCollection; each feature's standardized
  // attributes live under properties.fields (parcelnumb, parcelnumb_no_formatting,
  // ll_gisacre, zoning, owner, address, …). Auth is the `token` query param on v1
  // (this `/parcels/point` path). CONFIRM your account's API version — v2 paths
  // are under /api/v2 and may use a Bearer header instead.
  const params = new URLSearchParams({
    lat: String(body.lat),
    lon: String(body.lng),
    token: apiKey,
  });
  const url = `${REGRID_BASE}/parcels/point?${params.toString()}`;

  let upstream: Response;
  try {
    upstream = await fetch(url, { headers: { accept: "application/json" } });
  } catch (err) {
    const message = err instanceof Error ? err.message : String(err);
    return new Response(JSON.stringify({ error: `Regrid request failed: ${message}` }), {
      status: 502,
      headers: jsonHeaders(),
    });
  }

  if (!upstream.ok) {
    const text = await upstream.text().catch(() => "");
    return new Response(
      JSON.stringify({ error: `Regrid returned ${upstream.status}`, detail: text.slice(0, 500) }),
      { status: 502, headers: jsonHeaders() },
    );
  }

  const raw = await upstream.json().catch(() => null);
  const parcel = mapParcel(raw as Record<string, unknown>, body.lat, body.lng);

  return new Response(JSON.stringify(parcel), { status: 200, headers: jsonHeaders() });
  } catch (err) {
    return toResponse(err);
  }
});

// Maps a Regrid GeoJSON FeatureCollection to ParcelInfo, projecting the boundary
// to plan-view meters relative to the queried point. Field names below are the
// commonly documented Regrid ones; VERIFY and adjust per the live response.
function mapParcel(raw: Record<string, unknown>, lat: number, lng: number): ParcelInfo {
  const empty: ParcelInfo = { apn: "", lot_size_acres: 0, zoning: "", boundary: [] };
  if (!raw) return empty;

  const features = (raw.features ?? (raw.parcels as Record<string, unknown>)?.features) as
    | Array<Record<string, unknown>>
    | undefined;
  const feature = features?.[0];
  if (!feature) return empty;

  // Regrid nests the standardized attributes under properties.fields.
  const props = (feature.properties ?? {}) as Record<string, unknown>;
  const fields = (props.fields ?? props) as Record<string, unknown>;

  const apn = str(fields.parcelnumb ?? fields.parcelnumb_no_formatting ?? fields.apn);
  const acres = num(fields.ll_gisacre ?? fields.gisacre ?? fields.acres ?? fields.deeded_acres);
  const zoning = str(fields.zoning ?? fields.zoning_description ?? fields.usedesc);

  const boundary = projectBoundary(feature.geometry as Record<string, unknown>, lat, lng);

  return { apn, lot_size_acres: acres, zoning, boundary };
}

// Projects a GeoJSON Polygon/MultiPolygon outer ring to {x,y} meters relative to
// (originLat, originLng) using an equirectangular approximation (good for a
// parcel-sized footprint). x = east, y = north.
function projectBoundary(
  geometry: Record<string, unknown> | undefined,
  originLat: number,
  originLng: number,
): Point[] {
  if (!geometry) return [];

  const ring = outerRing(geometry);
  if (!ring) return [];

  const cosLat = Math.cos((originLat * Math.PI) / 180);
  return ring.map(([lng, lat]) => ({
    x: (lng - originLng) * METERS_PER_DEG_LAT * cosLat,
    y: (lat - originLat) * METERS_PER_DEG_LAT,
  }));
}

// Returns the first (outer) ring of a Polygon, or of the first polygon of a
// MultiPolygon, as [lng, lat] pairs.
function outerRing(geometry: Record<string, unknown>): Array<[number, number]> | null {
  const type = geometry.type;
  const coords = geometry.coordinates as unknown;

  if (type === "Polygon" && Array.isArray(coords)) {
    return coords[0] as Array<[number, number]>;
  }
  if (type === "MultiPolygon" && Array.isArray(coords)) {
    const first = coords[0] as unknown;
    if (Array.isArray(first)) return first[0] as Array<[number, number]>;
  }
  return null;
}

function num(v: unknown): number {
  const n = typeof v === "string" ? parseFloat(v) : (v as number);
  return Number.isFinite(n) ? (n as number) : 0;
}

function str(v: unknown): string {
  return typeof v === "string" ? v : (v == null ? "" : String(v));
}
