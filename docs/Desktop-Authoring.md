# Desktop 3D editing & authoring

The PC is the design surface; the headset is the viewer. A designer imports a
capture or blueprint, edits structure and finishes with precise 2D/3D tools at a
desk, and the result registers into the real space on-site (see
`docs/PIPELINE.md` and the blueprint workflow in `VISION.md` Pillar 3b/7).

Most of the editor *scene/UI* is Unity-Editor work; the **logic it drives is in
this repo and unit-tested**:

| Tool action | Backed by (this repo) |
|---|---|
| Import a capture → editable model | `Renovation.IBuildingModelImporter` → `BuildingModel` |
| Remove / add / move walls, change finishes, ceiling height | `Renovation.RenovationEngine.Apply(base, plan)` (non-destructive edits) |
| Undo / redo | the `RenovationPlan.Edits` list is the history — pop/replay |
| Before / after toggle | render `base` vs. `RenovationEngine.Apply(base, plan)` |
| Live renovation cost | `RenovationEngine.EstimatePlanCost(...)` (demo + finishes) |
| Paint/flooring quantities | `MaterialCost.FinishQuantity` (gallons, boxes) |
| "Shop this look" / recognized finish → product | `Catalog.FinishMatcher` + `IFinishCatalog` |
| Place from blueprint → world (on-site) | `Staging.BlueprintTransform` (two-point registration) |

## Build target

- Unity **Standalone (Windows/macOS)** player — no AR packages required for the
  authoring build; it operates on the same `BuildingModel` / `RenovationPlan` /
  `StagingLayout` data the headset consumes.
- Persistence is shared: models/plans/layouts live in Supabase
  (`building_models`, `renovation_plans`, `staging_layouts`), so a PC edit shows
  up on the headset by loading the same records (and, in a live session, via the
  shared anchor + realtime sync).

## Editor scene checklist (local work)

1. A top-down **plan view** camera for 2D wall/finish editing; an orbit **3D
   view** for inspection.
2. Wall draw/move/delete tools that emit `RenovationEdit`s (don't mutate the
   model directly — append edits so undo/redo and before/after work).
3. A finish palette bound to the **materials catalog**; selecting a finish for a
   surface emits a `Change*Finish` edit; the cost panel calls
   `EstimatePlanCost` on every change.
4. A blueprint import + scale-set step (image + real-world size) feeding
   `Blueprint`; on-site, the two-point alignment feeds `BlueprintTransform`.
5. Save → Supabase; the headset/session loads the same plan and renders
   `Apply(base, plan)`.

## Round-trip with the headset

```
 PC (author)                         Headset (view on-site)
 ───────────                         ──────────────────────
 import capture → BuildingModel
 edit → RenovationPlan.Edits  ──save──►  load model + plan
 finishes → cost (live)                  register via BlueprintTransform / anchor
                                         render Apply(base, plan) at 1:1
                                         before/after toggle, walk through
```
