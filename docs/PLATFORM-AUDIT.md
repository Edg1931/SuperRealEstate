# Platform evaluation — graded scorecard + roadmap (updated)

Re-audit of the **web app**, the **Unity AR apps** (Vision Pro / Galaxy XR /
phone), and the **backend/security**, run as independent audits and
cross-checked. Grades move as fixes land; this reflects the state *after* the
July hardening pass (per-user AI auth + rate limits, theme tokenization, global
nav, storage RLS, capture-prefix validation, has_plan round-trip, the web bug
batch, and the AI Staging Director).

## Master scorecard

| Platform / area | Grade | One-line |
|---|---|---|
| **Web (PC/mobile)** | **B+** | Blueprint-calibrated design surface, sharing/presentation, library + fit checks; publish still non-atomic (stopgap cleanup only), canvas has no pan/zoom |
| **AR — Vision Pro** | **B** | Feature-complete in code incl. on-device Object Capture + speech; grade capped until the scene is assembled and a device build runs |
| **AR — Galaxy XR** | **B+** | Best-positioned: eye-gaze ray, JNI speech, hover feedback; needs the in-Editor OpenXR setup + a device build |
| **AR — Phone** | **B+** | Capture → library → to-scale placement/fit is a real product loop; still no phone-native 2D screens |
| **Backend / data** | **B+** | Per-user AI auth + daily rate limits, RLS everywhere incl. Storage, worker path validated; needs deploy-and-verify + the reconstruction worker |
| **AR engine/logic (pure)** | **A** | ~71 EditMode tests over measurement, fit, capture, calibration, sun, consent, dispatch, the staging director |

**Headline:** the foundations were always A-grade; the products around them have
now caught up to solid B territory. What separates every AR grade from an A is
no longer missing code — it's the user-side steps (assemble the scene per
docs/SCENE-ASSEMBLY-GalaxyXR.md, run device builds, apply migrations 0001–0014 +
deploy functions + set secrets). The web app's remaining gap is workflow depth
(atomic publish, pan/zoom, catalog search), not correctness.

## What's genuinely strong (keep)

- **The to-scale guarantee chain**: blueprint upload → two-click calibration →
  meters everywhere → AR walkthrough at 1:1 → captured furniture with AR-measured
  bounds → FitChecker verdicts. Consumer scanners lose metric truth; we never do.
- **Design system** (`SpatialComfort`, angular type, `PaletteAdapt`, reduce-motion,
  no-Midas-touch gaze model) and **gaze privacy** (never reads raw gaze on visionOS).
- **Capability gating**: every feature resolves per device with a human-readable reason.
- **Security posture**: RLS on every table *and* Storage; SECURITY DEFINER RPCs
  pinned to `auth.uid()` or unguessable tokens; Anthropic key server-side only;
  per-user daily rate limits; worker paths validated against the caller's identity.
- **Pure/tested core with the AI on a leash**: the staging director's LLM output
  is geometry-validated before a single item renders.

## Fixed in this pass (was P0/P1)

- **Cross-user photo exfiltration** via client-named `storagePrefix` (HIGH) +
  **missing Storage RLS** (HIGH) → server-side prefix validation + migration 0013.
- `complete-capture` silently "succeeding" on unknown ids; timing-unsafe secret compare.
- `valuation` 500ing away a good value estimate when the rent fetch threw.
- Door-fit checker treating inches as feet (12× wrong); `has_plan` lost on
  publish (origin placements misclassified in AR — migration 0014); duplicate
  finish edits double-counting cost; publish-once lockout; geometry imports
  landing off-canvas; calibration stranding furniture; silent HEIC upload
  failures; swallowed fetch errors; anonymous 0.8 m squares in client
  presentations; canvas scroll-trap on phones; sub-32px tap targets;
  unconfirmed "Clear all"; nav active state; failed captures shown as "in progress".
- Signed-out capture uploads (would violate Storage RLS) now stay local.

## P1 — next

1. **[Web] Atomic publish**: move the 5-insert publish into one
   `publish_project` SECURITY DEFINER RPC (stopgap delete-on-failure is in).
2. **[Web] Pan/zoom the design canvas** (pinch on phones); catalog search/filter.
3. **[AR] Runtime mesh loader** (glTF) so captured furniture renders as the real
   piece instead of a to-scale box once `model_url` fills in; USDZ loads
   natively on Apple platforms.
4. **[AR] Captured furniture into shared sessions** — placements already carry
   `furniture_asset_id`; resolve + render them for every participant.
5. **[Backend] The reconstruction worker** (RealityCapture/Meshroom/hosted) —
   callback + storage + secret are ready and validated.
6. **[CI] A compile/PlayMode check covering the ARCore glue** (EditMode tests
   can't catch MonoBehaviour wiring breaks; needs `ENABLE_UNITY_CI` + a license).
7. **[Web] Auto-stage on the design surface** — call `stage-director` with the
   traced room + library and drop the plan onto the canvas (the AR voice path
   shipped; the web button is the remaining surface).

## P2 — later

- Spatial-Glass blur shader; environment-depth occlusion + portal stencil
  compositing; loading skeletons; web `lib/*` unit tests (needs a test-runner
  decision); Regrid/CubiCasa/Matterport API verification behind flags; import
  idempotency + pagination; CORS tightening; phone-native 2D screens; real
  device `location` in the voice context; store-submission passes.

## The AI Staging Director (new this pass)

"Stage this room, warm modern" (voice, any headset or phone) →
`stage-director` Edge Function (Claude; strict JSON schema; per-user
rate-limited) proposes a layout from the vendor catalog **plus the client's own
captured furniture, preferred first** → `Staging.StagingDirector` validates
every placement with `FitChecker` (near-misses nudged toward the room center,
impossible ones rejected with reasons) → the staged scene renders, with a
spoken summary including catalog cost. The AI proposes; geometry disposes.

## Remaining innovative-feature bench (grounded — pieces already exist)

1. **Comps-in-place cards** anchored around the property (overlay data flows exist).
2. **Instant finish-swap shopping** — `RecognizeFinish` already returns
   brand/cost; retexture live + buy link.
3. **Live diminished reality** — extend the wall portal from scanned-capture to
   real-time clutter removal.
4. **Budgeted staging** — feed a "$ under 8k" constraint into the staging
   director prompt + validator (cost math already returns).
