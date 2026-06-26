# Platform playbook — Android XR & visionOS mastery

How to make one codebase feel **native** on each runtime. The shared core
(measurement, cost, staging, renovation, finishes, landscape, UI tokens,
presentation, services) is platform-agnostic and tested; this doc is about the
thin per-platform layer and getting it *right*.

## North star

One C# codebase via Unity 6 + AR Foundation/OpenXR. Each platform gets a small
**adapter** that feeds our platform-agnostic models (`XrCapabilities`,
`FeatureAvailability`, `GazeInteractionModel`, `IPortalRenderer`,
`ISpatialAnchorService`). Feature code never branches on device names.

## The nuance that trips everyone up: gaze data

These two platforms expose eye tracking **completely differently**, and getting
this wrong is the #1 cross-platform XR mistake:

- **visionOS: you never get raw gaze.** It's a privacy boundary — the system
  renders the **hover highlight itself** on any entity with a collider +
  `HoverEffectComponent`, and only tells you *which* entity was hit on a
  `SpatialTapGesture` (pinch). You design with colliders + hover effects and
  react on tap.
- **Android XR (OpenXR Eye Gaze Interaction): you get a gaze pose.** You raycast
  it yourself, drive your own hover visuals, and commit on pinch/trigger.

**Our abstraction handles both:** feature code talks to `GazeInteractionModel`.
- visionOS adapter: `GazeEnter` ← RealityKit hover / on-tap entity; `Commit` ←
  `SpatialTapGesture`. (We do *not* read gaze; we let the system do it.)
- Android XR adapter: `GazeEnter` ← eye-gaze raycast hit; `Commit` ← pinch.
Same model, two drivers — and we never violate Apple's privacy model.

## Android XR (Galaxy XR + Project Aura)

**Stack:** Unity Android XR OpenXR provider + AR Foundation + XR Hands.

- **Features (XR Plug-in Management → OpenXR → Android XR):** session/reference
  space, plane detection, depth/scene mesh, anchors (+ persistent/cloud for
  sharing), **Eye Gaze Interaction**, hand tracking, **passthrough composition
  layer**.
- **Input profiles:** eye-gaze + hand (parity with Vision Pro); add a controller
  profile as fallback (Galaxy XR ships controllers; Aura is gaze+hand/phone).
- **Performance (non-negotiable on mobile-class XR):** single-pass instanced
  rendering, **foveated rendering** (eye-tracked on Galaxy XR), dynamic
  resolution, aggressive draw-call/material budgets, GPU instancing for repeated
  catalog items, LODs for scanned meshes, and **Gaussian-splat** for photoreal
  portals (cheaper than dense meshes).
- **Comfort:** 72/90 Hz target, stable reprojection, no head-locked heavy UI
  (our `AnchorMode` enforces this), vignette on fast motion.
- **AI:** Android XR integrates **Gemini**; we can offer a voice agent ("what's
  this tree?", "how much mulch?") that calls our Edge Functions — a natural fit.
- **Aura specifics:** optical see-through + tethered → `XrCapabilities` gates off
  video portals and leans compute on the phone; lighter sensor set, so fall back
  to phone capture for measurement where depth isn't exposed.
- **Packaging:** standard Android `.aab` → Play; test on device + Android XR
  Emulator.

## visionOS (Apple Vision Pro)

**Stack:** Unity PolySpatial (RealityKit-backed) for shared-space MR; or a fully
immersive Metal/CompositorServices path for a custom-rendered full space.

- **Spaces & volumes:** *shared space* (our app coexists with others — bounded
  **volumes** for tool panels) vs. *full space* (exclusive — unbounded for
  world overlays, and required for passthrough compositing like portals). Pick
  per task; default to shared/bounded for the HUD, full/unbounded for the
  walkthrough.
- **Immersion styles:** mixed (passthrough + content) is our default; progressive
  (digital crown dial) and full for focused review.
- **Interaction:** `SpatialTapGesture` (pinch), drag/rotate gestures for staging;
  hover effects for affordance — never read gaze.
- **UI:** attachments/ornaments for panels; honor system materials (glass) so our
  "Spatial Glass" reads native; respect Dynamic Type + the system's depth and
  text-legibility guidance.
- **Performance:** entity/draw budgets, model LODs, occlusion via scene depth.
- **Privacy:** no camera-frame scraping; occlude the real wall with content for
  portals (don't sample passthrough pixels). Scans of occupied homes → consent +
  retention limits.

## Parity matrix (feature → per-platform implementation)

| Feature | visionOS | Android XR headset | Aura glasses | Phone |
|---|---|---|---|---|
| Room measurement | ARKit scene depth | OpenXR depth/mesh | fallback to phone | ARKit/ARCore depth |
| Gaze UI | system hover + tap | eye-gaze raycast + pinch | eye-gaze + phone | touch ray |
| Wall portal | full-space occlude | passthrough composition | ✗ (optical) | screen composite |
| Shared anchors | ARKit world anchors | OpenXR/Cloud anchors | Cloud anchors | Cloud anchors |
| Staging / renovation / cost / finishes / landscape | shared core | shared core | shared core | shared core |

## Implementation order (Android-XR-first, visionOS in parallel)

1. **Shared AR scene** (XR Origin, AR Session, Plane Manager, `RoomMeasureController`)
   — already platform-neutral.
2. **Android XR adapter**: enable provider/features, eye-gaze + pinch → drive
   `GazeInteractionModel`; verify measurement on Galaxy XR.
3. **visionOS adapter**: PolySpatial volume + RealityKit hover/tap → drive the
   same model.
4. **Phone adapter**: touch ray + bottom-sheet UI (design system already maps it).
5. **Portals + shared anchors** per the contracts (`IPortalRenderer`,
   `ISpatialAnchorService`, `SharedAnchorPayload`).

The win: steps 1, 4, and every feature module are shared; only the input/render
adapters differ — and they're small.
