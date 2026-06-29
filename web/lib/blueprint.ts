// Pure, DOM-free helpers for the blueprint underlay + scale calibration +
// geometry import on the design surface (web/app/design/page.tsx).
//
// The blueprint image is a TRACING AID ONLY — it never enters the published
// payload (which stays geometry-in-meters). These helpers exist so the
// calibration math + geometry parsing are testable in isolation, with no React
// or browser APIs involved.

import type { Point2 } from "./projectPayload";
import {
  DEFAULT_WALL_HEIGHT_M,
  DEFAULT_WALL_THICKNESS_M,
  type DesignWall,
} from "./design";

// ---------------------------------------------------------------------------
// Underlay model (plan-view METERS, x=east y=north). The image's top-left
// corner sits at (x, y) in plan space; widthM/heightM are its extent in meters.
// ---------------------------------------------------------------------------

export interface BlueprintUnderlay {
  /** Data URL (e.g. "data:image/png;base64,…") of the uploaded blueprint. */
  dataUrl: string;
  /** Plan-x (meters, east) of the image's top-left corner. */
  x: number;
  /** Plan-y (meters, north) of the image's top-left corner. */
  y: number;
  /** Image width in plan meters. */
  widthM: number;
  /** Image height in plan meters. */
  heightM: number;
  /** Render opacity in [0, 1]; the user traces over the image. */
  opacity: number;
}

/** Length units the calibration form accepts. */
export type CalibrationUnit = "ft" | "m";

/** Meters per foot — exact (international foot). */
export const METERS_PER_FOOT = 0.3048;

// ---------------------------------------------------------------------------
// Calibration math
// ---------------------------------------------------------------------------

/**
 * Convert a measured value in the given unit to meters. Non-finite or negative
 * input yields NaN so callers can reject it before applying a scale.
 */
export function metersFrom(value: number, unit: CalibrationUnit): number {
  if (!Number.isFinite(value) || value <= 0) return Number.NaN;
  return unit === "ft" ? value * METERS_PER_FOOT : value;
}

/**
 * Planar distance (meters) between two plan points. Used to turn two clicks on
 * a known feature into the current (pre-calibration) plan length.
 */
export function planDistance(a: Point2, b: Point2): number {
  return Math.hypot(b.x - a.x, b.y - a.y);
}

/**
 * The scale factor that makes a feature currently spanning `dPlan` plan-meters
 * instead span `dReal` real-meters: f = dReal / dPlan. Returns NaN when the
 * inputs are unusable (e.g. the two calibration clicks coincided).
 */
export function scaleFactor(dPlanM: number, dRealM: number): number {
  if (!Number.isFinite(dPlanM) || dPlanM <= 0) return Number.NaN;
  if (!Number.isFinite(dRealM) || dRealM <= 0) return Number.NaN;
  return dRealM / dPlanM;
}

/**
 * Rescale a point about a fixed anchor by factor f. The anchor stays put; every
 * other point moves f× farther from it. Used to keep the point under the
 * user's first calibration click stationary while the drawing grows/shrinks.
 */
export function scaleAbout(p: Point2, anchor: Point2, f: number): Point2 {
  return {
    x: anchor.x + (p.x - anchor.x) * f,
    y: anchor.y + (p.y - anchor.y) * f,
  };
}

/**
 * Apply a calibration scale factor `f` to the underlay about anchor `p1`:
 *   - the image's size scales by f (widthM, heightM)
 *   - its top-left corner moves so the point under p1 stays fixed
 * After this, anything traced over the image is to true scale. Pure: returns a
 * new underlay, leaves the input untouched.
 */
export function rescaleUnderlay(
  underlay: BlueprintUnderlay,
  p1: Point2,
  f: number,
): BlueprintUnderlay {
  const topLeft = scaleAbout({ x: underlay.x, y: underlay.y }, p1, f);
  return {
    ...underlay,
    x: topLeft.x,
    y: topLeft.y,
    widthM: underlay.widthM * f,
    heightM: underlay.heightM * f,
  };
}

/**
 * Rescale a wall about anchor `p1` by factor f (both endpoints). Optional — the
 * page only calls this when the user opts to rescale already-drawn walls along
 * with the underlay (normal flow is calibrate-before-tracing, walls untouched).
 */
export function rescaleWall(w: DesignWall, p1: Point2, f: number): DesignWall {
  return {
    ...w,
    start: scaleAbout(w.start, p1, f),
    end: scaleAbout(w.end, p1, f),
  };
}

// ---------------------------------------------------------------------------
// Geometry import (building_models.geometry → DesignWall[])
// ---------------------------------------------------------------------------

function isRecord(v: unknown): v is Record<string, unknown> {
  return typeof v === "object" && v !== null;
}

function asNumber(v: unknown): number | null {
  return typeof v === "number" && Number.isFinite(v) ? v : null;
}

function asPoint(v: unknown): Point2 | null {
  if (!isRecord(v)) return null;
  const x = asNumber(v.x);
  const y = asNumber(v.y);
  if (x === null || y === null) return null;
  return { x, y };
}

/**
 * Parse a building-model geometry JSON (the shape exported by CubiCasa /
 * Matterport, or stored in building_models.geometry) into editor walls.
 *
 * Accepts either the full geometry object `{ walls: [...] }` or a bare array of
 * wall objects. Each wall needs start{x,y} + end{x,y}; thicknessM, heightM and
 * isExterior fall back to sane defaults. Walls missing usable endpoints are
 * skipped. Throws on input that isn't an object/array or carries no walls, so
 * the caller can surface a clear error card.
 */
export function parseGeometryWalls(json: unknown): DesignWall[] {
  let rawWalls: unknown;
  if (Array.isArray(json)) {
    rawWalls = json;
  } else if (isRecord(json) && Array.isArray(json.walls)) {
    rawWalls = json.walls;
  } else {
    throw new Error(
      "Expected a geometry object with a `walls` array (or a bare walls array).",
    );
  }

  const wallsArray = rawWalls as unknown[];
  const out: DesignWall[] = [];
  let autoId = 0;

  for (const raw of wallsArray) {
    if (!isRecord(raw)) continue;
    const start = asPoint(raw.start);
    const end = asPoint(raw.end);
    if (!start || !end) continue; // skip walls we can't place

    autoId += 1;
    const id =
      typeof raw.id === "string" && raw.id.trim() !== ""
        ? raw.id
        : `imported-w${autoId}`;
    const thicknessM = asNumber(raw.thicknessM) ?? DEFAULT_WALL_THICKNESS_M;
    const heightM = asNumber(raw.heightM) ?? DEFAULT_WALL_HEIGHT_M;
    const isExterior = typeof raw.isExterior === "boolean" ? raw.isExterior : true;

    out.push({ id, start, end, thicknessM, heightM, isExterior });
  }

  if (out.length === 0) {
    throw new Error("No usable walls found (each wall needs start{x,y} and end{x,y}).");
  }
  return out;
}
