# Furniture capture — photogrammetry, to-scale, for "will it fit"

Let a client photograph their own furniture on a phone, reconstruct a **to-scale**
3D model, store it in their library, and place it in a home during a walkthrough
so they can see it fit before they move. The differentiator vs every consumer
scanner: we keep **real-world metric scale**, because the whole point is fit.

## What the best scanning apps do (and what we take)

| App | Strength | What we adopt |
|---|---|---|
| **Apple Object Capture** (RealityKit `PhotogrammetrySession`) | On-device, high-quality USDZ from a guided photo orbit; automatic object isolation | Our **iOS/visionOS reconstruction backend** (on-device, private, no upload) |
| **Polycam** | Excellent **guided capture** with live coverage feedback; auto crop/ground removal; measurement; library + sharing | The **coverage-guidance coach** + auto ground-plane removal + a shared library |
| **Luma AI** | Photoreal **Gaussian-splat / NeRF**; web viewer + flythrough | A **GaussianSplat** capture method option for hero photoreal pieces |
| **Kiri Engine** | Object **masking / auto-isolation**, featureless-object support, cloud + on-device | Robust **object segmentation** before reconstruction |
| **Scaniverse** | Fast, free, on-device LiDAR + photo; clean meshes | A **LiDAR** fast-path on depth-capable phones |
| **RealityScan** (Epic) | **AR preview while capturing**; alignment confidence | **In-AR live preview** + capture-confidence UI |
| **Matterport** | Dimensioning + room context | We already do rooms; reuse for **fit context** |

## Our innovations (beyond the above)

1. **Metric auto-scale from AR (the killer).** Consumer photogrammetry is
   scale-free — you get a pretty mesh of unknown size. We measure the object's
   real bounding box from AR depth/anchors at capture time, so the model is
   **to-scale in meters**. Without this, "will it fit" is a guess.
2. **"Will it fit through the door?"** `CaptureBounds.FitsThroughOpening` answers
   the question movers actually ask — can this couch be turned/tilted through that
   doorway — not just "does it sit in the room."
3. **Coverage-guided capture coach.** `CaptureCoverage` bins the viewing sphere
   and tells the user exactly where to move next ("go to ~210° and keep it
   centered"), with a live coverage %. Better coverage → better reconstruction.
4. **Capture once, see it on every device.** A captured asset drops into the
   shared session, so the agent's headset, the buyer's phone, and a spouse's
   tablet all see the *real* sofa placed in the room together.
5. **Fit-check the instant it's placed.** The metric bounds feed the existing
   `Staging.FitChecker` — green when it fits with clearance, caution when it's
   tight — right as you set it down.
6. **On-device first, cloud fallback.** iOS/visionOS reconstruct privately
   on-device (Apple Object Capture); Android (no first-party API) uses a cloud
   photogrammetry service behind a Supabase Edge Function. Same capture UX either
   way — only the reconstruction backend differs.

## Architecture

```
Capture (mobile AR)                Reconstruct                 Store + use
─────────────────────             ─────────────               ───────────────────────
CaptureSession  ──photos+poses──▶ IFurnitureCaptureService ──▶ furniture_captures (job)
  CaptureCoverage (guidance)        • Apple ObjectCapture        → furniture_assets (library)
  AR metric bounds                    (iOS/visionOS, on-device)  → Storage (model + photos)
                                     • Cloud photogrammetry       → Staging.FitChecker (fit)
                                       (Android, Edge Function)   → shared session (everyone sees it)
```

### Built now (this commit) — the platform-agnostic brain, tested
- `Capture` assembly (pure, **10 EditMode tests**): `CaptureCoverage` (guided
  coverage + missing-angle coaching), `CaptureBounds` (metric size +
  `FitsThroughOpening`), `CaptureSession` (lifecycle: Idle→Capturing→ReadyToProcess
  →Processing→Ready/Failed), and the `IFurnitureCaptureService` reconstruction
  contract + `CaptureSubmission`/`FurnitureCaptureResult`.
- Data model: migration `0012_furniture_captures` (RLS owner-only job table) +
  the existing `furniture_assets` library (already supports `object_capture` /
  `photogrammetry` / `lidar` / `gaussian_splat`).

### Next (platform-specific — slots into the contract)
- **`FurnitureCaptureController`** (Unity mobile): drives `CaptureSession` from
  `ARCameraFrameProvider` (photos) + the AR camera pose (view angles) + AR depth
  (metric bounds); shows the coverage coach; on ready calls the service.
- **Apple Object Capture plugin** (`PhotogrammetrySession`, like the speech `.mm`)
  → on-device USDZ on iOS/visionOS.
- **Cloud reconstruction Edge Function** (Android/fallback): upload photos to
  Storage → trigger a photogrammetry service → write back `model_url` + bounds →
  flip the job to `ready`.
- **Library + placement UI**: the captured asset in the user's furniture library;
  place it via `Staging` with the live fit check; share into a session.
