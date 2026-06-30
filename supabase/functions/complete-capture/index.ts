// Supabase Edge Function: complete-capture
//
// The reconstruction WORKER callback. An external worker (which polls
// furniture_captures for status='processing', fetches the photos from
// photo_prefix, reconstructs a mesh, and uploads it) calls this to write the
// result back: it flips the job to 'ready' and fills in the model_url/thumbnail
// on both the job and its linked furniture_assets row.
//
// Auth: NOT a user — the worker authenticates with a shared secret
// (CAPTURE_WORKER_SECRET) and writes with the service role (so it can update a
// row it doesn't own). Set both as function secrets.

import { corsHeaders, GuardError, jsonHeaders, readJson, toResponse } from "../_shared/guard.ts";

const MAX_BODY_BYTES = 4 * 1024;

interface RequestBody {
  captureId?: string;
  modelUrl?: string;
  thumbnailUrl?: string;
  status?: string; // 'ready' (default) | 'failed'
  error?: string;
}

Deno.serve(async (req: Request) => {
  if (req.method === "OPTIONS") return new Response("ok", { headers: corsHeaders() });

  try {
    if (req.method !== "POST") {
      return new Response("Method not allowed", { status: 405, headers: corsHeaders() });
    }

    const secret = Deno.env.get("CAPTURE_WORKER_SECRET");
    if (!secret || req.headers.get("x-worker-secret") !== secret) {
      throw new GuardError(401, "Invalid worker credentials");
    }

    const url = Deno.env.get("SUPABASE_URL");
    const serviceKey = Deno.env.get("SUPABASE_SERVICE_ROLE_KEY");
    if (!url || !serviceKey) throw new GuardError(500, "Service credentials not configured");

    const body = await readJson<RequestBody>(req, MAX_BODY_BYTES);
    if (!body.captureId) throw new GuardError(400, "captureId is required");

    const status = body.status === "failed" ? "failed" : "ready";
    const writeHeaders = {
      apikey: serviceKey,
      authorization: `Bearer ${serviceKey}`,
      "content-type": "application/json",
      prefer: "return=representation",
    };

    // Update the job; capture its furniture_asset_id.
    const jobRes = await fetch(`${url}/rest/v1/furniture_captures?id=eq.${encodeURIComponent(body.captureId)}`, {
      method: "PATCH",
      headers: writeHeaders,
      body: JSON.stringify({
        status,
        model_url: body.modelUrl ?? null,
        thumbnail_url: body.thumbnailUrl ?? null,
        error: status === "failed" ? (body.error ?? "reconstruction failed") : null,
        updated_at: new Date().toISOString(),
      }),
    });
    if (!jobRes.ok) {
      const text = await jobRes.text().catch(() => "");
      return new Response(JSON.stringify({ error: `job update failed (${jobRes.status})`, detail: text.slice(0, 300) }),
        { status: 502, headers: jsonHeaders() });
    }
    const job = (await jobRes.json())?.[0];

    // Fill the model onto the library asset, if any and if reconstruction succeeded.
    if (status === "ready" && job?.furniture_asset_id) {
      await fetch(`${url}/rest/v1/furniture_assets?id=eq.${encodeURIComponent(job.furniture_asset_id)}`, {
        method: "PATCH",
        headers: { ...writeHeaders, prefer: "return=minimal" },
        body: JSON.stringify({ model_url: body.modelUrl ?? null, thumbnail_url: body.thumbnailUrl ?? null }),
      });
    }

    return new Response(JSON.stringify({ ok: true, captureId: body.captureId, status }), {
      status: 200,
      headers: jsonHeaders(),
    });
  } catch (err) {
    return toResponse(err);
  }
});
