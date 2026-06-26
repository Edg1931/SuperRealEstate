// Supabase Edge Function: matterport-import
//
// Seeds a BuildingModel from a Matterport model's Property Intelligence room
// DIMENSIONS, for the Unity Acquisition layer to parse
// (Assets/Scripts/Acquisition/BuildingModelDto.cs).
//
// IMPORTANT — source reality (docs/COMPETITIVE-LANDSCAPE.md):
// Matterport gives us a baked single mesh + point cloud and READ-ONLY room
// dimensions via the Enterprise Property Intelligence API. It does NOT expose
// editable, parametric walls. So this function emits ZERO walls and instead
// SEEDS each room as a rectangle from its dimensional estimate (length × width,
// + ceiling height). The output is an approximate, editable scaffold the agent
// refines — a starting point, not a survey. For editable walls, use CubiCasa
// or RoomPlan.
//
// The Matterport token lives only here as a function secret, never in the Unity
// client:  supabase secrets set MATTERPORT_TOKEN=...
//
// Request:  { modelId: string }   (Matterport model SID, e.g. "SxQL3iGyoDo")
// Response: BuildingModel JSON: { id, sourceType:"matterport", walls:[], rooms:[...] }
//   rooms seeded from dimensions; plan-view {x,y} meters (x=east, y=north).
//
// CORS/OPTIONS + missing-key 500 mirror scene-insights / parcels.

// === VERIFY against current Matterport API docs before production ===
// Matterport exposes a GraphQL Model API. Property Intelligence fields
// (floors → rooms → dimensionalEstimates: floorArea / volume / height, plus
// per-room length/width) are ENTERPRISE-gated; confirm your account's access,
// the exact GraphQL schema field names, and the auth style (the docs use HTTP
// Basic with token id:secret, or a Bearer token, depending on credential type).
const MATTERPORT_GRAPHQL = "https://api.matterport.com/api/models/graph"; // VERIFY host/path.

// VERIFY: the live Property Intelligence schema. Field names below (room.label,
// room.dimensions.{length,width,height}, units) are illustrative.
const MODEL_QUERY = `
query GetModel($id: ID!) {
  model(id: $id) {
    id
    name
    floors {
      id
      label
      rooms {
        id
        label
        # Property Intelligence dimensional estimates (read-only).
        # VERIFY exact field names/units against your schema.
        dimensions { length width height units }
        floorArea
      }
    }
  }
}`;

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
  modelId?: string;
}

interface Point {
  x: number;
  y: number;
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
  walls: unknown[]; // always empty — Matterport yields no editable walls.
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

  const token = Deno.env.get("MATTERPORT_TOKEN");
  if (!token) {
    throw new GuardError(500, "MATTERPORT_TOKEN not configured");
  }

  const body = await readJson<RequestBody>(req, MAX_BODY_BYTES);

  if (!body.modelId) {
    throw new GuardError(400, "modelId is required");
  }

  let upstream: Response;
  try {
    upstream = await fetch(MATTERPORT_GRAPHQL, {
      method: "POST",
      headers: {
        "content-type": "application/json",
        // VERIFY auth style. Matterport token credentials are often used as
        // HTTP Basic "token_id:token_secret"; a single Bearer token is also
        // supported for some credential types. Adjust to your credential.
        authorization: token.includes(":")
          ? `Basic ${btoa(token)}`
          : `Bearer ${token}`,
      },
      body: JSON.stringify({ query: MODEL_QUERY, variables: { id: body.modelId } }),
    });
  } catch (err) {
    const message = err instanceof Error ? err.message : String(err);
    return new Response(JSON.stringify({ error: `Matterport request failed: ${message}` }), {
      status: 502,
      headers: jsonHeaders(),
    });
  }

  if (!upstream.ok) {
    const text = await upstream.text().catch(() => "");
    return new Response(
      JSON.stringify({ error: `Matterport returned ${upstream.status}`, detail: text.slice(0, 500) }),
      { status: 502, headers: jsonHeaders() },
    );
  }

