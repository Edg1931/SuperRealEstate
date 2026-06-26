# Builder & construction tools

For builders and their teams (think Trimble Connect / SiteVision / XR10, Procore,
Autodesk BIM) — but consumer-simple and buyer-facing too. Turns a blueprint into
a walkable model on the lot, overlays the mechanicals, and verifies the build.

## The jobs to be done

1. **Blueprint → 3D, on the real lot.** Register a floor plan at 1:1 on the
   build site and *walk it* on the ground; pull the 3D model up out of the plan
   and walk through the framed house before it exists — in relation to the
   **property lines** and **where the sun rises and sets**.
2. **Mechanicals overlay (MEP).** Show electrical, plumbing, HVAC, structural,
   low-voltage, gas, and sewer **runs and fixtures** overlaid on the real
   structure — so a buyer knows where everything is, and the crew confirms it's
   going where the model says.
3. **As-built QA.** Measure what's actually installed and compare to the design;
   flag drift before drywall hides it. Catch **clashes** (the duct through the
   joist) early.
4. **Team coordination.** Everyone shares one anchored model on site (Vision Pro
   / Galaxy XR / phone) with attributed markups.

## What's built (pure, tested)

- `Construction.SystemsModel` + `SystemElement` (runs/fixtures per trade) and
  `SystemMetrics` — **takeoffs**: linear feet of wire/duct/pipe, fixture counts.
- `Construction.ClashDetector` — 3D segment-segment proximity between trades →
  clash points (catch interferences before the wall closes).
- `Construction.AsBuiltVerifier` — designed vs. measured deviation + tolerance.
- `Construction.SetbackChecker` — footprint vs. property lines: inside the lot?
  setback kept? (advisory).
- `Construction.SunPath` — sunrise/sunset azimuth, daylight hours, noon sun
  altitude for a latitude + day — "which way does this house face the morning
  sun?"
- Schema (`0007`): `parcels`, `building_systems`.
- Reuses what we already have: `BlueprintTransform` (1:1 site registration),
  `BuildingModel` (walls/openings → the 3D pulled from the plan),
  `SessionCoLocator`/`AnchorFrame` (shared on-site model), the AR measurement
  pipeline (as-built capture).

## How it flows on a build site

1. Import the plan → `BuildingModel`; place it on the lot with two known points
   (`BlueprintTransform`) → walk the blueprint on the ground at 1:1.
2. "Pull up the model" → extrude walls to height (the `BuildingModel` heights) →
   walk the framed house; toggle systems overlays from `SystemsModel`.
3. Orient with `SunPath` (sun rises NE in summer here) + `SetbackChecker`
   (footprint clears the property lines).
4. During construction, capture installed fixtures → `AsBuiltVerifier` flags
   anything beyond tolerance; `ClashDetector` flags interferences.
5. The whole crew shares one anchored model (`SessionCoLocator`).

## Competitive note

Trimble (Connect/SiteVision/XR10), Procore, and Autodesk BIM own pro AEC
coordination — powerful, expensive, pro-operated, desktop-to-field. The gap we
fill: a **consumer-simple, headset-native** path from a plan to a walkable,
mechanicals-aware model on the lot — usable by a small builder *and* shown to a
buyer — tied to the same staging/finishes/cost/AR stack as the rest of the app.

## Advisory guardrails

Setback/zoning, structural, and code checks are **advisory** — confirm with the
jurisdiction, the engineer of record, and the stamped plans. As-built results are
measurement aids, not certified surveys.
