// Pure, deterministic helpers + editor-state model for the top-down design
// surface (web/app/design/page.tsx). NO React/DOM imports here — these
// functions serialize the in-editor model into the locked project payload the
// AR app reads (see web/lib/projectPayload.ts and the C# ProjectPayloadParser /
// BuildingModelParser). Keep field names + enum values exactly in sync with the
// contract.

import type { Point2, RenovationEdit, StagingPlacementRow } from "./projectPayload";

// ---------------------------------------------------------------------------
// Editor-state model (what the UI manipulates; plan-view METERS, x=east y=north)
// ---------------------------------------------------------------------------

/** A wall segment as drawn in the editor. */
export interface DesignWall {
  id: string;
  start: Point2;
  end: Point2;
  heightM: number;
  thicknessM: number;
  isExterior: boolean;
}

/** A placed furniture item (vendor catalog item or a generic labelled box). */
export interface DesignItem {
  id: string;
  label: string;
  /** Vendor catalog item id; omit for a generic (label-only) placeholder. */
  catalogItemId?: string;
  x: number; // plan meters (east)
  y: number; // plan meters (north)
  yawDeg: number;
  scale: number; // never 0 — see toStagingRows
  /** Footprint, meters — used only for drawing; not part of the payload. */
  widthM?: number;
  depthM?: number;
}

// ---------------------------------------------------------------------------
// Building-model geometry (stored in building_models.geometry, JSONB)
// ---------------------------------------------------------------------------

export type BuildingSourceType =
  | "roomplan"
  | "cubicasa"
  | "polycam"
  | "matterport"
  | "ar_scan"
  | "manual";

export interface GeometryOpening {
  id: string;
  kind: string; // e.g. "Door" | "Window"
  offsetM: number;
  widthM: number;
  heightM: number;
  sillHeightM: number;
}

export interface GeometryWall {
  id: string;
  start: Point2;
  end: Point2;
  thicknessM: number;
  heightM: number;
  isExterior: boolean;
  openings: GeometryOpening[];
  leftFinishMaterialId: string | null;
  rightFinishMaterialId: string | null;
}

export interface GeometryRoom {
  id: string;
  name: string;
  ceilingHeightM: number;
  floorOutline: Point2[];
  floorMaterialId: string | null;
  ceilingMaterialId: string | null;
}

export interface BuildingModelGeometry {
  id: string;
  sourceType: BuildingSourceType;
  walls: GeometryWall[];
  rooms: GeometryRoom[];
}

/** A room as authored in the editor (kept minimal; optional). */
export interface DesignRoom {
  id: string;
  name: string;
  ceilingHeightM: number;
  floorOutline: Point2[];
}

// ---------------------------------------------------------------------------
// Constants
// ---------------------------------------------------------------------------

/** Default footprint (meters) for a generic item when no catalog size is known. */
export const DEFAULT_FOOTPRINT_M = 0.8;
/** Default wall height (meters). */
export const DEFAULT_WALL_HEIGHT_M = 2.5;
/** Default wall thickness (meters). */
export const DEFAULT_WALL_THICKNESS_M = 0.1;

// ---------------------------------------------------------------------------
// Pure helpers
// ---------------------------------------------------------------------------

function round(n: number, places = 4): number {
  const f = 10 ** places;
  return Math.round(n * f) / f;
}

function snapPoint(p: Point2, places = 4): Point2 {
  return { x: round(p.x, places), y: round(p.y, places) };
}

/** Snap a meter value to the nearest grid step (e.g. 0.25m). */
export function snapToGrid(value: number, stepM: number): number {
  if (stepM <= 0) return value;
  return round(Math.round(value / stepM) * stepM);
}

/** Snap a point's coordinates to the grid step. */
export function snapPointToGrid(p: Point2, stepM: number): Point2 {
  return { x: snapToGrid(p.x, stepM), y: snapToGrid(p.y, stepM) };
}

/** Euclidean length (meters) of a wall segment. */
export function wallLengthM(w: { start: Point2; end: Point2 }): number {
  return Math.hypot(w.end.x - w.start.x, w.end.y - w.start.y);
}

/**
 * Build the building_models.geometry object from the editor walls (+ optional
 * rooms). Matches the BuildingModelParser contract exactly.
 */
export function toBuildingModelGeometry(
  walls: DesignWall[],
  rooms: DesignRoom[] = [],
  sourceType: BuildingSourceType = "manual",
  id = "manual",
): BuildingModelGeometry {
  return {
    id,
    sourceType,
    walls: walls.map<GeometryWall>((w) => ({
      id: w.id,
      start: snapPoint(w.start),
      end: snapPoint(w.end),
      thicknessM: round(w.thicknessM),
      heightM: round(w.heightM),
      isExterior: w.isExterior,
      openings: [],
      leftFinishMaterialId: null,
      rightFinishMaterialId: null,
    })),
    rooms: rooms.map<GeometryRoom>((r) => ({
      id: r.id,
      name: r.name,
      ceilingHeightM: round(r.ceilingHeightM),
      floorOutline: r.floorOutline.map((p) => snapPoint(p)),
      floorMaterialId: null,
      ceilingMaterialId: null,
    })),
  };
}

/**
 * Convert editor furniture items into staging_placements rows. Blueprint-
 * authored: hasPlan=true, plan_* set, pos_* left unset. scale is never 0.
 */
export function toStagingRows(items: DesignItem[]): StagingPlacementRow[] {
  return items.map<StagingPlacementRow>((it) => {
    const scale = it.scale && it.scale > 0 ? round(it.scale) : 1;
    const row: StagingPlacementRow = {
      hasPlan: true,
      plan_x: round(it.x),
      plan_y: round(it.y),
      plan_yaw_deg: round(it.yawDeg, 2),
      scale,
    };
    if (it.catalogItemId) row.catalog_item_id = it.catalogItemId;
    return row;
  });
}

export interface ProjectBundle {
  geometry: BuildingModelGeometry;
  placements: StagingPlacementRow[];
  edits: RenovationEdit[];
}

/** The full exportable payload: geometry + placements + (optional) edits. */
export function toProjectBundle(
  walls: DesignWall[],
  items: DesignItem[],
  rooms: DesignRoom[] = [],
  edits: RenovationEdit[] = [],
  sourceType: BuildingSourceType = "manual",
): ProjectBundle {
  return {
    geometry: toBuildingModelGeometry(walls, rooms, sourceType),
    placements: toStagingRows(items),
    edits,
  };
}
