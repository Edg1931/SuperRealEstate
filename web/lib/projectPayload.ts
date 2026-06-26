// Canonical project-payload shapes — the contract between the design surface
// (web/desktop, which WRITES these) and the AR app (which READS them via
// ProjectPayloadParser in Assets/Scripts/ProjectsBackend). Keep this in sync
// with ProjectPayloadSerializer.cs (round-trip-tested) — see
// docs/PROJECT-PAYLOAD-CONTRACT.md.

/** Stored in renovation_plans.edits (JSONB). camelCase, mirroring RenovationEdit. */
export type EditKind =
  | "RemoveWall"
  | "AddWall"
  | "MoveWall"
  | "AddOpening"
  | "RemoveOpening"
  | "ChangeFloorFinish"
  | "ChangeWallFinish"
  | "ChangeCeilingFinish"
  | "ChangeCeilingHeight";

export interface Point2 {
  x: number;
  y: number;
}

export interface RenovationEdit {
  id?: string;
  kind: EditKind;
  targetId?: string;   // wall / room / opening id this edit acts on
  materialId?: string; // for finish changes
  value?: number;      // e.g. new ceiling height (m)
  start?: Point2;      // AddWall / MoveWall geometry (plan meters)
  end?: Point2;
  heightM?: number;
  thicknessM?: number;
}

/** A row in staging_placements. snake_case to match the DB columns. */
export interface StagingPlacementRow {
  id?: string;
  furniture_asset_id?: string; // user's own scanned furniture
  catalog_item_id?: string;    // a vendor catalog item

  // World / anchor-relative placement:
  pos_x?: number;
  pos_y?: number;
  pos_z?: number;
  rot_y_deg?: number;
  scale?: number;        // default 1; 0 is treated as 1 by the parser
  anchor_id?: string;

  // Blueprint-plane placement (set hasPlan=true so a placement legitimately at
  // plan origin (0,0) is still classified as blueprint-authored):
  plan_x?: number;
  plan_y?: number;
  plan_yaw_deg?: number;
  hasPlan?: boolean;
}

/**
 * A row is treated as blueprint-authored when hasPlan is true OR plan_x/plan_y
 * are non-zero; otherwise it's a world/anchor placement from pos_*. Set hasPlan
 * explicitly when authoring on a blueprint.
 */
export function isBlueprintRow(r: StagingPlacementRow): boolean {
  return r.hasPlan === true || (r.plan_x ?? 0) !== 0 || (r.plan_y ?? 0) !== 0;
}
