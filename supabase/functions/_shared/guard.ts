// Shared Edge Function guards (defense-in-depth hardening).
//
// Centralizes CORS, auth assertion, payload-size caps, and input validation so
// every function applies the same checks. Supabase `verify_jwt` stays ON at the
// platform level; requireAuth() here gives a clear, early 401 and a single place
// to evolve the policy. See docs/SECURITY-AUDIT.md (🟠/🟡 items).

// Shared CORS headers. Kept "*" (headset/native clients); tighten to the web
// companion's origin(s) here if the browser ever calls functions directly.
export function corsHeaders(): HeadersInit {
  return {
    "Access-Control-Allow-Origin": "*",
    "Access-Control-Allow-Headers": "authorization, content-type, apikey",
    "Access-Control-Allow-Methods": "POST, OPTIONS",
  };
}

export function jsonHeaders(): HeadersInit {
  return { ...corsHeaders(), "content-type": "application/json" };
}

// A typed error carrying an HTTP status, so handlers can route it through
// toResponse() and everything else falls through to a 500.
export class GuardError extends Error {
  readonly status: number;
  constructor(status: number, message: string) {
    super(message);
    this.name = "GuardError";
    this.status = status;
  }
}

// Asserts an Authorization Bearer header is present and non-empty. Defense in
// depth on top of Supabase verify_jwt; throws GuardError(401) when missing.
export function requireAuth(req: Request): void {
  const header = req.headers.get("authorization") ?? "";
  const match = /^Bearer\s+(.+)$/i.exec(header.trim());
  if (!match || match[1].trim().length === 0) {
    throw new GuardError(401, "Missing or empty Authorization Bearer token");
  }
}

// Reads the request body, rejecting (GuardError 413) if the declared
// Content-Length or the actual body exceeds maxBytes, and parsing the result as
// JSON (GuardError 400 on invalid JSON). Returns the parsed value.
export async function readJson<T = unknown>(req: Request, maxBytes: number): Promise<T> {
  const declared = req.headers.get("content-length");
  if (declared != null) {
    const n = Number(declared);
    if (Number.isFinite(n) && n > maxBytes) {
      throw new GuardError(413, `Payload too large (max ${maxBytes} bytes)`);
    }
  }

  const buf = await req.arrayBuffer();
  if (buf.byteLength > maxBytes) {
    throw new GuardError(413, `Payload too large (max ${maxBytes} bytes)`);
  }

  const text = new TextDecoder().decode(buf);
  if (text.trim().length === 0) {
    throw new GuardError(400, "Invalid JSON body");
  }
  try {
    return JSON.parse(text) as T;
  } catch {
    throw new GuardError(400, "Invalid JSON body");
  }
}

// GuardError(400) if a string exceeds maxLen characters. No-op when absent.
export function capString(value: unknown, maxLen: number, name: string): void {
  if (typeof value === "string" && value.length > maxLen) {
    throw new GuardError(400, `${name} exceeds maximum length of ${maxLen} characters`);
  }
}

// GuardError(400) if a base64 string decodes to more than maxBytes. Uses the
// length * 3/4 estimate (minus padding) to avoid decoding the whole payload.
export function capBase64(value: unknown, maxBytes: number, name: string): void {
  if (typeof value !== "string" || value.length === 0) return;
  let len = value.length;
  if (value.endsWith("==")) len -= 2;
  else if (value.endsWith("=")) len -= 1;
  const decodedBytes = Math.floor((len * 3) / 4);
  if (decodedBytes > maxBytes) {
    throw new GuardError(400, `${name} exceeds maximum size of ${maxBytes} bytes`);
  }
}

// GuardError(400) if lat/lng are present but out of [-90,90] / [-180,180].
export function validateLatLng(lat: unknown, lng: unknown): void {
  if (lat != null && (typeof lat !== "number" || !Number.isFinite(lat) || lat < -90 || lat > 90)) {
    throw new GuardError(400, "lat must be a number in [-90, 90]");
  }
  if (lng != null && (typeof lng !== "number" || !Number.isFinite(lng) || lng < -180 || lng > 180)) {
    throw new GuardError(400, "lng must be a number in [-180, 180]");
  }
}

// Maps a GuardError to its status JSON; anything else becomes a 500.
export function toResponse(err: unknown): Response {
  if (err instanceof GuardError) {
    return new Response(JSON.stringify({ error: err.message }), {
      status: err.status,
      headers: jsonHeaders(),
    });
  }
  const message = err instanceof Error ? err.message : String(err);
  return new Response(JSON.stringify({ error: message }), {
    status: 500,
    headers: jsonHeaders(),
  });
}
