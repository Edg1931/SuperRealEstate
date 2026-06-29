"use client";

// Reusable, read-only top-down plan view. Renders a building-model geometry
// (`{ walls:[...], rooms?:[...] }`, plan-view meters, x=east y=north) as an
// SVG, to scale — the same coordinate approach as the design surface, but
// presentational only (no editing, no DOM/event wiring). Optional furniture
// placements (staging_placements rows) are drawn as labelled boxes.
//
// This component is self-contained: it derives its own bounds + transform from
// the geometry so it never depends on the design page's fixed extent.

import type { Point2 } from "@/lib/projectPayload";

// --- Minimal, tolerant geometry shapes (we only read; never mutate) ---------
interface PlanWall {
  id?: string;
  start: Point2;
  end: Point2;
  thicknessM?: number | null;
  heightM?: number | null;
  isExterior?: boolean | null;
}

interface PlanRoom {
  id?: string;
  name?: string | null;
  floorOutline?: Point2[] | null;
}

export interface PlanGeometry {
  walls?: PlanWall[] | null;
  rooms?: PlanRoom[] | null;
}

/** A staging_placements-shaped row (blueprint-authored plan_* coords). */
export interface PlanPlacement {
  id?: string;
  plan_x?: number | null;
  plan_y?: number | null;
  plan_yaw_deg?: number | null;
  scale?: number | null;
  catalog_item_id?: string | null;
  label?: string | null;
}

export interface PlanViewProps {
  geometry: PlanGeometry | null | undefined;
  placements?: PlanPlacement[] | null;
  /** Optional accessible label override. */
  ariaLabel?: string;
}

// --- Render constants -------------------------------------------------------
const PX_PER_M = 40; // scale (matches the design surface)
const PAD = 24; // px padding around the plan inside the SVG
const MIN_EXTENT_M = 1; // guard against a degenerate (single-point) plan
const GRID_STEP_M = 1; // a faint 1 m grid
const DEFAULT_FOOTPRINT_M = 0.8; // placement box when no size is known
const MIN_STROKE_PX = 3; // never thinner than this so thin walls stay visible

function isFinitePoint(p: unknown): p is Point2 {
  return (
    typeof p === "object" &&
    p !== null &&
    Number.isFinite((p as Point2).x) &&
    Number.isFinite((p as Point2).y)
  );
}

interface Bounds {
  minX: number;
  minY: number;
  maxX: number;
  maxY: number;
}

/** Compute plan-space bounds spanning every wall endpoint, room point, and
 *  placement, with a small margin so geometry never touches the SVG edge. */
function computeBounds(
  walls: PlanWall[],
  rooms: PlanRoom[],
  placements: PlanPlacement[],
): Bounds {
  let minX = Number.POSITIVE_INFINITY;
  let minY = Number.POSITIVE_INFINITY;
  let maxX = Number.NEGATIVE_INFINITY;
  let maxY = Number.NEGATIVE_INFINITY;

  const eat = (x: number, y: number) => {
    if (x < minX) minX = x;
    if (y < minY) minY = y;
    if (x > maxX) maxX = x;
    if (y > maxY) maxY = y;
  };

  for (const w of walls) {
    if (isFinitePoint(w.start)) eat(w.start.x, w.start.y);
    if (isFinitePoint(w.end)) eat(w.end.x, w.end.y);
  }
  for (const r of rooms) {
    for (const p of r.floorOutline ?? []) {
      if (isFinitePoint(p)) eat(p.x, p.y);
    }
  }
  for (const pl of placements) {
    const x = pl.plan_x;
    const y = pl.plan_y;
    if (typeof x === "number" && typeof y === "number" && Number.isFinite(x) && Number.isFinite(y)) {
      eat(x, y);
    }
  }

  // Empty / degenerate → a unit box centred at origin.
  if (!Number.isFinite(minX) || !Number.isFinite(minY) || !Number.isFinite(maxX) || !Number.isFinite(maxY)) {
    return { minX: 0, minY: 0, maxX: MIN_EXTENT_M, maxY: MIN_EXTENT_M };
  }

  // A small margin (5% of the larger span, min 0.5 m) keeps strokes off the edge.
  const spanX = Math.max(maxX - minX, MIN_EXTENT_M);
  const spanY = Math.max(maxY - minY, MIN_EXTENT_M);
  const margin = Math.max(0.5, Math.max(spanX, spanY) * 0.05);
  return {
    minX: minX - margin,
    minY: minY - margin,
    maxX: maxX + margin,
    maxY: maxY + margin,
  };
}

