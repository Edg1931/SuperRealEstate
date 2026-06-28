# Platform status — where each app is, and what to improve

A per-platform breakdown of SuperRealEstate: the **two AR headsets** (Apple Vision
Pro, Samsung Galaxy XR), the **PC/web** companion, and the **mobile** companion.
All four come from **two codebases**: one Unity 6 + AR Foundation project (the
three native apps) and one Next.js app (`web/`, the PC/mobile browser companion).
"Built" = the logic/wiring exists in code and (where pure) is unit-tested;
"in-Editor / on-device" = needs Unity assembly + a device build only you can do.

Legend: ✅ built in code · 🟡 partial / needs art or device wiring · ⛔ not started

---

## 1. Apple Vision Pro — visionOS (Unity + PolySpatial)

**How it's produced:** Unity → PolySpatial (visionOS) → Xcode → sign → Vision Pro.
Requires a Mac + Xcode. One shared codebase with Android XR.

**Feature set**
- ✅ Room measure → material cost (RoomMeasure + MaterialCost, tested)
- ✅ Plant ID, surface-finish recognition (camera frame → Edge Functions)
- ✅ Landscape/bulk-material calculators (mulch/concrete/sod/pavers/fence/paint/flooring)
- ✅ Voice agent end-to-end (transcript → agent → dispatcher → action)
- ✅ Wall-removal **portal** (video-passthrough compositing — works here)
- ✅ Virtual staging (own + vendor furniture), blueprint→AR 1:1 walk
- ✅ Shared multi-device sessions + **presence avatars** (REST poll + Realtime)
- ✅ Property overlays (comps/parcels/valuation), builder/MEP overlays
- ✅ Onboarding + consent + settings + telemetry (consent-gated)
- ✅ Spatial UI (frosted panels, hover feedback, billboard, comfort placement)
- 🟡 Input: pointer + `SpatialTapGesture` (correct visionOS pattern — never reads
  raw gaze); needs the PolySpatial pointer wired to `SpatialPointerInput` in-Editor
- 🟡 Co-location across devices needs ARKit↔ARCore bridging (Cloud Anchors seam)
- 🟡 Real furniture/UI art (loaders ready; prefabs not authored)

**Strengths here:** highest display PPD → text/frosted glass look their best;
system-private gaze + pinch is the most natural selection; strong occlusion.

**Improve on Vision Pro**
- Add a **PolySpatial hover effect** on real prefabs so the system glow and our
  `SpatialHoverFeedback` agree.
- Use **bounded vs unbounded volumes** deliberately: unbounded for world overlays
  (measure, portals), a bounded volume for the staging "dollhouse" preview.
- Honor **Digital Crown** immersion + Reduce Motion; wire Apple's **RoomPlan** as a
  first-class scan source (better than generic planes on this device).
- App Review: keep camera-frame use consent-gated + purpose-stringed; never
  transmit gaze; ship Persona-aware presence later.

---

## 2. Samsung Galaxy XR — Android XR (Unity + OpenXR)

**How it's produced:** Unity → OpenXR Android XR provider → `.aab` → device/emulator.
Builds from any OS (no Mac needed). **This is the lead headset (Project Aura class).**

**Feature set:** same shared feature set as Vision Pro (all ✅ above), plus:
- ✅ **Eye-gaze ray** + pinch via `PointerPoseDriver` (OpenXR eye-gaze pose) — we
  get an actual gaze ray here (visionOS hides it), so our hover feedback is what
  makes targeting legible
- ✅ OpenXR persistent anchors path for co-location (Android XR ↔ Android XR)
- 🟡 Enable the OpenXR feature group in-Editor (eye-gaze, hand, passthrough, depth,
  anchors) + interaction profiles; map pinch → Select
- 🟡 Hand-tracking interactions beyond pinch (direct touch on near panels)

**Strengths here:** depth + scene mesh are first-class; a real gaze ray means rich
hover; standalone compute; the most open path for cross-device Cloud Anchors.

**Improve on Galaxy XR**
- **Calibration-tolerant targets:** bump `buttonHeightM`/`Gap` if on-device eye
  tracking is coarser than the 2°/1° floors; offer a one-tap recalibration.
- **Passthrough contrast:** Galaxy XR passthrough is brighter/noisier — wire the
  **ambient-luminance palette adapt** (token exists) and lean on the edge halo.
- **Near-field direct touch:** for panels within arm's reach, allow poke/pinch
  directly (hand interaction profile) in addition to gaze+pinch.
