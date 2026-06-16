# Feature backlog & ideas

Outside-the-box features to make this genuinely great for realtors and buyers
walking through a property. Tags: 🟢 builds on what we have · 🟡 needs an
integration/AR work · 🔵 moonshot. ⭐ = you specifically asked for it.

## Outdoor & Landscape intelligence (Pillar 8)

The yard is half the property and nobody serves it well in AR.

- ⭐🟢 **Plant / tree / flower ID**: walk up and the app names it (common +
  scientific), with **care level, water & sun needs, mature size, toxicity to
  kids/pets, invasive flag, pollen/allergy, and replacement cost.** Built:
  `PlantIdentification` + `IPlantIdentifier` (Pl@ntNet/Plant.id via Edge Fn).
- ⭐🟢 **"How much do I need?" bulk calculators**: measure a flower bed → **mulch
  (cu yd + bags)**; a patio → **concrete**, **pavers**; a lawn → **sod / grass
  seed**; a yard run → **fence (posts + panels)**. Built:
  `Landscape.BulkMaterialCalculator` (tested), fed by the same AR measurement.
- 🟡 **Lawn area → maintenance**: mowing time/cost, irrigation zones, "xeriscape
  this" water-savings estimate.
- 🟡 **Tree risk/value** ⚠️: proximity to foundation/roof, shade value, rough
  removal cost — advisory, "consult an arborist."
- 🟡 **Sun/shade path** across the yard over the day → best garden/patio spots,
  solar potential (ties to Pillar 2).
- 🟡 **Hardscape design**: drop a virtual **patio / deck / pergola / pool** and
  see it at 1:1 with live cost + the bulk-material takeoff.
- 🟡 **Property line + setbacks + buildable envelope** → "could an ADU / pool /
  addition fit here?" (advisory; parcel data via Regrid).
- 🔵 **Seasonal preview**: "show this yard in spring bloom / fall color."
- 🔵 **Drainage/slope hints** from the scan → flag low/pooling spots.

## Indoor extras

- 🟢 **Delivery-path check**: extend "will it fit?" to the whole route — front
  door → hallway → stairs → room (not just the destination doorway).
- 🟡 **Fixture sizing**: right ceiling-fan/chandelier size for the room; **TV
  size + viewing distance** for a wall; **rug size** for a seating group.
- 🟡 **Aging-in-place / accessibility audit** ⚠️: door widths, ramp needs,
  grab-bar spots, step-free routes.
- 🟡 **HVAC vent coverage / draft + noise estimate** per room.
- 🔵 **"What's behind this wall"** utility hints (water/gas/electrical) from
  records — advisory.

## Realtor workflow & intelligence

- 🟡 **Auto floor plan + GLA**: the walkthrough generates a measured,
  listing-ready floor plan and gross living area.
- 🟡 **Auto listing kit**: description, room dimensions, feature list, tagged
  photos — drafted from the walk.
- 🟡 **Buyer recap**: branded post-tour package (measurements, staged/renovated
  shots, costs, comps, the agent's spatial notes).
- 🟡 **Affordability overlay**: mortgage + tax + HOA + insurance estimate, and
  commute/POI/school layers, pinned in space.
- 🟡 **Comparison mode**: overlay two properties' rooms side by side at 1:1.
- 🟡 **Disclosure/inspection overlay**: pin known issues to where they are.

## Eye-tracking-native ideas (only possible here)

- 🟢 **"Look at anything → it explains itself"**: the north star — gaze a wall,
  plant, appliance, or floor and its card appears; look away, it recedes.
- 🟡 **Attention insights for sellers** (privacy-gated, opt-in): where buyers
  lingered ("they spent 40s on the kitchen island") → real feedback for pricing
  and staging. A genuinely novel data product the eye tracker unlocks.
- 🟢 **Fully hands-free tour**: gaze + voice ("how much mulch?", "what's this
  tree?", "remove this wall") so an agent never touches a screen.

## Moonshots

- 🔵 **Wall-removal portals** (see `docs/Diminished-Reality.md`) — feasible via
  prior-scan compositing.
- 🔵 **AI auto-redesign** of a room/yard the user then refines in 3D.
- 🔵 **Live "time machine"**: scrub a property through proposed renovation phases
  in place.

## Notes
- Everything advisory (plant toxicity, tree risk, code, condition, valuations)
  carries a disclaimer and a "confirm with a pro" handoff — per the
  responsible-AI guardrails in `VISION.md`.
- Volume/quantity math (`BulkMaterialCalculator`, `FinishQuantity`) is the
  reusable spine: measure once, answer "how much / how much $" for any material.
