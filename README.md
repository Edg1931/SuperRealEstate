# SuperRealEstate

An augmented-reality toolkit for realtors and buyers. Walk through a property
with an AR/XR headset (or phone) and get instant, contextual information:
auto-measured rooms with live material-cost estimates, plant/vegetation
identification, property lines and neighborhood comps, and virtual furniture
staging.

## Target platforms

Built on **Unity 6 + AR Foundation** so a single C# codebase ships to multiple
ecosystems:

| Platform | Backend | Device | Status |
|---|---|---|---|
| **visionOS** | PolySpatial (ARKit) | Apple Vision Pro | Primary |
| **Android XR** | OpenXR provider | Samsung Galaxy XR | Primary |
| **iOS / Android phone** | ARKit / ARCore | Companion app | Later phase |
| **Desktop (Win/Mac)** | Standalone | Blueprint authoring (design on PC → place on site) | Later phase |

> The INMO Air 3 (and similar HUD-only glasses) are **not** targets: they lack
> depth/world-tracking and cannot do room measurement or world-anchored AR.

## MVP

**Walk into a room → auto-measure → live material-cost estimate.** See
[`ROADMAP.md`](./ROADMAP.md) for the full phased plan and
[`ARCHITECTURE.md`](./ARCHITECTURE.md) for the system design.

## Build prerequisites

- **Unity 6 LTS** with build-support modules for visionOS and/or Android.
  - Verify the exact Unity 6 LTS version, PolySpatial package version, and
    Android XR provider version at setup time — these move fast.
- **Vision Pro builds require a Mac** with Xcode + Apple Silicon.
- **Android XR builds** work from Windows / Linux / macOS.
- A **Supabase** project for the backend (catalog, saved rooms, auth). See
  [`supabase/`](./supabase).

## Repo layout

```
Assets/Scripts/RoomMeasure/    Geometry capture + measurement math (testable)
Assets/Scripts/MaterialCost/   Material catalog + cost estimation (testable)
Assets/Scripts/Staging/        Furniture, vendor catalogs, layouts, blueprint
                               registration + "will it fit?" math (testable)
Assets/Scripts/Renovation/     Editable building model, edit engine, load-bearing
                               advisor, renovation cost (demo + finishes) (testable)
Assets/Scripts/Catalog/        Recognized finish → real product matching (testable)
Assets/Scripts/Landscape/      Plant ID + bulk-material calculators (mulch, etc.) (testable)
Assets/Scripts/UI/             Spatial design tokens, comfort + eye-tracking model
Assets/Scripts/Collaboration/  Shared multi-device sessions + spatial anchors (contracts)
Assets/Scripts/Insights/       AI scene analysis + surface-finish recognition
Assets/Scripts/ARCore/         AR Foundation session/plane/mesh wiring
Assets/Scripts/Services/       Supabase backend client (PostgREST) + contracts
Assets/Scripts/Presentation/   View-models binding domain → UI tokens (testable)
Assets/Scripts/UI/             Spatial panels + phone screens
Assets/Tests/EditMode/         Headless unit tests for the core logic
Packages/manifest.json         Unity package dependencies
supabase/                      Database migrations + seed data + Edge Functions
web/                           Next.js web companion (deploy to Vercel)
docs/                          Setup, design system, pipeline, competitive, features
```

See [`VISION.md`](./VISION.md) for the full product vision and feature pillars.

## Testing

The pure-logic core (`MeasurementService`, `CostEstimator`) is covered by Unity
**EditMode tests** that run headlessly — no device required. On-device behavior
(real depth capture, accuracy vs. tape measure) is verified by building to a
headset; see `ROADMAP.md` → Verification.
