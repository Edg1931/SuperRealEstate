# The Walk-In Pipeline (end-to-end review)

How the pieces fit when an agent + buyers walk into a room, and how we make it
"the best way." This is the review of the whole process — measure → recognize
finishes → cost → re-finish virtually → stage furniture → share — plus gaps and
innovative additions.

## The flow

```
 walk into a room
        │
   1. DETECT & MEASURE ──► RoomGeometry / RoomMeasurements + Surface[] (floor, walls, ceiling)
        │
   2. RECOGNIZE FINISHES ─► AI per-surface: "wall = SW Agreeable Gray ~$45/gal",
        │                    "floor = oak hardwood ~$8/sqft"  (SurfaceFinding[])
        │
   3. COST IT ───────────► current finish cost + proposed-change cost
        │                    (CostEstimator + FinishQuantity: paint by gallon, etc.)
        │
   4. RE-FINISH (AR) ────► recolor the wall / swap the floor; before/after toggle
        │
   5. STAGE ─────────────► drop own / vendor-catalog furniture on the same floor
        │                    (FitChecker: fits through door? clears walkways?)
        │
   6. SHARE & CAPTURE ───► everyone sees it (shared anchor); recap + cart export
```

## The key design decision: one **Surface** primitive

The "best way" insight is that measurement, finish-recognition, re-finishing,
and staging are all about the **same surfaces**. So `RoomMeasure.Surface` is the
single object they all attach to:

- **Area** (for cost + coverage),
- **RecognizedFinishId** (what the AI sees there now),
- **ProposedFinishId** (what the user changed it to),
- and, for a floor, the **staging plane** furniture drops onto.

This is why step 2's recognized wall and step 5's staging floor aren't separate
features bolted together — they're properties of one `Surface`. It keeps the AR
overlay, the cost math, and the catalog all pointing at the same thing.

## Stage-by-stage (what exists + best practice)

1. **Detect & measure** — `MeasurementService` + `FloorOutlineBuilder` (built,
   tested). Each detected plane becomes a `Surface` with area. *Best practice:*
   confirm-on-tap where auto-detection is ambiguous; depth-capable devices only
   (Vision Pro / Galaxy XR).

2. **Recognize finishes** — `scene-insights` Edge Function now returns
   `surfaces[]` with material type, **brand/product/color** (e.g. "Sherwin-
   Williams Agreeable Gray SW 7029"), an estimated unit cost, and a confidence.
   `EdgeFunctionSceneAnalyzer.AnalyzeSceneAsync` parses them into
   `SurfaceFinding`. *Best practice (from research):* brand/price are
   **advisory** — let the user tap-to-confirm/correct; white-balance the frame
   for accurate color; map the recognized product to a catalog `Material`/
   `CatalogItem` so cost and re-finish use the real product ("shop this look").

3. **Cost it** — `FinishQuantity` prices finishes **the right way**: paint by
   the **gallon** (coats × coverage), flooring by the **box** (with waste),
   tile/area by area. `CostEstimator` / `RenovationCostEstimator` add demolition
   + (advisory) labor. *Best practice:* localize unit costs by ZIP (RSMeans/
   Kukun-style factors), always label "estimate," and offer a "get real bids"
   handoff — bid-grade live cost is an unsolved frontier, retail-BOM live cost
   is achievable now.

4. **Re-finish (AR)** — change a `Surface`'s `ProposedFinishId`; recolor/retexture
   in the headset with before/after toggle (Renovation edit list). *Best
   practice:* relight the new finish to the room's lighting so it reads true.

5. **Stage** — `FitChecker` already validates fit-through-door + walkway
   clearance for own furniture **and** vendor-catalog items (shared `Vector3`
   size). Furniture drops on the recognized **floor** `Surface`. (Built.)

6. **Share & capture** — shared spatial anchor (Collaboration) puts the same
   recolored walls + staged furniture in front of everyone; export a recap +
   a vendor cart of chosen products.

## Gaps & innovative additions (buyer/realtor value)

- 🟢 **Shop-the-look fulfillment**: recognized finish/furniture → exact product +
  price + buy/cart (validated: every AI tool monetizes this; none tie it to a
  measured 3D model).
- 🟢 **"Renovation budget HUD"**: a running total updating as surfaces are
  re-finished and furniture is added — everyone in the session sees it.
- 🟡 **Palette try-on**: swap a whole color palette (SW/Behr/BM fan decks) across
  all walls at once; "show me 4 schemes."
- 🟡 **Refinish vs. replace** options per surface (refinish hardwood vs. new LVP)
  with cost + disruption tradeoffs.
- 🟡 **Lighting/time-of-day simulation** on new finishes (north vs. west light).
- 🟡 **Per-surface condition note** (advisory): scuffs, water stains, wear on
  *that* wall → "ask the inspector."
- 🟡 **Renovation ROI**: reno cost (this engine) × comps → "≈ $X added value."
- 🔵 **AI auto-redesign** of a whole room (open-concept, modern kitchen) the user
  then refines — beats the 2D-only AI tools by producing an editable 3D result.
- 🟡 **Wall removal as a scan portal** — show a wall gone by compositing the
  already-scanned adjacent room over it on passthrough (not live inpainting).
  Feasible for see-through now; walk-through in empty shells. Full live
  diminished reality (incomplete-scan inpainting + relighting) is the research
  frontier. See `docs/Diminished-Reality.md`.

## Caveats we will not paper over

- Brand/finish recognition and cost are **estimates** — confirmable, never
  asserted as fact for purchase/contract.
- Condition / code / load-bearing flags are **advisory** ("confirm with an
  inspector / structural engineer / licensed pro").
- Bid-grade renovation cost and automated code compliance are **hard/unsolved**;
  we ship advisory estimates + narrow structured checks + a cited code copilot,
  and hand off to real bids/pros.
