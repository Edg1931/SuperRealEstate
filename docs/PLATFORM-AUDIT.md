# Deep platform audit — graded scorecard + roadmap

Independent deep audits of the **web app**, the **Unity AR apps** (Vision Pro /
Galaxy XR / phone), and the **backend/security**, cross-checked. Honest grades —
the goal is to find what to fix, not to flatter. (Audited at session date; grades
move as the roadmap below is executed.)

## Master scorecard

| Platform / area | Grade | One-line |
|---|---|---|
| **Web (PC/mobile)** | **C+/B−** | Strong design surface + clean data layer; no global nav, theme-token drift, one-way publish |
| **AR — Vision Pro** | **C+** | Correct gaze-privacy + design system; scene not assembled, no PolySpatial pointer wired |
| **AR — Galaxy XR** | **B−** | Best-positioned (real eye-gaze ray); needs in-Editor OpenXR setup + haptics |
| **AR — Phone** | **B** | Capability gating is genuinely correct; needs phone-native 2D screens |
| **Backend / data** | **C+/B−** | Excellent RLS + guardrails; anon-key AI calls + unverified import APIs + no rate limiting |
| **AR engine/logic (pure)** | **A−** | Math, gating, parsing, tokens — test-backed, genuinely excellent |

**Headline:** the *foundations are A-grade* (design system, RLS, capability model,
pure logic) but the *runnable products are C+/B−* because the AR scene isn't
assembled, navigation isn't wired to actions, the web app lacks a global nav +
has token drift, and there are concrete defects (a compile break — now fixed; an
anon-key AI-auth gap; unverified third-party APIs).

---

## Per-area sub-grades

**Web** — Visual consistency C+ · Functionality B− · UI/UX & nav C · Polish C+ · Innovation-readiness B.
**AR** — Functionality/wiring C+ · Spatial UI/UX & nav B− · Polish C · Innovation-readiness A−.
**Backend** — Data model & RLS B+ · Edge Functions & validation B− · Security/secrets B · Production-readiness D+ · AI guardrails B.

---

## What's genuinely strong (keep)

- **Design system** (`UI/DesignTokens`, `SpatialComfort`, `PaletteAdapt`): angular
  type, vergence comfort, no-Midas-touch, ambient-luminance adapt. First-class.
- **Gaze privacy**: never reads raw gaze; reacts to the resolved hit. App-Review-safe.
- **Capability gating** (`Platform/*`): every feature resolved per device with a reason.
- **RLS**: enabled + owner-scoped on every table; 0009 invite-hardening is well-executed.
- **No secrets in clients**; AI fair-housing/advisory guardrails enforced in prompts + schema.
- **Pure/tested core**: ~41 EditMode tests; clean web serialization layer kept in sync with C#.

---

## P0 — blocking / do first

