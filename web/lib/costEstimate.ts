// Pure, DOM-free renovation cost estimate for the design surface. Given the
// drawn walls, the finish edits (which wall gets which material), and the
// materials catalog (price + unit), produce an itemized, advisory cost.
//
// ADVISORY: rough material-only estimate (one wall face per finish, plus a waste
// factor) — not a quote. Labor, openings, and per-room floor/ceiling finishes
// aren't included yet. Kept here so the math is testable without React.

import type { RenovationEdit } from "./projectPayload";
import { wallLengthM, type DesignWall } from "./design";

/** The catalog facts the estimate needs for a material. */
export interface CostMaterial {
  name: string;
  unit: string;          // per_sqft | per_sqm | per_linft | per_gallon | each
  pricePerUnit: number;
}

export interface CostLine {
  wallId: string;
  materialName: string;
  quantity: number;
  unitLabel: string;     // ft² | m² | lin ft | gal | ea
  subtotal: number;
}

export interface CostEstimate {
  lines: CostLine[];
  subtotal: number;
  wasteFactor: number;
  total: number;
}

const SQFT_PER_SQM = 10.7639;
const FT_PER_M = 3.28084;
const PAINT_COVERAGE_SQFT_PER_GAL = 350;

/**
 * Estimate the material cost of the assigned wall finishes. Each
 * <c>ChangeWallFinish</c> edit contributes one wall face (length × height) of
 * its material; quantities convert to the material's pricing unit, then a waste
 * factor is applied to the subtotal.
 */
export function estimateFinishCost(
  walls: DesignWall[],
  edits: RenovationEdit[],
  materialsById: Map<string, CostMaterial>,
  wasteFactor = 0.1,
): CostEstimate {
  const wallById = new Map(walls.map((w) => [w.id, w]));
  const lines: CostLine[] = [];

  for (const edit of edits) {
    if (edit.kind !== "ChangeWallFinish" || !edit.targetId || !edit.materialId) continue;
    const wall = wallById.get(edit.targetId);
    const material = materialsById.get(edit.materialId);
    if (!wall || !material) continue;

    const lengthM = wallLengthM(wall);
    const areaSqM = lengthM * Math.max(0, wall.heightM);
    const { quantity, unitLabel } = quantityFor(material.unit, areaSqM, lengthM);
    const subtotal = quantity * (Number.isFinite(material.pricePerUnit) ? material.pricePerUnit : 0);

    lines.push({ wallId: wall.id, materialName: material.name, quantity, unitLabel, subtotal });
  }

  const subtotal = lines.reduce((sum, l) => sum + l.subtotal, 0);
  const waste = Number.isFinite(wasteFactor) && wasteFactor > 0 ? wasteFactor : 0;
  return { lines, subtotal, wasteFactor: waste, total: subtotal * (1 + waste) };
}

function quantityFor(unit: string, areaSqM: number, lengthM: number): { quantity: number; unitLabel: string } {
  const areaSqFt = areaSqM * SQFT_PER_SQM;
  const lengthFt = lengthM * FT_PER_M;
  switch (unit) {
    case "per_sqm": return { quantity: round2(areaSqM), unitLabel: "m²" };
    case "per_sqft": return { quantity: round2(areaSqFt), unitLabel: "ft²" };
    case "per_linft": return { quantity: round2(lengthFt), unitLabel: "lin ft" };
    case "per_gallon": return { quantity: Math.max(1, Math.ceil(areaSqFt / PAINT_COVERAGE_SQFT_PER_GAL)), unitLabel: "gal" };
    default: return { quantity: 1, unitLabel: "ea" };
  }
}

const round2 = (n: number) => Math.round(n * 100) / 100;

/** Format a number as USD (no cents for round figures, else two decimals). */
export function formatUsd(value: number): string {
  return value.toLocaleString("en-US", { style: "currency", currency: "USD", maximumFractionDigits: 2 });
}