  const raw = await upstream.json().catch(() => null);
  const model = mapModel(raw as Record<string, unknown>, body.modelId);

  return new Response(JSON.stringify(model), { status: 200, headers: jsonHeaders() });
  } catch (err) {
    return toResponse(err);
  }
});

// Maps a Matterport Model API response to BuildingModel JSON, SEEDING rooms from
// dimensional estimates. No walls are emitted (Matterport has none to give).
// Rooms are laid out in a non-overlapping grid since absolute room positions are
// not part of the dimensional data — positions are placeholders the agent moves.
function mapModel(raw: Record<string, unknown>, modelId: string): BuildingModel {
  const empty: BuildingModel = {
    id: `matterport:${modelId}`,
    sourceType: "matterport",
    walls: [],
    rooms: [],
  };
  if (!raw) return empty;

  const data = (raw.data ?? raw) as Record<string, unknown>;
  const model = (data.model ?? data) as Record<string, unknown>;
  if (!model) return empty;

  const floors = asArray(model.floors) ?? [];
  const rooms: Room[] = [];

  // Simple shelf layout: lay rooms left→right with a gap, wrapping rows. Purely
  // to avoid overlap; absolute placement is the agent's job after import.
  let cursorX = 0;
  let rowY = 0;
  let rowMaxDepth = 0;
  const gapM = 0.5;
  const rowWidthLimitM = 20;

  for (const floorRaw of floors) {
    const floor = (floorRaw ?? {}) as Record<string, unknown>;
    const floorRooms = asArray(floor.rooms) ?? [];

    for (let i = 0; i < floorRooms.length; i++) {
      const r = (floorRooms[i] ?? {}) as Record<string, unknown>;
      const dims = (r.dimensions ?? {}) as Record<string, unknown>;
      const divisor = unitsPerMeter(str(dims.units));

      // VERIFY field names. Fall back to deriving width from floorArea if only
      // an area is exposed (assume square-ish room).
      let lengthM = num(dims.length) / divisor;
      let widthM = num(dims.width) / divisor;
      const areaM2 = num(r.floorArea) / (divisor * divisor);
      if ((lengthM <= 0 || widthM <= 0) && areaM2 > 0) {
        lengthM = widthM = Math.sqrt(areaM2);
      }
      if (lengthM <= 0 || widthM <= 0) continue; // no usable dimensions

      const heightM = num(dims.height) / divisor || 2.5;

      if (cursorX > 0 && cursorX + lengthM > rowWidthLimitM) {
        // wrap to a new row
        rowY += rowMaxDepth + gapM;
        cursorX = 0;
        rowMaxDepth = 0;
      }

      const x0 = cursorX;
      const y0 = rowY;
      rooms.push({
        id: str(r.id) || `room${rooms.length}`,
        name: str(r.label ?? r.name) || "Room",
        // Rectangle CCW from the room's near-left corner.
        floorOutline: [
          { x: x0, y: y0 },
          { x: x0 + lengthM, y: y0 },
          { x: x0 + lengthM, y: y0 + widthM },
          { x: x0, y: y0 + widthM },
        ],
        ceilingHeightM: heightM,
      });

      cursorX += lengthM + gapM;
      if (widthM > rowMaxDepth) rowMaxDepth = widthM;
    }
  }

  return { id: `matterport:${modelId}`, sourceType: "matterport", walls: [], rooms };
}

// Maps Matterport's reported unit string to a per-meter divisor.
// VERIFY which units your Property Intelligence response uses.
function unitsPerMeter(units: string): number {
  switch (units.toLowerCase()) {
    case "cm":
    case "centimeters":
      return 100;
    case "mm":
    case "millimeters":
      return 1000;
    case "ft":
    case "feet":
      return 1 / 0.3048; // feet → meters: value * 0.3048; as divisor → 1/0.3048
    case "in":
    case "inches":
      return 1 / 0.0254;
    case "m":
    case "meters":
    default:
      return 1;
  }
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
