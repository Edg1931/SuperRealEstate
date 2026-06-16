# Roadmap

## Phase 0 — Foundation (scaffolding)
- [x] Repo docs (`README`, `ARCHITECTURE`, `ROADMAP`), Unity `.gitignore`.
- [x] Unity package manifest (AR Foundation, PolySpatial, Android XR, Input System).
- [x] Testable core: `MeasurementService`, `CostEstimator` + EditMode tests.
- [ ] Supabase project: `materials`/`properties`/`rooms`/`room_measurements` + seed.
- [ ] `BackendClient` wired to Supabase.

## Phase 1 — Room measurement (Vision Pro first)
- [x] `FloorOutlineBuilder`: AR plane boundary → world-space outline → `RoomGeometry` (+ tests).
- [x] `RoomMeasureController` (`ARCore` assembly): picks floor/ceiling planes, captures + measures.
- [x] visionOS/PolySpatial setup guide (`docs/VisionOS-Setup.md`).
- [ ] Editor: AR scene wiring (XR Origin, AR Session, Plane Manager, volume camera). *(local)*
- [ ] Spatial UI panel showing live floor area, perimeter, wall area, height, volume.
- [ ] Build to Vision Pro and verify vs. tape measure. *(local, needs Mac + headset)*

## Phase 2 — Material cost estimate
- [ ] `MaterialCatalog` synced from Supabase; per-surface material selection.
- [ ] `CostEstimator` itemized + total in UI; save room + estimate to Supabase.
- [ ] Cross-build to the second headset to prove the one-codebase claim.

## Phase 3 — Plant ID
- [ ] Camera-frame capture → Pl@ntNet via Supabase Edge Function → result card.

## Phase 4 — Property data overlay
- [ ] Comps via RentCast; parcel/property lines via Regrid anchored to location.

## Phase 5 — Virtual staging (Pillar 3)
- [x] Staging data model (`furniture_assets`, `staging_layouts`, `staging_placements`) + RLS.
- [x] Staging domain (`FurnitureAsset`, `Placement`, `StagingLayout`) + `FitChecker` (+ tests).
- [ ] Phone furniture scanning (Object Capture/photogrammetry) → glTF/USDZ in storage.
- [ ] Place/arrange furniture on detected floor; surface fit + clearance warnings in UI.
- [ ] AI auto-staging / declutter (vision model via Edge Function).

## Phase 6 — Shared multi-device sessions (Pillar 4)
- [x] Session data model (`sessions`, `session_participants`) + RLS + participant helper.
- [x] Collaboration contracts: `ISharedSessionService`, `ISpatialAnchorService`, presence model.
- [ ] Spatial-anchor co-location (ARKit shared / ARCore persistent anchors) across devices.
- [ ] Realtime sync of placements + presence (Supabase Realtime); avatars/pointers.

## Phase 7 — AI scene insights (Pillars 1 & 6)
- [x] Insight contracts: `SceneInsight`, `SceneAnalysisRequest`, `ISceneAnalyzer`.
- [x] Edge Function `scene-insights`: Claude multimodal → structured `SceneInsight[]`.
- [x] C# `EdgeFunctionSceneAnalyzer`: posts captured frame → parses `SceneInsight[]`.
- [ ] Frame capture from headset/phone camera wired into the analyzer. *(in-editor)*
- [ ] Insight cards UI; advisory disclaimers; buyer-preference matching.

## Phase 8 — Property data overlays (Pillar 2)
- [ ] Comps (RentCast), parcel/property lines (Regrid), risk/light layers — anchored in space.

## Phase 9 — Phone/tablet companion + deliverables (Pillars 5)
- [ ] Build same project to iOS/Android; companion screens for saved rooms/estimates/staging.
- [ ] Auto-generated walkthrough recap (floor plan, measurements, staged shots, costs, comps).

## Verification

- **Headless (CI-able):** EditMode tests for `MeasurementService` (area/perimeter/
  volume from known geometry) and `CostEstimator` (cost math, waste, itemization).
- **Backend:** verify Supabase schema + seed; confirm `BackendClient` reads the catalog.
- **On-device (local):**
  1. Build to first headset.
  2. Walk into a real room → dimensions match a tape measure within tolerance.
  3. Pick flooring → cost = area × seeded price × waste.
  4. Save → room/estimate persists in Supabase.
  5. Build to second headset and repeat.

## Open prerequisites
- Mac with Xcode + Apple Silicon? (gates Vision Pro builds → decides first device)
- Unity 6 installed with visionOS + Android build modules.
- Confirm current package versions (Unity 6 LTS, PolySpatial, Android XR) at setup.
