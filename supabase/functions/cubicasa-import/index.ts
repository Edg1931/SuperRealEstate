// Supabase Edge Function: cubicasa-import
//
// Turns a CubiCasa floor-plan export into the BuildingModel JSON the Unity
// Acquisition layer parses (Assets/Scripts/Acquisition/BuildingModelDto.cs).
// CubiCasa produces a VECTOR floor plan with real walls + rooms, so this is a
// primary editable-import path (see docs/COMPETITIVE-LANDSCAPE.md).
//
// The CubiCasa API key lives only here as a function secret, never in the Unity
// client:  supabase secrets set CUBICASA_API_KEY=...
//
// Request:  { jobId?: string, exportUrl?: string }
//   - jobId     → fetch the export for that CubiCasa order/job
//   - exportUrl → fetch a pre-known export artifact URL directly
// Response: BuildingModel JSON (see shape in BuildingModelDto.cs):
//   { id, sourceType:"cubicasa", walls:[...], rooms:[...] }  with plan-view
//   {x,y} meters (x=east, y=north).
//
// CORS/OPTIONS + missing-key 500 mirror scene-insights / parcels.

// === VERIFY against current CubiCasa API docs before production ===
// CubiCasa's developer/partner API surface evolves; confirm all of the below.
//   * Base host + auth style. CubiCasa has used both an "Order/Result" REST API
//     and a vectorized geometry export. Confirm whether auth is a Bearer token,
//     an "x-api-key" header, or a query token.
const CUBICASA_BASE = "https://api.cubi.casa"; // VERIFY exact host/path.

// Units: CubiCasa geometry is typically delivered in centimeters or millimeters.
// VERIFY the unit of the export you consume and set this divisor to convert to
// METERS (cm → 100, mm → 1000). Wrong units = wrong room areas downstream.
const UNITS_PER_METER = 100; // VERIFY: assumes centimeters.

import {
  corsHeaders,
  GuardError,
  jsonHeaders,
  readJson,
  requireAuth,
  toResponse,
} from "../_shared/guard.ts";

// Payload cap (defense-in-depth; see docs/SECURITY-AUDIT.md).
const MAX_BODY_BYTES = 64 * 1024; // 64 KB

interface RequestBody {
  jobId?: string;
  exportUrl?: string;
}

interface Point {
  x: number; // east, meters
  y: number; // north, meters
}

interface Opening {
  id: string;
  kind: "Door" | "Window" | "Passage";
  offsetM: number;
  widthM: number;
  heightM: number;
  sillHeightM: number;
}

interface Wall {
  id: string;
  start: Point;
  end: Point;
  thicknessM: number;
  heightM: number;
  isExterior: boolean;
  openings: Opening[];
}

interface Room {
  id: string;
  name: string;
  floorOutline: Point[];
  ceilingHeightM: number;
}

interface BuildingModel {
  id: string;
  sourceType: string;
  walls: Wall[];
  rooms: Room[];
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

  const apiKey = Deno.env.get("CUBICASA_API_KEY");
  if (!apiKey) {
    throw new GuardError(500, "CUBICASA_API_KEY not configured");
  }

  const body = await readJson<RequestBody>(req, MAX_BODY_BYTES);

  if (!body.jobId && !body.exportUrl) {
    throw new GuardError(400, "jobId or exportUrl is required");
  }

  // VERIFY: the exact export endpoint + field name for the vector geometry.
  // Commonly the floor-plan geometry is delivered as a JSON/GeoJSON artifact
  // either inline in the order result or behind a signed export URL.
  const url = body.exportUrl ??
    `${CUBICASA_BASE}/v2/orders/${encodeURIComponent(body.jobId!)}/export?format=json`;

  let upstream: Response;
  try {
    upstream = await fetch(url, {
      headers: {
        accept: "application/json",
        // VERIFY auth header name/style (Bearer vs x-api-key vs query token).
        authorization: `Bearer ${apiKey}`,
        "x-api-key": apiKey,
      },
    });
  } catch (err) {
    const message = err instanceof Error ? err.message : String(err);
    return new Response(JSON.stringify({ error: `CubiCasa request failed: ${message}` }), {
      status: 502,
      headers: jsonHeaders(),
    });
  }

  if (!upstream.ok) {
    const text = await upstream.text().catch(() => "");
    return new Response(
      JSON.stringify({ error: `CubiCasa returned ${upstream.status}`, detail: text.slice(0, 500) }),
      { status: 502, headers: jsonHeaders() },
    );
  }

  const raw = await upstream.json().catch(() => null);
  const model = mapModel(raw as Record<string, unknown>, body.jobId ?? "cubicasa");

  return new Response(JSON.stringify(model), { status: 200, headers: jsonHeaders() });
  } catch (err) {
    return toResponse(err);
  }
});

