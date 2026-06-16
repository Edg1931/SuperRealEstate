# Architecture

## Goals

- One codebase → visionOS (Vision Pro), Android XR (Galaxy XR), and phones.
- Depth-first: every flagship feature depends on real spatial sensing.
- Keep third-party API keys off the device.
- Keep the cost/measurement math pure and unit-testable, independent of any AR
  backend.

## Layers

```
Unity 6 project (single codebase)
├── AR core layer            AR Foundation: plane / mesh / raycast / anchors
│   ├── visionOS             PolySpatial (Vision Pro)
│   ├── Android XR           OpenXR provider (Galaxy XR)
│   └── Phone AR             iOS / Android companion builds
├── Feature modules
│   ├── RoomMeasure          geometry capture → area / perimeter / volume   [MVP]
│   ├── MaterialCost         catalog lookup × measured area + waste          [MVP]
│   ├── PlantID              camera frame → plant-ID API                     [later]
│   ├── PropertyData         comps + parcel / property lines                 [later]
│   └── VirtualStaging       place 3D furniture models                       [later]
├── Services
│   ├── BackendClient        REST / Realtime to Supabase
│   └── ApiClients           Pl@ntNet / RentCast / Regrid wrappers
└── UI                       spatial panels + phone screens
```

## Assembly boundaries (why it matters)

The measurement and cost logic live in their own assemblies (`asmdef`) that
depend only on `UnityEngine` math types — **not** on AR Foundation. This lets
the EditMode test assembly reference and exercise them headlessly, even on a
machine with no AR packages or headset. AR-specific wiring lives in a separate
`ARCore` assembly that depends on AR Foundation.

```
RoomMeasure (asmdef, UnityEngine only)  ─┐
MaterialCost (asmdef, UnityEngine only) ─┼─► Tests (EditMode asmdef)
                                          │
ARCore (asmdef, refs AR Foundation) ──────┘ references RoomMeasure/MaterialCost
```

## Data flow — MVP

1. AR core detects floor + wall planes / scene mesh (AR Foundation).
2. `ARCore` controller produces a `RoomGeometry` (floor polygon + ceiling height).
3. `MeasurementService` computes floor area, perimeter, wall area, volume.
4. User picks a `Material` per surface from the `MaterialCatalog` (synced from
   Supabase).
5. `CostEstimator` computes itemized + total cost (`area × price × (1 + waste)`).
6. Results render in a spatial UI panel; the room + estimate persist to Supabase.

## Backend (Supabase)

- **Auth**: Supabase Auth for realtor accounts.
- **Tables**: `materials`, `properties`, `rooms`, `room_measurements` (see
  `supabase/migrations`).
- **Edge Functions**: proxy third-party APIs (Pl@ntNet, RentCast, Regrid) so
  their keys never ship in the client build.

## Data sources & constraints

| Need | Reality | Approach |
|---|---|---|
| Material price/sq ft | No official Lowe's/Home Depot API; scraping = ToS/legal risk | Seeded, editable Supabase catalog; affiliate/3rd-party feed later. **No scraping.** |
| Comps / valuations | MLS needs a license; 3rd-party APIs exist | RentCast (free tier) now; clean seam for RESO/MLS later |
| Parcel / property lines | Commercial parcel polygons | Regrid parcel API |
| Plant ID | Good APIs exist | Pl@ntNet (free) now; Plant.id/Kindwise paid upgrade |
