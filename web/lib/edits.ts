// Pure, deterministic helpers for authoring renovation edits (RenovationEdit[])
// in the design surface. NO React/DOM imports — keep these testable. The
// RenovationEdit contract lives in ./projectPayload; reuse it, don't redefine.

import type { EditKind, RenovationEdit } from "./projectPayload";

/** Lookups passed in by the caller so this stays pure (no fetching here). */
export interface SummarizeOptions {
  /** Maps a material id → a human-readable name (e.g. "Agreeable Gray"). */
  materialName?: (materialId: string) => string | undefined;
  /** Maps a wall/room/opening id → a short label (e.g. "w2"). */
  targetLabel?: (targetId: string) => string | undefined;
}

/** Edit kinds whose target is one of the drawn walls (used by the editor UI). */
export const WALL_TARGETED_KINDS: readonly EditKind[] = [
  "RemoveWall",
  "ChangeWallFinish",
] as const;

function labelFor(
  targetId: string | undefined,
  opts: SummarizeOptions | undefined,
): string {
  if (!targetId) return "?";
  return opts?.targetLabel?.(targetId) ?? targetId;
}

function materialFor(
  materialId: string | undefined,
  opts: SummarizeOptions | undefined,
): string {
  if (!materialId) return "?";
  return opts?.materialName?.(materialId) ?? materialId;
}

/**
 * Render a single edit as a short, human-readable string. Pure: all naming
 * comes from `opts` lookups (falls back to raw ids / values when absent).
 */
export function summarizeEdit(edit: RenovationEdit, opts?: SummarizeOptions): string {
  switch (edit.kind) {
    case "RemoveWall":
      return `Remove wall ${labelFor(edit.targetId, opts)}`;
    case "ChangeWallFinish":
      return `Change finish of wall ${labelFor(edit.targetId, opts)} → ${materialFor(edit.materialId, opts)}`;
    case "ChangeFloorFinish":
      return `Change floor finish of ${labelFor(edit.targetId, opts)} → ${materialFor(edit.materialId, opts)}`;
    case "ChangeCeilingFinish":
      return `Change ceiling finish of ${labelFor(edit.targetId, opts)} → ${materialFor(edit.materialId, opts)}`;
    case "ChangeCeilingHeight":
      return edit.value != null
        ? `Ceiling height → ${edit.value} m`
        : "Ceiling height change";
    case "AddWall":
      return edit.start && edit.end
        ? `Add wall ${edit.start.x},${edit.start.y} → ${edit.end.x},${edit.end.y}`
        : "Add wall";
    case "MoveWall":
      return `Move wall ${labelFor(edit.targetId, opts)}`;
    case "AddOpening":
      return `Add opening on wall ${labelFor(edit.targetId, opts)}`;
    case "RemoveOpening":
      return `Remove opening ${labelFor(edit.targetId, opts)}`;
    default: {
      // Exhaustiveness guard — a new EditKind will surface as a type error here.
      const _never: never = edit.kind;
      return _never;
    }
  }
}