// Maps a CubiCasa vector floor-plan export to the BuildingModel JSON shape.
// Field names below are illustrative — VERIFY against the live export schema.
function mapModel(raw: Record<string, unknown>, jobId: string): BuildingModel {
  const empty: BuildingModel = {
    id: `cubicasa:${jobId}`,
    sourceType: "cubicasa",
    walls: [],
    rooms: [],
  };
  if (!raw) return empty;

  // VERIFY: CubiCasa may nest geometry under "floorplan", "floors[0]",
  // "result", or return GeoJSON features. Adjust these accessors accordingly.
  const plan = (raw.floorplan ?? raw.floor_plan ?? raw.result ?? raw) as Record<string, unknown>;

  const rawWalls = (plan.walls ?? []) as Array<Record<string, unknown>>;
  const rawRooms = (plan.rooms ?? plan.spaces ?? []) as Array<Record<string, unknown>>;

  // VERIFY: default storey height. CubiCasa floor plans are 2D; ceiling height
  // is often not present in the vector export. Use a sane residential default
  // and let the agent edit it. Prefer any provided value.
  const defaultHeightM = num(plan.ceiling_height ?? plan.storey_height) / UNITS_PER_METER || 2.5;

  const walls: Wall[] = rawWalls.map((w, i) => {
    // VERIFY: wall geometry field names (start/end vs from/to vs points[]),
    // and whether thickness is "thickness"/"width". Coordinates assumed
    // {x,y} in UNITS_PER_METER units.
    const start = pointOf(w.start ?? w.from ?? (asArray(w.points)?.[0]));
    const end = pointOf(w.end ?? w.to ?? (asArray(w.points)?.[1]));
    const thicknessUnits = num(w.thickness ?? w.width);
    return {
      id: str(w.id ?? w.uuid) || `w${i}`,
      start,
      end,
      thicknessM: thicknessUnits > 0 ? thicknessUnits / UNITS_PER_METER : 0.1,
      heightM: defaultHeightM,
      isExterior: bool(w.is_exterior ?? w.exterior ?? w.external),
      openings: mapOpenings(asArray(w.openings ?? w.doors_windows)),
    };
  });

  const rooms: Room[] = rawRooms.map((r, i) => {
    // VERIFY: room outline field (polygon / outline / boundary / coordinates)
    // and whether it is [[x,y],...] or [{x,y},...].
    const outline = asArray(r.polygon ?? r.outline ?? r.boundary ?? r.coordinates) ?? [];
    return {
      id: str(r.id ?? r.uuid) || `r${i}`,
      name: str(r.name ?? r.label ?? r.type) || "Room",
      floorOutline: outline.map((p) => pointOf(p)),
      ceilingHeightM: defaultHeightM,
    };
  });

  return { id: `cubicasa:${jobId}`, sourceType: "cubicasa", walls, rooms };
}

function mapOpenings(raw: Array<unknown> | null): Opening[] {
  if (!raw) return [];
  return raw.map((o, i) => {
    const rec = (o ?? {}) as Record<string, unknown>;
    // VERIFY: CubiCasa opening type strings; normalize to Door/Window/Passage.
    const rawType = str(rec.type ?? rec.kind).toLowerCase();
    const kind: Opening["kind"] = rawType.includes("window")
      ? "Window"
      : rawType.includes("pass") || rawType.includes("opening")
        ? "Passage"
        : "Door";
    return {
      id: str(rec.id) || `o${i}`,
      kind,
      offsetM: num(rec.offset ?? rec.position) / UNITS_PER_METER,
      widthM: num(rec.width) / UNITS_PER_METER,
      heightM: num(rec.height) / UNITS_PER_METER || (kind === "Window" ? 1.2 : 2.0),
      sillHeightM: num(rec.sill ?? rec.sill_height) / UNITS_PER_METER,
    };
  });
}

// Reads a {x,y} or [x,y] point in source units and converts to meters.
function pointOf(p: unknown): Point {
  if (Array.isArray(p) && p.length >= 2) {
    return { x: num(p[0]) / UNITS_PER_METER, y: num(p[1]) / UNITS_PER_METER };
  }
  const rec = (p ?? {}) as Record<string, unknown>;
  return { x: num(rec.x) / UNITS_PER_METER, y: num(rec.y) / UNITS_PER_METER };
}

function asArray(v: unknown): Array<unknown> | null {
  return Array.isArray(v) ? v : null;
}

function num(v: unknown): number {
  const n = typeof v === "string" ? parseFloat(v) : (v as number);
  return Number.isFinite(n) ? (n as number) : 0;
}

function str(v: unknown): string {
  return typeof v === "string" ? v : (v == null ? "" : String(v));
}

function bool(v: unknown): boolean {
  return v === true || v === "true" || v === 1;
}