- **Controller fallback** profile for users who prefer it.
- Add **haptic/audio commit** confirmation on pinch.

---

## 3. PC / Web — the design + account companion (Next.js, `web/`)

**How it's produced:** `web/` → Vercel (Root Directory = `web`). Deploy-ready.

**Feature set**
- ✅ **Design surface** (`/design`): top-down plan editor — draw walls, place
  furniture (vendor catalog or generic), rotate/scale, live payload preview,
  Copy/Download, and **publish** the locked project to Supabase (magic-link auth)
- ✅ **Renovation-edit authoring**: change wall finish, mark walls for removal,
  ceiling-height changes → `renovation_plans`
- ✅ **My projects** (`/projects`): list your published projects w/ status + artifacts
- ✅ **Catalog** (`/catalog`): finishes with brand/color/price
- ✅ Deploy-readiness: `vercel.json`, env banner, metadata, 404, import-safe client
- ⛔ No 3D/AR preview in-browser (it's a 2D plan authoring tool by design)
- ⛔ No project **detail/edit** page (list only) or re-open-to-edit
- ⛔ No team/sharing, no billing

**Strengths here:** the **Microsoft-Layout differentiator** lives here — design on a
big screen, walk it 1:1 on the lot. Writes the exact locked payload the AR app reads.

**Improve on PC/Web**
- A **project detail page**: open a published project, see its plan + placements,
  edit and re-publish (round-trip, not just create).
- A lightweight **3D/WebGL preview** (three.js) of the building model + staging so
  desktop users see the room before donning a headset.
- **CubiCasa/Matterport upload** UI to seed a building model from a real scan.
- **Org/sharing**: invite a client to view a project; shared-session links.
- Keyboard-first authoring (snapping, undo/redo, copy-paste rooms).

---

## 4. Mobile — phone & tablet companion (Unity AR Foundation + responsive web)

**How it's produced:** two ways in — (a) the same Unity project built to iOS
(ARKit) / Android (ARCore) for on-the-go AR; (b) the responsive `web/` app in a
mobile browser for design/catalog/projects.

**Feature set**
- ✅ AR core works on phones (plane detection, measure, plant/finish via camera)
- ✅ Capability profiles already gate phone features (`XrCapabilities.For(IosPhone/
  AndroidPhone)`) — wall-removal portals correctly **disabled** (screen, not
  passthrough headset) with a reason surfaced to the user
- ✅ Input adapter: camera-as-pointer + screen tap (`SpatialPointerInput` +
  `PointerPoseDriver` fallback source = camera)
- ✅ Web companion is responsive (cards/grids) for design + viewing on a tablet
- 🟡 Phone-optimized **2D screens** (vs spatial panels) for viewing saved rooms,
  staging on a flat AR view — not yet authored
- 🟡 ARCore **Cloud Anchors** is the cross-platform co-location path (phone joins a
  headset session) — service seam built, SDK package not added
- ⛔ Native mobile app store presence (could ship the Unity build, or wrap web)

**Strengths here:** widest reach; a buyer/spouse joins the agent's session from a
phone; tablet is a great design surface for the web tool.

**Improve on Mobile**
- **Phone-native UI**: the spatial panels are headset-shaped; build flat 2D HUD
  variants for phone AR (bottom-sheet tools, tap targets ≥44 pt).
- Ship **ARCore Cloud Anchors** so a phone truly co-locates into a headset session
  (the marquee "agent + buyer + spouse see the same furniture" moment on mixed
  devices).
- **Handoff**: start on phone (browse/preview), continue on headset (walk it).
- Consider a **PWA** of the web app for install-to-home-screen without an app store.

---

## Cross-cutting priorities (move every platform forward)

1. **Stand up the backend** (apply migrations, deploy Edge Functions, set
   `ANTHROPIC_API_KEY` + RentCast/Regrid keys) — unblocks AI + data on all apps.
2. **Author the art** (furniture models, TextMeshPro UI, Spatial-Glass shader) —
   the loaders/builders are ready; this is what makes it *beautiful*, not just
   correct.
3. **Cross-device co-location** (Cloud Anchors) — the one feature that makes the
   shared-session story real across Vision Pro + Galaxy XR + phone.
4. **STT/TTS** platform hookup for voice; **per-user rate limiting** at the gateway.
5. **Store readiness** (Apple Developer + App Review; Google Play + Android XR).
