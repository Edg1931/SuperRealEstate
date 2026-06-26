# Project payload contract

The canonical JSON shapes a project stores, so the **writer** (the phone/desktop
design surface) and the **reader** (the AR app) always agree. This is the contract
the ProjectStore agent flagged as previously unspecified — now pinned down and
round-trip-tested.

- Reader: `ProjectsBackend.ProjectPayloadParser` (C#).
- Writer (reference): `ProjectsBackend.ProjectPayloadSerializer` (C#) — proves the
  shape round-trips (`ProjectPayloadSerializerTests`).
- Web/desktop writer: `web/lib/projectPayload.ts` (TypeScript mirror).

## `renovation_plans.edits` (JSONB array)

camelCase, one object per `RenovationEdit`. `kind` is the enum name.

```json
[
  { "kind": "RemoveWall", "targetId": "w1" },
  { "kind": "ChangeFloorFinish", "targetId": "r1", "materialId": "lvp" },
  { "kind": "AddWall", "start": {"x":0,"y":0}, "end": {"x":4,"y":0}, "heightM": 2.5, "thicknessM": 0.1 }
]
```

| Field | Type | Notes |
|---|---|---|
| `id` | string? | optional edit id |
| `kind` | enum | `RemoveWall` `AddWall` `MoveWall` `AddOpening` `RemoveOpening` `ChangeFloorFinish` `ChangeWallFinish` `ChangeCeilingFinish` `ChangeCeilingHeight` |
| `targetId` | string? | wall / room / opening the edit acts on |
| `materialId` | string? | finish-change material |
| `value` | number? | e.g. new ceiling height (m) |
| `start`,`end` | `{x,y}`? | AddWall/MoveWall geometry, plan meters |
| `heightM`,`thicknessM` | number? | AddWall |

Unknown `kind` strings parse to a safe default; malformed JSON → empty plan.

## `staging_placements` rows

snake_case (the DB columns from migrations `0002`/`0003`). A row is **blueprint-
authored** when `hasPlan` is true or `plan_x`/`plan_y` is non-zero; otherwise it's
a **world/anchor** placement from `pos_*`.

```json
[
  { "catalog_item_id": "sofa", "plan_x": 2, "plan_y": 3, "plan_yaw_deg": 90, "scale": 1.25, "hasPlan": true },
  { "furniture_asset_id": "chair", "pos_x": 1, "pos_y": 0, "pos_z": 1, "rot_y_deg": 45, "scale": 1, "anchor_id": "a1" }
]
```

| Field | Type | Notes |
|---|---|---|
| `id` | string? | |
| `furniture_asset_id` / `catalog_item_id` | string? | one or the other (own vs. vendor) |
| `pos_x/y/z`, `rot_y_deg` | number | world / anchor-relative |
| `plan_x/y`, `plan_yaw_deg` | number | blueprint-plane |
| `scale` | number | default 1; **0 is treated as 1** by the reader |
| `anchor_id` | string? | shared-anchor handle |
| `hasPlan` | bool? | set true when authoring on a blueprint (disambiguates plan-origin) |

## Rules for writers

1. Emit edits camelCase, placement rows snake_case (matches the DB columns).
2. Always set `hasPlan: true` for blueprint-authored placements (so a placement
   at plan origin isn't misread as world).
3. Use the exact `kind` enum names above.
4. `scale` defaults to 1; don't emit 0.

Changing these shapes means updating both `ProjectPayloadParser` (+ its tests) and
`web/lib/projectPayload.ts` together.