1. **[AR] Compile break — FIXED.** `XrCapabilities.For` → `XrCapabilityProfiles.For`
   in `RealEstateApp` + `RadialToolMenuView`. (Add a PlayMode/compile check so the
   ARCore glue is covered — the EditMode asmdef doesn't reference ARCore.)
2. **[Backend] Edge-Function AI clients use the anon key, not the user JWT.** No
   per-user rate limiting / cost attribution is possible. Add `SetAccessToken` to
   `EdgeFunctionSceneAnalyzer/PlantIdentifier/VoiceAgent`, `EdgeFunctionPropertyData`;
   propagate the signed-in token from `RealEstateApp.SetAccessToken`.
3. **[Backend] Per-user AI rate limit + cost ceiling** (needs #2 first): a counter
   table keyed on `auth.uid()`, checked in `guard.ts`. Unbounded Anthropic spend today.
4. **[Web] Global navigation.** Put a real header nav (Home/Catalog/Design/Projects +
   auth) in `layout.tsx`; remove every per-page `← Home`.
5. **[Web] Theme tokenization** (the flagged color/transparency issue): consolidate
   the 3 un-tokenized blacks + ~7 white-overlay alphas into tokens; give framed
   surfaces one consistent treatment; fix the inverted h2>h1 type scale.
6. **[Web] Touch drawing on the design canvas** — currently mouse-only; broken on
   phones despite the README promising phone drawing.
7. **[Backend] Smoke-test the Claude calls** (SDK 0.69.0 + `output_config.format`);
   make failures log loudly instead of returning empty silently.

## P1 — soon

- ✅ **[AR] Navigation wired** — `RadialToolMenuView.summonAction` + `ToolMenuActionBridge`
  route the menu to `SceneAppActions`; gaze+pinch now acts, not just voice.
- ✅ **[AR] `StageFurnitureAsync` + `ShowCompsAsync` implemented** (real staging +
  comp overlays via the existing renderers).
- ✅ **[AR] Reduce-motion, commit audio/haptic hook, collider strip** shipped.
- **[AR] Platform STT/TTS** adapters feeding `VoiceCommandController`.
- **[AR] Assemble the AR scene** for one target (XR Origin + AR Session + input + our MonoBehaviours).
- **[Web] `/projects/[id]` detail** (read-only plan render) → unblocks share links,
  presentation mode, WebGL preview, billing. **Project name field** before publish.
  Consume `useAuth` in `/design`. EnvNotice on all data pages.
- **[Backend] Verify/lock CubiCasa + Matterport** mappings (gate behind a flag);
  confirm Regrid v1/v2 auth. Structured logging + request IDs + audit logging.

## P2 — later

- **[AR]** reduce-motion + haptic/audio confirms; environment-depth occlusion +
  portal stencil compositing; the "Spatial Glass" blur shader; strip placeholder
  colliders; `Destroy` vs `DestroyImmediate`; real `location` in voice context.
- **[Web]** catalog search/filter; loading skeletons; tests for `lib/*`; cost rollup.
- **[Backend]** idempotency on imports; pagination/limits; tighten CORS; `get_advisors`.

---

## Wave accounting integration (first business priority)

Greenfield — no billing model exists today. The business bills via **Wave** and
wants it as the source for AI insights. Recommended shape (mirrors the existing
secret-stays-server-side Edge-Function pattern):

**`0010_accounting.sql` (RLS owner-scoped):**
- `wave_connections` — `user_id, business_id, access_token, refresh_token,
  token_expires_at, scopes`. **No `to authenticated` select policy** — tokens are
  readable only by service-role/Edge Functions (or Vault-encrypted). Crown jewels.
- `wave_invoices` (+ optional `wave_customers`, `wave_transactions`) — cached
  snapshots (`wave_id, status, total, currency, issued_at, customer_name,
  property_id?`). **This is the table AI insights read from.** RLS via connection ownership.
- `wave_webhook_events` — raw events, idempotent on Wave's event id, signature-verified flag.

**Edge Functions (Wave secrets server-side):**
- `wave-oauth-callback` — code → tokens; writes `wave_connections` via service-role only.
- `wave-sync` — refresh tokens + pull invoices/customers via Wave GraphQL; upsert
  cache; **authenticate the caller with the user JWT** (the P0 #2 fix) so it scopes
  to that user's connection.
- `wave-webhook` — public, but **HMAC-verify Wave's signature**, dedupe, re-fetch
  authoritative state, upsert.
- `accounting-insights` (or extend `scene-insights`) — reads the cache, Claude
  summary with the **same advisory guardrails** ("not accounting advice"; no
  fair-housing-adjacent inferences from customer data).

**UI slot (web):** a "Billing" section on the new `/projects/[id]` page — invoice
status pills (reuse `.pill-positive/.pill-accent/.pill-muted`) + an AI insight
blurb. **Do P0 #2 first so the Wave functions inherit correct per-user auth.**

---

## Top innovative features (grounded — pieces already exist)

1. **AI staging director over voice** — "stage this as a mid-century family room
   under $8k" → Voice → `AutoStager` → `CostEstimator` (only the action is stubbed).
2. **Comps-in-place** — turn the `ShowComps` stub into floating neighborhood cards
   (CompsOverlayBuilder + property-data are built).
3. **Sun-path / daylight sim** — `Construction/SunPath.cs` exists; "see this kitchen
   at 5pm in July."
4. **Instant finish-swap shopping** — `RecognizeFinish` already returns brand/cost;
   retexture live + buy link.
5. **Shared read-only project links + client presentation mode** (web) — reuse the
   pure SVG plan render + a share token.
6. **Live diminished reality** — extend the wall portal from scanned-capture to
   real-time clutter removal.
7. **Wave-driven margin insights** — per-project profitability surfaced in-app.
