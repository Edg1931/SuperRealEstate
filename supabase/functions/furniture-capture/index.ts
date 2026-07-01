// Supabase Edge Function: furniture-capture
//
// Records a furniture capture job and creates a TO-SCALE entry in the user's
// furniture library straight away, using the AR-measured metric bounds. The fit
// check ("will it fit") only needs dimensions, so a correctly-sized placeholder
// is immediately useful; a reconstruction worker swaps in the real mesh
// (model_url) asynchronously and flips the job to 'ready'.
//
// Only metadata is sent here (name, method, photo count, bounds) — the photos
// upload to Storage separately (they exceed the body cap). Inserts go through
// PostgREST with the caller's token so RLS attributes everything to them.

import {
  capString,
  corsHeaders,
  GuardError,
  jsonHeaders,
  readJson,
  requireAuth,
  toResponse,
  userIdFromAuth,
} from "../_shared/guard.ts";

const MAX_BODY_BYTES = 8 * 1024;

interface RequestBody {
  name?: string;
  method?: string;
  photoCount?: number;
  storagePrefix?: string;
  widthM?: number;
  depthM?: number;
  heightM?: number;
}

const CAPTURE_METHODS = new Set(["object_capture", "photogrammetry", "lidar", "gaussian_splat", "manual"]);

Deno.serve(async (req: Request) => {
  if (req.method === "OPTIONS") return new Response("ok", { headers: corsHeaders() });

  try {
    if (req.method !== "POST") {
      return new Response("Method not allowed", { status: 405, headers: corsHeaders() });
    }

    requireAuth(req);
    const userId = userIdFromAuth(req);
    if (!userId) throw new GuardError(401, "A signed-in user token is required");

    const body = await readJson<RequestBody>(req, MAX_BODY_BYTES);
    capString(body.name, 120, "name");

    const method = CAPTURE_METHODS.has(body.method ?? "") ? (body.method as string) : "photogrammetry";
    const name = (body.name ?? "Furniture").trim() || "Furniture";
    const widthM = num(body.widthM), depthM = num(body.depthM), heightM = num(body.heightM);

    // The reconstruction worker later reads photos from this prefix with the
    // service role (bypassing storage RLS), so NEVER trust a client-named path:
    // it must live under the caller's own folder, with no traversal.
    let photoPrefix: string | null = null;
    if (typeof body.storagePrefix === "string" && body.storagePrefix.length > 0) {
      const p = body.storagePrefix.slice(0, 300);
      if (!p.startsWith(`${userId}/`) || p.includes("..") || p.includes("//")) {
        throw new GuardError(400, "storagePrefix must be under your own user folder");
      }
      photoPrefix = p;
    }

    const url = Deno.env.get("SUPABASE_URL");
    if (!url) throw new GuardError(500, "SUPABASE_URL not configured");

    // Forward the caller's credentials so RLS attributes the rows to them.
    const apikey = req.headers.get("apikey") ?? "";
    const authorization = req.headers.get("authorization") ?? "";
    const writeHeaders = {
      apikey,
      authorization,
      "content-type": "application/json",
      prefer: "return=representation",
    };

    // 1) To-scale library asset (mesh filled in later by the worker).
    const assetRes = await fetch(`${url}/rest/v1/furniture_assets`, {
      method: "POST",
      headers: writeHeaders,
      body: JSON.stringify({
        owner_id: userId,
        name,
        width_m: widthM,
        depth_m: depthM,
        height_m: heightM,
        capture_method: method,
      }),
    });
    if (!assetRes.ok) {
      return upstreamError("furniture_assets", assetRes);
    }
    const asset = (await assetRes.json())?.[0];

    // 2) The capture job (status processing → worker flips to ready).
    const capRes = await fetch(`${url}/rest/v1/furniture_captures`, {
      method: "POST",
      headers: writeHeaders,
      body: JSON.stringify({
        owner_id: userId,
        name,
        method,
        status: "processing",
        photo_count: Math.max(0, Math.trunc(body.photoCount ?? 0)),
        photo_prefix: photoPrefix,
        width_m: widthM,
        depth_m: depthM,
        height_m: heightM,
        furniture_asset_id: asset?.id ?? null,
      }),
    });
    if (!capRes.ok) {
      return upstreamError("furniture_captures", capRes);
    }
    const cap = (await capRes.json())?.[0];

    return new Response(
      JSON.stringify({
        assetId: asset?.id ?? null,
        captureId: cap?.id ?? null,
        name,
        widthM, depthM, heightM,
        modelUrl: null,
        status: "processing",
      }),
      { status: 200, headers: jsonHeaders() },
    );
  } catch (err) {
    return toResponse(err);
  }
});

function num(v: unknown): number {
  return typeof v === "number" && Number.isFinite(v) && v > 0 ? v : 0;
}

async function upstreamError(table: string, res: Response): Promise<Response> {
  const text = await res.text().catch(() => "");
  return new Response(
    JSON.stringify({ error: `${table} insert failed (${res.status})`, detail: text.slice(0, 300) }),
    { status: 502, headers: jsonHeaders() },
  );
}
