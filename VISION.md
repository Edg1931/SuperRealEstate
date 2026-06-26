# SuperRealEstate — Product Vision

> **The pitch:** put on a headset (or hold up a phone) and the home explains
> itself. Every measurement, every material, every cost, every comp, every
> hidden risk, and every "what if" — surfaced in context, in space, in real
> time. The agent becomes a superhuman expert; the buyer sees and believes.

This document is the north star. It's deliberately bigger than what we'll build
first — it's the menu of "amazing insight" we draw from. Each idea is tagged:

- 🟢 **MVP-adjacent** — builds directly on the room-measure/cost core we have.
- 🟡 **Phase 2–4** — needs data integrations or more AR plumbing.
- 🔵 **Moonshot** — high wow, harder; sequence later.
- ⚠️ **Liability note** — needs careful framing (not a substitute for a licensed
  inspector/appraiser).

---

## The core insight

A home walkthrough today is **lossy**. The agent knows some things, the buyer
absorbs a fraction, and the real intelligence (measurements, costs, condition,
value, possibilities) lives in their heads or not at all. AR/XR + AI turns the
walkthrough into a **shared, persistent, data-rich experience** where:

1. The **environment is understood** (geometry, objects, materials, condition).
2. **Insight is overlaid in place** (numbers float on the thing they describe).
3. **Everyone shares one view** (agent + buyers, across headsets and phones).
4. The session becomes a **deliverable** (recap, floor plan, staged photos, costs).

---

## Pillar 1 — Instant Property Intelligence (the "superpower")

The agent walks in and *already knows everything*. AI analyzes the live scene and
whispers insight the agent can relay naturally.

- 🟢 **Auto room measurement → material cost** *(building now)*: floor/wall/ceiling
  area, perimeter, volume; pick a material → live cost with waste factor.
- 🟢 **Renovation cost-swaps**: "rip out carpet → LVP: ~$X", "repaint this room:
  ~$Y", "new countertops: ~$Z." Each surface becomes a one-tap remodel quote.
- 🟡 **Remodel ROI**: pair reno cost with comp data → "a $15k kitchen refresh adds
  ~$28k to comparable sale price here." Turns cost into *argument*.