export default function PlanView({ geometry, placements, ariaLabel }: PlanViewProps): React.ReactElement {
  const rawWalls = Array.isArray(geometry?.walls) ? (geometry?.walls ?? []) : [];
  const rawRooms = Array.isArray(geometry?.rooms) ? (geometry?.rooms ?? []) : [];
  const rawPlacements = Array.isArray(placements) ? placements : [];

  const walls = rawWalls.filter((w) => isFinitePoint(w?.start) && isFinitePoint(w?.end));
  const rooms = rawRooms.filter((r) => Array.isArray(r?.floorOutline) && r.floorOutline.length >= 2);

  const bounds = computeBounds(walls, rooms, rawPlacements);
  const extentW = Math.max(bounds.maxX - bounds.minX, MIN_EXTENT_M);
  const extentH = Math.max(bounds.maxY - bounds.minY, MIN_EXTENT_M);
  const svgW = extentW * PX_PER_M + PAD * 2;
  const svgH = extentH * PX_PER_M + PAD * 2;

  // Plan meters → screen px. y=north points UP, so screen-y flips relative to
  // plan-y. Offsets are relative to the computed bounds (not a fixed extent).
  const toScreen = (p: Point2): { sx: number; sy: number } => ({
    sx: PAD + (p.x - bounds.minX) * PX_PER_M,
    sy: PAD + (bounds.maxY - p.y) * PX_PER_M,
  });

  // Faint 1 m grid lines across the plan area.
  const gridLines: React.ReactNode[] = [];
  const startX = Math.ceil(bounds.minX / GRID_STEP_M) * GRID_STEP_M;
  for (let x = startX; x <= bounds.maxX + 1e-6; x += GRID_STEP_M) {
    const a = toScreen({ x, y: bounds.minY });
    const b = toScreen({ x, y: bounds.maxY });
    gridLines.push(<line key={`vx${x.toFixed(3)}`} x1={a.sx} y1={a.sy} x2={b.sx} y2={b.sy} className="planview-grid" />);
  }
  const startY = Math.ceil(bounds.minY / GRID_STEP_M) * GRID_STEP_M;
  for (let y = startY; y <= bounds.maxY + 1e-6; y += GRID_STEP_M) {
    const a = toScreen({ x: bounds.minX, y });
    const b = toScreen({ x: bounds.maxX, y });
    gridLines.push(<line key={`hy${y.toFixed(3)}`} x1={a.sx} y1={a.sy} x2={b.sx} y2={b.sy} className="planview-grid" />);
  }

  const hasContent = walls.length > 0 || rooms.length > 0 || rawPlacements.length > 0;

  return (
    <div className="planview-wrap">
      <svg
        className="planview-svg"
        viewBox={`0 0 ${svgW} ${svgH}`}
        width={svgW}
        height={svgH}
        role="img"
        aria-label={ariaLabel ?? "Top-down floor plan"}
        preserveAspectRatio="xMidYMid meet"
      >
        <rect x={PAD} y={PAD} width={extentW * PX_PER_M} height={extentH * PX_PER_M} className="planview-bg" />

        {gridLines}

        {/* Room outlines (faint fill behind the walls). */}
        {rooms.map((r, i) => {
          const pts = (r.floorOutline ?? [])
            .filter(isFinitePoint)
            .map((p) => {
              const s = toScreen(p);
              return `${s.sx},${s.sy}`;
            })
            .join(" ");
          if (!pts) return null;
          return <polygon key={r.id ?? `room${i}`} points={pts} className="planview-room" />;
        })}

        {/* Walls. */}
        {walls.map((w, i) => {
          const a = toScreen(w.start);
          const b = toScreen(w.end);
          const thickness = typeof w.thicknessM === "number" && w.thicknessM > 0 ? w.thicknessM : 0.1;
          return (
            <line
              key={w.id ?? `wall${i}`}
              x1={a.sx}
              y1={a.sy}
              x2={b.sx}
              y2={b.sy}
              className="planview-wall"
              strokeWidth={Math.max(MIN_STROKE_PX, thickness * PX_PER_M)}
            />
          );
        })}

        {/* Furniture placements (optional). */}
        {rawPlacements.map((pl, i) => {
          const x = pl.plan_x;
          const y = pl.plan_y;
          if (typeof x !== "number" || typeof y !== "number" || !Number.isFinite(x) || !Number.isFinite(y)) {
            return null;
          }
          const c = toScreen({ x, y });
          const scale = typeof pl.scale === "number" && pl.scale > 0 ? pl.scale : 1;
          const sizePx = DEFAULT_FOOTPRINT_M * scale * PX_PER_M;
          const yaw = typeof pl.plan_yaw_deg === "number" && Number.isFinite(pl.plan_yaw_deg) ? pl.plan_yaw_deg : 0;
          const label = pl.label?.trim() || "";
          return (
            <g key={pl.id ?? `pl${i}`} transform={`translate(${c.sx} ${c.sy}) rotate(${yaw})`} className="planview-item">
              <rect x={-sizePx / 2} y={-sizePx / 2} width={sizePx} height={sizePx} className="planview-item-rect" />
              <line x1={0} y1={0} x2={0} y2={-sizePx / 2} className="planview-item-facing" />
              {label && (
                <text className="planview-item-label" textAnchor="middle" y={3}>
                  {label}
                </text>
              )}
            </g>
          );
        })}
      </svg>
      {!hasContent && <p className="planview-empty subtle">No plan geometry to show.</p>}
    </div>
  );
}
