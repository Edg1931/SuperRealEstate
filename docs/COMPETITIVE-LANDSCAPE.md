# Competitive Landscape (capture → edit → cost → AR)

Grounded research synthesis (2024–2026) for the renovation-visualization and
staging side of SuperRealEstate. Sources are linked inline; vendor pricing
pages often block automated fetch, so exact prices are indicative.

## TL;DR — the wedge is real

No competitor delivers the full loop: **scan an existing home → edit its real
walls/finishes → live renovation cost (materials *and* labor) → walk the result
at 1:1 in AR glasses**, shared across devices, with vendor catalogs. Capture
tools stop at the model; design tools stop at a desktop render; AI tools stop at
a 2D image; the one true 1:1 MR tool (Arkio) makes pros build geometry from
scratch.

## Capture / import — what gives us *editable* geometry?

| Source | Editable walls? | Notes |
|---|---|---|
| **Apple RoomPlan** | **Yes** — parametric, `Codable` walls/doors/windows/openings + objects | Best in-app editable source. LiDAR iPhone/iPad Pro only; ~9×9 m/room; no ceilings; **no finishes**. Edit *after* `StructureBuilder` merge. |
| **CubiCasa** | Yes (2D) | Floor-plan-from-scan → vector/DXF walls; good for plan-grade structure. |
| **Polycam** | Partial | Raw mesh/point-cloud/Gaussian splat is *not* structured; its **2026 Floor Plan Editor** + DXF give editable walls. |
| **Matterport** | **No** | Baked single mesh + point cloud + **read-only** room dimensions (Enterprise Property Intelligence API). Great visual twin + data to *seed* a model; will not hand us editable walls. "Defurnish" = image object-removal, not geometry. |

**Implication:** primary editable-import = RoomPlan / CubiCasa / Polycam-floorplan.
Matterport = visual twin + dimensions to seed. **None capture finishes** — our AI
surface-finish recognition layer supplies those.
Sources: developer.apple.com/augmented-reality/roomplan, developer.apple.com/documentation/roomplan/capturedroom; poly.cam/floor-plans + Polycam Floor Plan Editor (PRNewswire, Mar 2026); matterport.com/features/property-intelligence.

## Design / staging / AI tools

- **3D home design** (Planner 5D, HomeByMe, RoomSketcher, Cedreo, Coohom, Foyr,
  Live Home 3D, Chief Architect, SketchUp): all edit walls; most do finishes.
  Gaps — importing an *arbitrary* real-home scan is unsolved (each uses its own
  iOS scanner or needs CAD/DXF); only SketchUp ingests point clouds (Win-only,
  $819/yr, no wall semantics). Live **renovation** cost is rare (Coohom + Chief
  Architect Premier do material takeoff; both miss labor). Modern **headset**
  walkthrough is essentially absent (HomeByMe VR = tethered Rift/Vive).
- **AI interior redesign** (Spacely, REimagineHome, Collov, IKEA Kreativ,
  Decorilla): output 2D restyled photos; **zero structural wall edits**
  (REimagineHome explicitly *locks* structure). Their "AI furniture finder →
  product + price + buy" validates our shop-the-look idea. IKEA Kreativ is the
  only true capture+AR, but furniture-only, IKEA-SKU-only.
- **AR/headset reno** (Lowe's Style Studio on Vision Pro, Planner5D/HomeByMe/
  Wayfair Decorify, Arkio on Quest 3, Meta "Layout"): decor/retail-led or
  pro-modeling. **Removing a real wall in AR (diminished reality) is
  research-stage, unshipped** — our "ghost a removed wall" must stay honest
  about this. Arkio proves 1:1 human-scale passthrough + teleport works.
Sources: arkio.is/meta-quest-3; fastcompany/retaildive (Lowe's Style Studio);
sciencedirect.com S1364815220303753 (diminished-reality demolition).

## Cost estimation (how to do it "the best way")

- Accuracy = **real quantities** (takeoff / 3D) × **localized unit-cost DB**
  (RSMeans / Kukun, ZIP-level) + **waste + markup + labor**. Labor is the hard
  part — material SKUs price easily; install varies by region/scope/contractor.
- **Live cost in 3D exists at retail/BOM granularity** (Homestyler, Estimero,
  Chief Architect, Houzz Pro AutoMate). **Bid-grade live cost is the unsolved
  middle.** Real accuracy today = competitive bids (Sweeten, BuildZoom, Block).
- Best consumer estimator: **Kukun** (ZIP labor/material, waste/markup,
  contractor-validated). Pro backbone: **STACK + RSMeans**.
- **Design choice:** ship retail-BOM live cost now (we have the measurement +
  `CostEstimator` + `FinishQuantity` core); localize with ZIP cost factors;
  label everything an estimate; offer a "get real bids" handoff. Do **not**
  claim bid-grade accuracy.
Sources: mykukun.com/renovation-data; rsmeans.com; pro.houzz.com (AutoMate);
angi.com (39% of remodels go over budget).

## Permit / code & ADU feasibility (be advisory + narrow)

- **Full automated code compliance for arbitrary remodels is HARD/unsolved** —
  natural-language codes + per-jurisdiction fragmentation. Leaders' moats are
  per-jurisdiction rule DBs (PermitFlow 1,500+ municipalities; GreenLite human-
  in-the-loop; Symbium per-city "Complaw"). Copilots (UpCodes ~93% on Q&A, ICC
  AI Navigator) *assist*, don't certify.
- **Feasible scope for us:** narrow structured checks (egress, ceiling height,
  stair/rail dims), an **LLM code-Q&A copilot with citations + disclaimers**,
  and zoning/setback/lot-coverage **feasibility** flags for limited
  jurisdictions. Never a hard "this violates code" claim.
- **ADU:** address → parcel/setbacks/buildable area is commodity (GIS/Regrid;
  Symbium/ADU Pilot/Site Plan Creator do instant feasibility + rough cost). The
  moat is the jurisdiction rules DB + credible instant cost (site factors force
  everyone to a paid study/site visit). Treat ADU feasibility as advisory.
Sources: symbium.com; upcodes.com/copilot; greenlite.com; arxiv.org/abs/2510.02634;
adupilot.com; siteplancreator.com/adu-feasibility-software.

## What we copy vs. beat

- **Copy:** shop-the-look (recognized finish → product + price + buy), ZIP-level
  cost localization, retail-BOM live cost, 1:1 passthrough scene-lock (Arkio-style).
- **Beat / own:** capture *any* home → edit *real* structure → live reno cost
  (materials + labor) → **AR-glasses walkthrough** → shared multi-device →
  vendor-agnostic catalog + buy. Nobody owns this combination.