- 🟡 **Appliance & finish recognition** ⚠️: AI identifies appliances, cabinetry,
  countertop material, flooring, fixtures; estimates era/tier ("stainless,
  ~2015; granite counters; builder-grade cabinets"). Feeds talking points.
- 🟡 **Per-surface finish ID → cost → "shop this look"**: look at a wall and the
  AI names the finish (e.g. *Sherwin-Williams Agreeable Gray SW 7029*) with a
  cost (paint by the gallon); look at the floor and it names the flooring with a
  $/sq ft. One tap re-finishes it virtually or links to buy the real product.
  See `docs/PIPELINE.md`. *(Brand/price are advisory — confirm before purchase.)*
- 🔵 **Condition & defect spotting** ⚠️: flag water stains, cracks, mold-likely
  areas, uneven floors, dated electrical (knob-and-tube cues), roof/gutter wear.
  Framed strictly as *"worth asking the inspector about,"* never a verdict.
- 🟡 **Code/clearance hints** ⚠️: ceiling height, egress window size, stair
  rise/run, GFCI presence near water, hallway widths — surface *potential* issues.
- 🟡 **Natural light & orientation**: compass + sun position + window detection →
  "primary bedroom gets strong western afternoon sun; living room is north-facing."
- 🟡 **Sound & airflow**: estimate noise exposure (road/airport/rail proximity);
  note HVAC vent coverage per room.

## Pillar 2 — Spatial Context Overlays (X-ray vision for the lot)

Look around and see the invisible.

- 🟡 **Property lines, setbacks, easements** *(planned)*: parcel polygon anchored
  to the real yard; buildable envelope; "an ADU could go *here*."
- 🟡 **Comps in space**: glance at neighboring homes → floating price / $-per-sqft
  / last-sold tags; a neighborhood price heatmap.
- 🟡 **Risk & lifestyle layers**: flood zone, wildfire risk, noise, school
  attendance boundaries, walkability — toggled on as spatial overlays.
- 🔵 **Utility "x-ray"**: approximate water/gas/electrical/sewer runs from records;
  septic vs sewer; well location.
- 🟢 **Live redecoration**: repaint walls, swap flooring, remove a (non-load-
  bearing) wall — see it instantly, with the cost attached.
- 🟡 **Solar potential**: roof orientation + usable area → panel count, output, and
  payback estimate.

## Pillar 3 — Virtual Staging & "Bring Your Own Furniture"

*(You called this out — it's a flagship differentiator.)*

- 🟡 **Scan your own furniture** (phone): photogrammetry / Object Capture / LiDAR /
  Gaussian splat → a personal 3D furniture library tied to the account
  (USDZ + glTF). "Will *my* couch work here?"
- 🟡 **Vendor catalog import**: pull from *vendors' inventories* — furniture,
  fixtures, appliances, cabinetry, lighting — as ready-made 3D assets to drop
  into a space. A staging marketplace, not just your own scans. Especially
  powerful for **new construction**: a buyer furnishes and selects finishes from
  partner vendors *before* move-in, and "add to cart" flows straight into a real
  order. (Each vendor is an importable catalog; assets carry SKU + price.)
- 🟢 **"Will it fit?" checker** *(testable math, building now)*: real item
  dimensions vs. room bounds, doorway pass-through, and clearance/walkway margins.
- 🟡 **Place & arrange** in empty or occupied rooms; snap-to-wall, collision hints.
- 🔵 **AI auto-staging**: instantly furnish an empty room in a chosen style
  (modern farmhouse, mid-century, Scandinavian).
- 🔵 **AI declutter / depersonalize**: visually remove existing furniture and
  clutter to reveal a clean slate (great for occupied listings).
- 🔵 **Style transfer**: "show this kitchen with white shaker cabinets and a
  waterfall island" — visualized in place with a rough cost.

## Pillar 3b — Blueprint-to-Space (design on PC, place on site)

*(Inspired by Microsoft Layout on HoloLens — a flagship workflow for new builds.)*

Author a staged layout from a **floor plan / blueprint on a desktop** (calm,
precise, top-down), then **register it to the real world** and walk through it at
1:1 scale in a headset. The PC is the design surface; the headset is the viewer.

- 🟡 **Desktop blueprint authoring**: import a floor plan, set its real-world
  scale, and place vendor/own furniture in plan view at a PC — no headset needed
  to design.
- 🟡 **On-site registration**: align the blueprint to the real space, then every
  placement snaps into the room at true scale for everyone in the session.
- 🔵 **New-construction pre-visualization**: stand on a bare slab or in a framed
  shell — where there are *no walls to detect* — and see the finished, furnished
  space anchored to the site. Two-point ground alignment (pick two known
  points / stakes that match two blueprint points) solves position, rotation,
  and scale without relying on wall detection.
- 🟡 **Round-trips with Pillar 4**: the registered blueprint rides the shared
  spatial anchor, so the PC-authored design appears in the same spot for the
  agent's headset, the buyer's phone, and a remote participant alike.

## Pillar 4 — Shared Spatial Sessions (everyone sees the same thing)

*(Your example: agent + husband + wife, mixed devices, one shared scene.)*

- 🟡 **Co-located multi-device AR**: a Vision Pro, a Galaxy XR, an iPhone, and an
  iPad all see the **same virtual furniture anchored to the same real spot**,
  via shared spatial anchors. Some in headsets, some on phones — one truth.
- 🟡 **Shared furniture library**: all session participants pull from the same
  account's scanned furniture; move a chair and everyone sees it move.
- 🔵 **Remote presence**: a spouse who couldn't attend joins from home, sees the
  live spatial scan, places furniture, and points — represented by an avatar /
  spatial cursor the in-room people can see.
- 🟡 **Agent "present" mode**: spotlight an item, drop spatial annotations
  ("seller says roof replaced 2022"), guide everyone's attention.
- 🟢 **Shared budget & cart**: a running renovation/furnishing total everyone
  watches update as materials/furniture are chosen.

## Pillar 5 — Agent Enablement & Deliverables

The session shouldn't evaporate when everyone leaves.

- 🟡 **Auto-listing kit**: a walkthrough yields a measured floor plan, room
  dimensions, a material inventory, and room captures → auto-drafted listing
  description + feature list.
- 🟡 **Buyer recap**: a branded post-tour package — measurements, staged photos,
  cost estimates, comps, and the agent's spatial notes — sent automatically.
- 🟡 **Hands-free voice Q&A**: buyer asks "how old is the roof?" → the agent's
  glasses surface seller disclosures / public records, hands-free.
- 🔵 **Narrated walkthrough capture**: glasses record the tour → transcript,
  highlights, and shareable clips.

## Pillar 6 — AI Talking-Point Coach

The quiet superpower: AI watches the scene + listing data + *this buyer's stated
preferences* and feeds the agent insight to relay.

- 🟡 **Insight cards**: contextual, relayable suggestions ("point out the
  south-facing light — they wanted a bright office") and value justifications.
- 🔵 **Objection handling**: "buyer worried about kitchen size — note it's 12%
  larger than the neighborhood median and opens to the living area."
- 🟡 **Preference matching**: tuned to the buyer's wishlist (home office, big
  yard, natural light) → highlights matching features as they walk.

## Pillar 7 — Renovation Visualization (capture → edit → walk it in AR)

*(Take a Matterport/CubiCasa/RoomPlan capture, remodel it with 3D tools, and
walk the result at 1:1 in glasses. The blueprint workflow's bigger sibling.)*

The loop: **capture or import → editable building model → renovate → cost it →
see it in AR**.

- 🟡 **Import a captured model**: ingest a Matterport twin, a CubiCasa floor
  plan, or an Apple RoomPlan scan into an **editable building model** — walls,
  doors/windows, and rooms as first-class objects (not just a frozen mesh).
- 🟡 **Structural edits**: remove / move / add walls, open up a kitchen, raise a
  ceiling, add an opening — modeled as a **non-destructive edit list** so
  before/after toggles cleanly and edits sync across a shared session.
- ⚠️ 🟡 **Load-bearing advisory**: flag walls that are *likely* structural
  (exterior, long spans, perpendicular to joists) before someone imagines
  removing them — always framed as "confirm with a structural engineer," never a
  determination.
- 🟢 **Finishes & live cost**: repaint, swap flooring, change cabinets/counters
  from the **vendor catalog**, and watch the **renovation cost** update as you
  design (reuses the measurement + `CostEstimator` core; demolition + materials
  + a labor estimate). Pair with comps for **renovation ROI**.
- 🟡 **Before/after in AR**: stand in the real room and toggle existing ↔
  renovated; **"remove" a wall as a portal into the already-scanned adjacent
  room** — feasible on passthrough headsets because we composite a *known* scan,
  not inpaint the unknown (see `docs/Diminished-Reality.md`). See-through works
  in an occupied home; true walk-through is for empty shells / new construction.
- 🟡 **Desktop 3D editing → on-site viewing**: heavy editing at a PC (precise
  modeling tools), then register and walk it on glasses — same PC→site bridge as
  the blueprint workflow.
- 🔵 **AI auto-redesign**: "show this as a modern open-concept kitchen" →
  generated layout + finishes the user can then refine.

## Pillar 8 — Outdoor & Landscape Intelligence

The yard is half the property and nobody serves it well in AR.

- 🟢 **Plant / tree / flower ID**: walk up and the app names it (common +
  scientific) with care level, water/sun, mature size, toxicity to kids/pets,
  invasive flag, pollen/allergy, and replacement cost.
- 🟢 **"How much do I need?" calculators**: measure a flower bed → **mulch**
  (cu yd + bags); a patio → **concrete / pavers**; a lawn → **sod / seed**; a
  run → **fence** (posts + panels). Same AR measurement, bulk-material answers.
- 🟡 **Hardscape design**: drop a virtual patio / deck / pergola / pool with live
  cost + takeoff; sun/shade path for the best spots; buildable-envelope checks.
- See `docs/FEATURES-BACKLOG.md` for the full idea list and `docs/DESIGN-SYSTEM.md`
  for the spatial UI/eye-tracking design language.

## Pillar 9 — Builder & Construction (Trimble-class, consumer-simple)

For builders and their teams — and buyers who want to *see* the systems. Full
detail in `docs/BUILDER-TOOLS.md`.

- 🟡 **Walk the blueprint on the lot**: register a plan at 1:1 on the ground,
  pull the 3D model up out of it, and walk the framed house before it's built —
  next to the **property lines** and **where the sun rises/sets**.
- 🟡 **Mechanicals overlay (MEP)**: electrical, plumbing, HVAC, structural, gas,
  sewer, low-voltage runs + fixtures overlaid on the real structure — buyers see
  where everything is; crews confirm it's correct.
- 🟢 **Takeoffs + clash + as-built QA**: linear feet of wire/duct/pipe, fixture
  counts, trade clashes (duct-through-joist), and designed-vs-installed drift.
- 🟡 **Site layout**: footprint vs. property lines (setbacks), buildable
  envelope, sun path — all on the real lot, shared across the crew's devices.

---

## Competitive landscape & the gap we exploit

| Product | What it nails | The gap we fill |
|---|---|---|
| **Matterport / Zillow 3D** | Beautiful 3D digital twins & remote tours | Passive viewing; no *live* in-person insight, costing, or staging-with-your-own-furniture |
| **IKEA Place / Wayfair AR** | Placing *catalog* furniture via phone AR | Their catalog only; single device; no shared session; no real-estate intelligence |
| **Apple RoomPlan / Object Capture** | Best-in-class room + object scanning APIs | Building blocks, not a product; iOS-only; no cross-device sharing or domain insight |
| **CubiCasa / magicplan / Hover** | Floor plans & exterior measurement | Measurement only; not real-time, in-headset, or insight-rich |
| **Virtual staging services** | Photorealistic staged *photos* | Offline, 2D, days of turnaround; not live, spatial, or interactive |
| **Spatial / Arkio / Mesh** | Multi-user collaborative AR/VR | Generic collaboration; no real-estate data, costing, or property intelligence |
| **Microsoft Layout (HoloLens, retired)** | PC blueprint authoring → 1:1 placement in real space | Enterprise/HoloLens-only & discontinued; no real-estate domain, vendor catalogs, costing, or buyer-facing flows |
| **CubiCasa / RoomPlan** | Fast capture → semantic floor plan / walls | Capture only; no renovation editing, costing, or AR walkthrough of an *edited* result |
| **Planner 5D / Cedreo / SketchUp / Chief Architect** | Powerful 3D home design & renders from plans | Desktop renders, not AR-on-site; steep or pro-only; not tied to live cost/comps or a buyer in the room |
| **Spacely / REimagineHome / IKEA Kreativ** | AI redesign of a room from a photo | 2D image output; not a walkable, edited 3D model; no structural edits or 1:1 AR |
| **Enscape / Twinmotion / D5** | Real-time photoreal arch-viz | Needs a CAD/BIM model + a pro operator; not capture-to-AR for an agent in the field |

**Our wedge:** the only platform that fuses (1) live spatial measurement +
costing, (2) AI scene insight, (3) bring-your-own-furniture **and vendor-catalog**
staging, (4) **blueprint-to-space** authoring (PC → on-site, great for new
builds), (5) **capture → edit → renovation visualization** walkable in AR with
live cost, and (6) **cross-device shared sessions** into one realtor-buyer
experience. Nobody owns the intersection — capture tools stop at the model,
design tools stop at a desktop render, AI tools stop at a 2D image.

---

## Responsible-AI guardrails (non-negotiable for a realtor product)

- **Condition/defect/code features are advisory only.** Always framed as "worth
  asking your inspector" — never a determination. Disclaimers in-product.
- **Valuations/comps are estimates**, clearly labeled, sourced, and dated.
- **No fair-housing-sensitive inference.** The AI must not characterize
  neighborhoods by demographics or steer; school/crime/“safety” layers are
  handled carefully and sourced from neutral public data only.
- **Privacy:** scans of occupied homes contain personal belongings — explicit
  consent, retention limits, and easy deletion.

---

## How this maps to what we've built

- The **measurement + cost core** (done) is the literal foundation of Pillars 1–2.
- The **"will it fit?" checker** + **staging/session data model** (building next)
  seed Pillars 3–4.
- The **AI scene-insight contract** (building next) is the seam Pillars 1 & 6
  plug into once we add a vision model.

See `ROADMAP.md` for sequencing.
