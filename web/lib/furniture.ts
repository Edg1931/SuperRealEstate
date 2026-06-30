// Pure, DOM-free furniture geometry helpers — the web mirror of the C#
// `CaptureBounds` math in `Assets/Scripts/Capture/FurnitureCapture.cs`. Kept
// side-effect-free so the math is unit-testable in isolation.

/** Metric dimensions of a captured object (width × depth × height, all meters). */
export interface Dims {
  widthM: number;
  depthM: number;
  heightM: number;
}

/** A standard US interior door: 32" × 80" → 0.81 m × 2.03 m. */
export const STANDARD_DOOR = { widthM: 0.81, heightM: 2.03 } as const;

const METERS_TO_FEET = 3.28084;

/**
 * Can this object pass through a rectangular opening (a doorway), allowing it to
 * be turned/tilted? EXACT mirror of the C# `CaptureBounds.FitsThroughOpening`:
 * sort the object's three dims ascending (d1 ≤ d2 ≤ d3) and the opening's two
 * ascending (o1 ≤ o2); it fits iff its two smallest dimensions clear the
 * opening's two — i.e. lead with the smallest face. The "will the movers get it
 * inside?" check.
 */
export function fitsThroughOpening(
  dims: Dims,
  openingWidthM: number,
  openingHeightM: number,
): boolean {
  // Object dims ascending (d1 ≤ d2 ≤ d3); d3 is unused — the deepest dimension
  // can always run lengthwise through the opening.
  const sorted = [dims.widthM, dims.depthM, dims.heightM].sort((a, b) => a - b);
  const d1 = sorted[0];
  const d2 = sorted[1];
  // Opening dims ascending.
  const o1 = Math.min(openingWidthM, openingHeightM);
  const o2 = Math.max(openingWidthM, openingHeightM);
  return d1 <= o1 && d2 <= o2;
}

/** Footprint area (width × depth) in square meters. */
export function footprintSqM(dims: Dims): number {
  return dims.widthM * dims.depthM;
}

/** Volume (width × depth × height) in cubic meters. */
export function volumeM3(dims: Dims): number {
  return dims.widthM * dims.depthM * dims.heightM;
}

function trimNum(n: number): string {
  // One decimal place, but drop a trailing ".0" so 2.0 → "2".
  return Number.parseFloat(n.toFixed(1)).toString();
}

/**
 * Human-readable dimensions, e.g. `2.1 × 0.9 × 0.85 m (6.9 × 3 × 2.8 ft)`.
 * Meters first (the captured unit), with a feet equivalent in parens.
 */
export function formatDims(dims: Dims): string {
  const m = `${trimNum(dims.widthM)} × ${trimNum(dims.depthM)} × ${trimNum(dims.heightM)} m`;
  const ft = `${trimNum(dims.widthM * METERS_TO_FEET)} × ${trimNum(
    dims.depthM * METERS_TO_FEET,
  )} × ${trimNum(dims.heightM * METERS_TO_FEET)} ft`;
  return `${m} (${ft})`;
}
