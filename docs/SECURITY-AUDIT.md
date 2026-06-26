# Security & RLS audit

Review of all migrations (`0001`–`0009`) and Edge Functions. Severity: 🔴 high ·
🟠 medium · 🟡 low · ⚪ info/pass.

## Findings

### 🔴 Open session self-join — FIXED (`0009`)
`0002`'s `participants_self_insert` only checked `user_id = auth.uid()`, so any
authenticated user could insert themselves into **any** session by id and then
read its layout/furniture (via the participant-based policies) and edit
placements. Session ids are guessable/enumerable → unauthorized access to a
private design session.
**Fix (`0009_security_hardening.sql`):** dropped the open self-insert; added
`session_invites` (host-issued codes) and a `SECURITY DEFINER join_session(code)`
function that adds the caller only on a valid, unexpired invite. Hosts may add
participants directly; everyone else must redeem a code.

### 🟠 Edge Function abuse / cost — ADDRESSED (defense-in-depth)
All functions (`scene-insights`, `plant-id`, `voice-agent`, `comps`, `parcels`,
`cubicasa-import`, `matterport-import`, `valuation`) call paid third-party APIs.
Now hardened via `supabase/functions/_shared/guard.ts`: each asserts an
`Authorization` Bearer token (`requireAuth`) on top of Supabase `verify_jwt`,
caps the body size (`readJson`), and routes errors through `toResponse`.
**Still recommended:** per-user **rate limiting** (not expressible in-function;
do it at the gateway / a counter table) — keep `verify_jwt` ON.

### 🟡 Edge Function input validation — ADDRESSED
Guards added: `capBase64` (image ≤ ~6 MB), `capString` (transcript ≤ 4000),
`validateLatLng` ([-90,90]/[-180,180]). Applied across the functions.

### 🟡 CORS `Access-Control-Allow-Origin: *`
Fine for the headset/native clients; if the **web companion** calls functions
directly from the browser, tighten to the app's origin(s).

### ⚪ Public catalog read (`0006`) — intended
`materials` / `vendors` / `vendor_catalog_items` are readable by `anon` so the
web companion's catalog loads without login. This exposes only curated catalog
+ pricing (marketing data). Acceptable; revisit if pricing becomes sensitive.

### ⚪ Owner-scoped RLS — PASS
`properties`, `rooms`, `room_estimate_items`, `furniture_assets`,
`staging_layouts`/`placements`, `blueprints`, `building_models`,
`renovation_plans`, `parcels`, `building_systems`, `renovation_projects` are all
correctly gated to the owner (directly or via the parent record). Shared reads
(furniture/layout/blueprint in a session) are gated by `is_session_participant`,
which — now that joining is invite-gated — is sound.

### ⚪ SECURITY DEFINER functions — PASS
`is_session_participant` and the new `join_session` both set
`search_path = public` (prevents search-path hijacking). `join_session` is the
only privileged write path and validates the invite before inserting.

### ⚪ Secrets — PASS
The Anthropic/provider keys live only as Edge Function secrets; clients use the
public anon key (safe by design under RLS). No service-role key ships to a
client.

## Recommended follow-ups

- Enforce the 🟠/🟡 items in the Edge Functions (auth assertion, rate limit,
  size caps, lat/lng validation).
- Invite hygiene: default `expires_at`, one-time or rotating codes, and a host
  "revoke all" action.
- Add lightweight **audit logging** for session joins and destructive deletes.
- Re-run Supabase **advisors** (`get_advisors`) on the live project after
  applying migrations, and review any flagged policies.
- Periodic re-audit as new tables/functions land (this doc is the baseline).
