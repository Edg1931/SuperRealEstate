# Shipping to Vision Pro & Android XR

What it takes to go from "tested engine + scaffolds" to **installable apps** on
Apple Vision Pro and Samsung Galaxy XR (Android XR) — plus the PC/web app that
stages a space from a blueprint. Both headsets share the same Unity project; the
difference is a thin per-platform adapter + each store's pipeline.

## Where we are vs. a shippable app

- **Have:** the cross-platform engine (measurement, cost, staging, renovation,
  finishes, blueprint registration, co-location, voice, builder/MEP, property
  data — all unit-tested), the Supabase backend + hardened Edge Functions, the
  payload contract, the AR render + input scaffolds, and the web companion.
- **Missing for a shippable app:** an **assembled AR scene**, the **per-platform
  adapters** wired to real input/rendering, **real assets** (prefabs/materials/UI),
  the **capture + design** surfaces, **auth/onboarding**, and **store submission**.

## Milestones (engine is shared; only adapters differ)

| # | Milestone | Platform | Notes |
|---|---|---|---|
| **M1** | Assemble the AR scene + a composition root (press Play → measure a room) | both | XR Origin + AR Session + Plane/Mesh Manager + our controllers, wired by one `RealEstateApp` root. |
| **M2** | Android XR build to Galaxy XR | Android XR | Enable the OpenXR Android XR provider + features (eye-gaze, hand, passthrough, depth, anchors); map eye-gaze pose + pinch → `SpatialPointerInput`; build `.aab`. (`docs/AndroidXR-Setup.md`) |
| **M3** | visionOS build to Vision Pro | visionOS | PolySpatial + volume camera; RealityKit hover/`SpatialTapGesture` → `SpatialPointerInput`; build → Xcode → sign → run. (`docs/VisionOS-Setup.md`) |
| **M4** | Platform adapters | both | Input: `SpatialPointerInput` + `PointerPoseDriver` (origin follows OpenXR eye-gaze / visionOS pointer / phone camera via Input System). `IPortalRenderer` → `WallPortalRenderer` (aperture-quad compositing; supply stencil shader + revealed-capture materials). `ISpatialAnchorService` → `ArCloudAnchorService` (real AF6 tracked anchors + device-aware provider selection via `AnchorProviderSelector`); remaining seam is cross-device **Cloud Anchor hosting** (ARCore Extensions / OpenXR persistence — `HostCloudAsync`/`ResolveCloudAsync`). `ISharedSessionService` → `SupabaseSharedSessionService` (real REST CRUD for own + vendor-catalog placements + invite-code join + `SharedSessionSync` polling). `SupabaseRealtimeChannel` adds the **Supabase Realtime** WebSocket (low-latency `postgres_changes` + presence broadcast) as an additive layer — frame parsing is unit-tested (`RealtimeFrameParser`); verify the live Phoenix wire format on device. |
| **M5** | Wire the data | both | Inject Supabase URL + anon key; `SupabaseBackendClient` / `SupabaseProjectStore` / the Edge-Function clients. |
| **M6** | Real assets | both | Furniture prefabs/materials (swap `StagedSceneRenderer`'s placeholders), UI prefabs for the design-system components, overlay label visuals. |
| **M7** | Capture + design surfaces | both + web | On-device room scan (CubiCasa-style) or RoomPlan import; the **PC/web design surface** that stages from a blueprint and writes the locked payload (`docs/PROJECT-PAYLOAD-CONTRACT.md`). |
| **M8** | Productionization | all | Supabase **Auth** (app + web), onboarding + camera/scan **consent**, loading/empty/error states (built — wire them), settings, telemetry, rate limiting. |
| **M9** | Store submission | both | See below. |

## Store / compliance

**Apple Vision Pro** — Apple Developer Program ($99/yr); App Store Connect +
TestFlight for beta; **App Review**: camera-usage purpose strings, privacy
nutrition labels, no private APIs, hand/eye data stays on-device (don't transmit
gaze), spatial-computing HIG. Build path: Unity → Xcode → sign → archive →
upload.

**Android XR (Galaxy XR)** — Google Play Console ($25 one-time); Play app with an
**Android XR** listing; permissions (camera) + Data Safety form; policy review.
Build path: Unity → `.aab` → Play.

## Feature priorities — a coherent MVP first

Ship a focused first release, then expand. **MVP (realtor + renovation client):**
1. **Room measure → material cost** (the keystone; make it run on Galaxy XR).
2. **Surface finish recognition + "shop this look"** (the wow moment).
3. **Virtual staging** — own + vendor furniture, with the fit check.
4. **Blueprint → AR auto-staging** — the PC design surface + walk-it-on-site
   (the Microsoft-Layout differentiator).
5. **Before/after renovation** incl. the wall-removal portal.

**Fast-follow:** voice agent · shared co-located sessions · plant ID + landscape
calculators · builder/MEP overlays · property data (comps/parcels/risk).

This MVP is demoable, valuable to both audiences, and exercises every core
system we've built.

## The immediate next code step

**M1 — the composition root + a runnable scene.** A `RealEstateApp` MonoBehaviour
that takes config (Supabase URL/anon key) + scene references (AR managers, our
controllers) and wires them: builds the backend clients, configures
`ProjectStagingController` with the store + `StagedSceneRenderer`, hooks
`SpatialPointerInput` to the gaze model, and applies `XrSessionBootstrap` feature
gates. With it, opening the scene and pressing Play (or building to Galaxy XR)
runs the measurement MVP end to end.
