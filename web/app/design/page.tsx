"use client";

import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { supabase } from "@/lib/supabase";
import {
  DEFAULT_FOOTPRINT_M,
  DEFAULT_WALL_HEIGHT_M,
  DEFAULT_WALL_THICKNESS_M,
  snapPointToGrid,
  toProjectBundle,
  wallLengthM,
  type DesignItem,
  type DesignWall,
} from "@/lib/design";
import type { Point2, RenovationEdit } from "@/lib/projectPayload";
import { summarizeEdit } from "@/lib/edits";
import {
  metersFrom,
  parseGeometryWalls,
  planDistance,
  rescaleUnderlay,
  rescaleWall,
  scaleFactor,
  type BlueprintUnderlay,
  type CalibrationUnit,
} from "@/lib/blueprint";
import { estimateFinishCost, formatUsd, type CostMaterial } from "@/lib/costEstimate";

// --- Canvas constants ------------------------------------------------------
const EXTENT_W_M = 12; // plan width  (east, x)
const EXTENT_H_M = 9; // plan height (north, y)
const PX_PER_M = 40; // scale
const GRID_STEP_M = 0.25; // snap + minor gridlines
const PAD = 24; // px padding around the plan inside the SVG
const SVG_W = EXTENT_W_M * PX_PER_M + PAD * 2;
const SVG_H = EXTENT_H_M * PX_PER_M + PAD * 2;

// --- Catalog row -----------------------------------------------------------
interface CatalogItem {
  id: string;
  name: string;
  category: string | null;
  width_m: number | null;
  depth_m: number | null;
  height_m: number | null;
  vendor_id: string | null;
}

// --- Materials row (finish library) ----------------------------------------
interface MaterialItem {
  id: string;
  name: string;
  brand: string | null;
  category: string | null;
  price_per_unit: number | null;
  unit: string | null;
}

type Mode = "wall" | "furniture" | "calibrate";

// The two clicks collected during a calibration pass, before the user enters
// the known real-world distance.
interface CalibrationPick {
  p1: Point2;
  p2: Point2 | null;
}

// A short label for a wall, used in edit summaries and selects.
function wallLabel(w: DesignWall): string {
  return `${w.id} (${w.start.x.toFixed(1)},${w.start.y.toFixed(1)} → ${w.end.x.toFixed(1)},${w.end.y.toFixed(1)})`;
}

// Coordinate transforms. We draw with y=north pointing UP, so screen-y is
// flipped relative to plan-y.
function planToScreen(p: Point2): { sx: number; sy: number } {
  return {
    sx: PAD + p.x * PX_PER_M,
    sy: PAD + (EXTENT_H_M - p.y) * PX_PER_M,
  };
}

function screenToPlan(sx: number, sy: number): Point2 {
  return {
    x: (sx - PAD) / PX_PER_M,
    y: EXTENT_H_M - (sy - PAD) / PX_PER_M,
  };
}

let _idCounter = 0;
function newId(prefix: string): string {
  _idCounter += 1;
  return `${prefix}${_idCounter}-${Math.random().toString(36).slice(2, 7)}`;
}

export default function DesignPage() {
  // ----- Editor state -----
  const [mode, setMode] = useState<Mode>("wall");
  const [walls, setWalls] = useState<DesignWall[]>([]);
  const [items, setItems] = useState<DesignItem[]>([]);
  const [pendingStart, setPendingStart] = useState<Point2 | null>(null);
  const [cursor, setCursor] = useState<Point2 | null>(null);
  const [selectedItemId, setSelectedItemId] = useState<string | null>(null);

  // ----- Blueprint underlay + scale calibration (tracing aids; NOT published) -----
  const [underlay, setUnderlay] = useState<BlueprintUnderlay | null>(null);
  const [calibPick, setCalibPick] = useState<CalibrationPick | null>(null);
  const [calibValue, setCalibValue] = useState<string>("");
  const [calibUnit, setCalibUnit] = useState<CalibrationUnit>("ft");
  const [calibRescaleWalls, setCalibRescaleWalls] = useState(false);
  const [calibReadout, setCalibReadout] = useState<string | null>(null);
  const blueprintInputRef = useRef<HTMLInputElement | null>(null);

  // ----- Geometry import (CubiCasa / Matterport / building_models.geometry) -----
  const [importError, setImportError] = useState<string | null>(null);
  const geometryInputRef = useRef<HTMLInputElement | null>(null);

  // ----- Furniture picker -----
  const [catalog, setCatalog] = useState<CatalogItem[]>([]);
  const [catalogError, setCatalogError] = useState<string | null>(null);
  const [catalogLoading, setCatalogLoading] = useState(true);
  const [pickedCatalogId, setPickedCatalogId] = useState<string>(""); // "" = generic
  const [genericLabel, setGenericLabel] = useState("Box");

  // ----- Renovation edits -----
  const [edits, setEdits] = useState<RenovationEdit[]>([]);
  const [materials, setMaterials] = useState<MaterialItem[]>([]);
  const [materialsError, setMaterialsError] = useState<string | null>(null);
  const [materialsLoading, setMaterialsLoading] = useState(true);
  const [finishWallId, setFinishWallId] = useState<string>(""); // wall to refinish
  const [finishMaterialId, setFinishMaterialId] = useState<string>("");
  const [removeWallId, setRemoveWallId] = useState<string>(""); // wall to remove
  const [ceilingHeightInput, setCeilingHeightInput] = useState<string>("2.7");

  // ----- Auth + publish -----
  const [userId, setUserId] = useState<string | null>(null);
  const [authChecked, setAuthChecked] = useState(false);
  const [email, setEmail] = useState("");
  const [magicSent, setMagicSent] = useState(false);
  const [publishing, setPublishing] = useState(false);
  const [publishError, setPublishError] = useState<string | null>(null);
  const [publishedProjectId, setPublishedProjectId] = useState<string | null>(null);

  const svgRef = useRef<SVGSVGElement | null>(null);

  // Load vendor catalog items for the furniture picker.
  useEffect(() => {
    let active = true;
    (async () => {
      const { data, error } = await supabase
        .from("vendor_catalog_items")
        .select("id,name,category,width_m,depth_m,height_m,vendor_id")
        .order("name", { ascending: true });
      if (!active) return;
      if (error) {
        setCatalogError(error.message);
        setCatalog([]);
      } else {
        setCatalog((data as CatalogItem[] | null) ?? []);
      }
      setCatalogLoading(false);
    })();
    return () => {
      active = false;
    };
  }, []);

  // Load materials (finish library) for the renovation-edit picker.
  useEffect(() => {
    let active = true;
    (async () => {
      const { data, error } = await supabase
        .from("materials")
        .select("id,name,brand,category,price_per_unit,unit")
        .order("category", { ascending: true })
        .order("name", { ascending: true });
      if (!active) return;
      if (error) {
        setMaterialsError(error.message);
        setMaterials([]);
      } else {
        setMaterials((data as MaterialItem[] | null) ?? []);
      }
      setMaterialsLoading(false);
    })();
    return () => {
      active = false;
    };
  }, []);

  // Check auth state once on mount.
  useEffect(() => {
    let active = true;
    (async () => {
      const { data } = await supabase.auth.getUser();
      if (!active) return;
      setUserId(data.user?.id ?? null);
      setAuthChecked(true);
    })();
    const { data: sub } = supabase.auth.onAuthStateChange((_event, session) => {
      setUserId(session?.user?.id ?? null);
      setAuthChecked(true);
    });
    return () => {
      active = false;
      sub.subscription.unsubscribe();
    };
  }, []);

  const catalogById = useMemo(() => {
    const m = new Map<string, CatalogItem>();
    for (const c of catalog) m.set(c.id, c);
    return m;
  }, [catalog]);

  const bundle = useMemo(
    () => toProjectBundle(walls, items, [], edits),
    [walls, items, edits],
  );
  const bundleJson = useMemo(() => JSON.stringify(bundle, null, 2), [bundle]);

  // Lookup maps for human-readable edit summaries (kept pure in summarizeEdit).
  const materialNameById = useMemo(() => {
    const m = new Map<string, string>();
    for (const mat of materials) m.set(mat.id, mat.name);
    return m;
  }, [materials]);

  const wallLabelById = useMemo(() => {
    const m = new Map<string, string>();
    for (const w of walls) m.set(w.id, w.id);
    return m;
  }, [walls]);

  // Pricing map + live material-cost estimate from the assigned wall finishes.
  const materialsCostById = useMemo(() => {
    const m = new Map<string, CostMaterial>();
    for (const mat of materials) {
      m.set(mat.id, { name: mat.name, unit: mat.unit ?? "each", pricePerUnit: mat.price_per_unit ?? 0 });
    }
    return m;
  }, [materials]);

  const cost = useMemo(
    () => estimateFinishCost(walls, edits, materialsCostById),
    [walls, edits, materialsCostById],
  );

  // Wall ids referenced by any RemoveWall edit — used to hint them on canvas.
  const removedWallIds = useMemo(() => {
    const s = new Set<string>();
    for (const e of edits) {
      if (e.kind === "RemoveWall" && e.targetId) s.add(e.targetId);
    }
    return s;
  }, [edits]);

  const selectedItem = useMemo(
    () => items.find((i) => i.id === selectedItemId) ?? null,
    [items, selectedItemId],
  );

  // --- Pointer helpers ---
  const eventToPlan = useCallback((evt: React.MouseEvent<SVGSVGElement>): Point2 | null => {
    const svg = svgRef.current;
    if (!svg) return null;
    const rect = svg.getBoundingClientRect();
    // Map client px → SVG user units (viewBox === pixel size here).
    const sx = ((evt.clientX - rect.left) / rect.width) * SVG_W;
    const sy = ((evt.clientY - rect.top) / rect.height) * SVG_H;
    return screenToPlan(sx, sy);
  }, []);

  const clampToExtent = useCallback((p: Point2): Point2 => {
    return {
      x: Math.min(Math.max(p.x, 0), EXTENT_W_M),
      y: Math.min(Math.max(p.y, 0), EXTENT_H_M),
    };
  }, []);

  const onCanvasMove = useCallback(
    (evt: React.MouseEvent<SVGSVGElement>) => {
      const p = eventToPlan(evt);
      if (!p) return;
      setCursor(snapPointToGrid(clampToExtent(p), GRID_STEP_M));
    },
    [eventToPlan, clampToExtent],
  );

  const onCanvasLeave = useCallback(() => setCursor(null), []);

  const onCanvasClick = useCallback(
    (evt: React.MouseEvent<SVGSVGElement>) => {
      const raw = eventToPlan(evt);
      if (!raw) return;
      const p = snapPointToGrid(clampToExtent(raw), GRID_STEP_M);

      if (mode === "calibrate") {
        // Collect two points on a feature of known length. Don't snap to the
        // grid — calibration should use the exact clicked positions.
        if (!calibPick || calibPick.p2) {
          // First click (or restart after a completed pair).
          setCalibPick({ p1: raw, p2: null });
        } else {
          // Second click completes the pair; the form then appears.
          setCalibPick({ p1: calibPick.p1, p2: raw });
        }
        return;
      }

      if (mode === "wall") {
        if (!pendingStart) {
          setPendingStart(p);
        } else {
          // Ignore zero-length segments.
          if (p.x !== pendingStart.x || p.y !== pendingStart.y) {
            const w: DesignWall = {
              id: newId("w"),
              start: pendingStart,
              end: p,
              heightM: DEFAULT_WALL_HEIGHT_M,
              thicknessM: DEFAULT_WALL_THICKNESS_M,
              isExterior: true,
            };
            setWalls((prev) => [...prev, w]);
          }
          setPendingStart(null);
        }
        return;
      }

      // Furniture mode: place an item at the click.
      const picked = pickedCatalogId ? catalogById.get(pickedCatalogId) : undefined;
      const widthM = picked?.width_m ?? DEFAULT_FOOTPRINT_M;
      const depthM = picked?.depth_m ?? DEFAULT_FOOTPRINT_M;
      const label = picked ? picked.name : genericLabel.trim() || "Item";
      const item: DesignItem = {
        id: newId("i"),
        label,
        catalogItemId: picked?.id,
        x: p.x,
        y: p.y,
        yawDeg: 0,
        scale: 1,
        widthM: widthM ?? DEFAULT_FOOTPRINT_M,
        depthM: depthM ?? DEFAULT_FOOTPRINT_M,
      };
      setItems((prev) => [...prev, item]);
      setSelectedItemId(item.id);
    },
    [
      mode,
      pendingStart,
      clampToExtent,
      eventToPlan,
      pickedCatalogId,
      catalogById,
      genericLabel,
      calibPick,
    ],
  );

  // --- Mutators ---
  const deleteWall = useCallback((id: string) => {
    setWalls((prev) => prev.filter((w) => w.id !== id));
    // Drop any edits that targeted this wall so we never publish a stale
    // targetId that no longer exists in the building model geometry.
    setEdits((prev) => prev.filter((e) => e.targetId !== id));
    setFinishWallId((cur) => (cur === id ? "" : cur));
    setRemoveWallId((cur) => (cur === id ? "" : cur));
  }, []);

  // --- Renovation-edit mutators ---
  const addEdit = useCallback((edit: RenovationEdit) => {
    setEdits((prev) => [...prev, edit]);
  }, []);

  const deleteEdit = useCallback((index: number) => {
    setEdits((prev) => prev.filter((_, i) => i !== index));
  }, []);

  const addWallFinishEdit = useCallback(() => {
    if (!finishWallId || !finishMaterialId) return;
    addEdit({
      id: newId("e"),
      kind: "ChangeWallFinish",
      targetId: finishWallId,
      materialId: finishMaterialId,
    });
  }, [finishWallId, finishMaterialId, addEdit]);

  const addRemoveWallEdit = useCallback(() => {
    if (!removeWallId) return;
    // Avoid duplicate RemoveWall edits for the same wall.
    setEdits((prev) => {
      if (prev.some((e) => e.kind === "RemoveWall" && e.targetId === removeWallId)) {
        return prev;
      }
      return [...prev, { id: newId("e"), kind: "RemoveWall", targetId: removeWallId }];
    });
  }, [removeWallId]);

  const addCeilingHeightEdit = useCallback(() => {
    const value = Number(ceilingHeightInput);
    if (!Number.isFinite(value) || value <= 0) return;
    addEdit({ id: newId("e"), kind: "ChangeCeilingHeight", value });
  }, [ceilingHeightInput, addEdit]);

  const deleteItem = useCallback((id: string) => {
    setItems((prev) => prev.filter((i) => i.id !== id));
    setSelectedItemId((cur) => (cur === id ? null : cur));
  }, []);

  const updateItem = useCallback((id: string, patch: Partial<DesignItem>) => {
    setItems((prev) => prev.map((i) => (i.id === id ? { ...i, ...patch } : i)));
  }, []);

  const clearAll = useCallback(() => {
    setWalls([]);
    setItems([]);
    setEdits([]);
    setFinishWallId("");
    setRemoveWallId("");
    setPendingStart(null);
    setSelectedItemId(null);
  }, []);

  // --- Blueprint underlay (tracing aid; never part of the published payload) ---
  const onBlueprintFile = useCallback((evt: React.ChangeEvent<HTMLInputElement>) => {
    const file = evt.target.files?.[0];
    // Allow re-selecting the same file later.
    evt.target.value = "";
    if (!file) return;
    const reader = new FileReader();
    reader.onload = () => {
      const dataUrl = typeof reader.result === "string" ? reader.result : null;
      if (!dataUrl) return;
      // Read the natural aspect ratio so the default height matches the image.
      const img = new Image();
      img.onload = () => {
        const aspect =
          img.naturalWidth > 0 && img.naturalHeight > 0
            ? img.naturalHeight / img.naturalWidth
            : EXTENT_H_M / EXTENT_W_M;
        const widthM = EXTENT_W_M;
        const heightM = widthM * aspect;
        setUnderlay({
          dataUrl,
          x: 0, // plan top-left x
          y: EXTENT_H_M, // plan top-left y (north edge)
          widthM,
          heightM,
          opacity: 0.5,
        });
        setCalibReadout(null);
      };
      img.src = dataUrl;
    };
    reader.readAsDataURL(file);
  }, []);

  const removeBlueprint = useCallback(() => {
    setUnderlay(null);
    setCalibPick(null);
    setCalibReadout(null);
  }, []);

  const setUnderlayOpacity = useCallback((opacity: number) => {
    setUnderlay((cur) => (cur ? { ...cur, opacity } : cur));
  }, []);

  // --- Scale calibration: two clicks + a known distance → a scale factor f ---
  const applyCalibration = useCallback(() => {
    if (!calibPick || !calibPick.p2 || !underlay) return;
    const dPlan = planDistance(calibPick.p1, calibPick.p2);
    const dReal = metersFrom(Number(calibValue), calibUnit);
    const f = scaleFactor(dPlan, dReal);
    if (!Number.isFinite(f)) return;

    setUnderlay((cur) => (cur ? rescaleUnderlay(cur, calibPick.p1, f) : cur));
    // Optionally bring already-drawn walls along (default: leave them as-is).
    if (calibRescaleWalls) {
      setWalls((prev) => prev.map((w) => rescaleWall(w, calibPick.p1, f)));
    }
    setCalibReadout(
      `Scale ×${f.toFixed(3)} applied — ${dPlan.toFixed(2)} m measured now = ` +
        `${dReal.toFixed(2)} m real. Blueprint ≈ ${(underlay.widthM * f).toFixed(2)}×` +
        `${(underlay.heightM * f).toFixed(2)} m. (Canvas grid: 1 m = ${PX_PER_M} px.)`,
    );
    setCalibPick(null);
    setCalibValue("");
  }, [calibPick, underlay, calibValue, calibUnit, calibRescaleWalls]);

  const cancelCalibration = useCallback(() => {
    setCalibPick(null);
  }, []);

  // --- Geometry import (CubiCasa / Matterport / building_models.geometry) ---
  const loadImportedWalls = useCallback(
    (imported: DesignWall[], replace: boolean) => {
      if (replace) {
        setWalls(imported);
        // Stale edit/finish/remove targets would no longer exist — clear them.
        setEdits([]);
        setFinishWallId("");
        setRemoveWallId("");
        setPendingStart(null);
      } else {
        setWalls((prev) => [...prev, ...imported]);
      }
    },
    [],
  );

  const onGeometryFile = useCallback(
    (evt: React.ChangeEvent<HTMLInputElement>) => {
      const file = evt.target.files?.[0];
      evt.target.value = "";
      if (!file) return;
      setImportError(null);
      const reader = new FileReader();
      reader.onload = () => {
        try {
          const text = typeof reader.result === "string" ? reader.result : "";
          const json: unknown = JSON.parse(text);
          const imported = parseGeometryWalls(json);
          // Replace by default (a clean import); confirm so appended traces or
          // an existing drawing aren't silently discarded.
          const replace =
            walls.length === 0
              ? true
              : window.confirm(
                  `Import ${imported.length} wall(s). Replace the ${walls.length} ` +
                    `existing wall(s)?\n\nOK = replace · Cancel = append.`,
                );
          loadImportedWalls(imported, replace);
        } catch (e) {
          setImportError(
            e instanceof Error ? e.message : "Couldn't parse that geometry JSON.",
          );
        }
      };
      reader.onerror = () => setImportError("Couldn't read that file.");
      reader.readAsText(file);
    },
    [walls.length, loadImportedWalls],
  );

  // --- Payload export ---
  const copyJson = useCallback(async () => {
    try {
      await navigator.clipboard.writeText(bundleJson);
    } catch {
      // Clipboard may be blocked; ignore silently — the textarea is selectable.
    }
  }, [bundleJson]);

  const downloadJson = useCallback(() => {
    const blob = new Blob([bundleJson], { type: "application/json" });
    const url = URL.createObjectURL(blob);
    const a = document.createElement("a");
    a.href = url;
    a.download = "project-payload.json";
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    URL.revokeObjectURL(url);
  }, [bundleJson]);

  // --- Auth: magic link ---
  const sendMagicLink = useCallback(async () => {
    setPublishError(null);
    const trimmed = email.trim();
    if (!trimmed) {
      setPublishError("Enter an email address first.");
      return;
    }
    const { error } = await supabase.auth.signInWithOtp({
      email: trimmed,
      options:
        typeof window !== "undefined"
          ? { emailRedirectTo: window.location.href }
          : undefined,
    });
    if (error) {
      setPublishError(error.message);
      return;
    }
    setMagicSent(true);
  }, [email]);

  // --- Publish: insert in order, threading ids ---
  const hasEdits = bundle.edits.length > 0;

  const publish = useCallback(async () => {
    setPublishError(null);
    setPublishedProjectId(null);
    setPublishing(true);
    try {
      const { data: userData } = await supabase.auth.getUser();
      const uid = userData.user?.id;
      if (!uid) {
        setPublishError("You need to be signed in to publish. Send a magic link below.");
        setPublishing(false);
        return;
      }

      // 1) building_models
      const { data: bm, error: bmErr } = await supabase
        .from("building_models")
        .insert({ owner_id: uid, source_type: "manual", geometry: bundle.geometry })
        .select("id")
        .single();
      if (bmErr || !bm) throw new Error(bmErr?.message ?? "Failed to create building model.");
      const buildingModelId = bm.id as string;

      // 2) staging_layouts
      const { data: layout, error: layoutErr } = await supabase
        .from("staging_layouts")
        .insert({ name: "Design surface layout", created_by: uid })
        .select("id")
        .single();
      if (layoutErr || !layout) throw new Error(layoutErr?.message ?? "Failed to create staging layout.");
      const layoutId = layout.id as string;

      // 3) staging_placements (set plan_*, leave pos_* at 0)
      if (bundle.placements.length > 0) {
        const rows = bundle.placements.map((r) => ({
          layout_id: layoutId,
          catalog_item_id: r.catalog_item_id ?? null,
          furniture_asset_id: r.furniture_asset_id ?? null,
          pos_x: 0,
          pos_y: 0,
          pos_z: 0,
          rot_y_deg: 0,
          scale: r.scale ?? 1,
          plan_x: r.plan_x ?? 0,
          plan_y: r.plan_y ?? 0,
          plan_yaw_deg: r.plan_yaw_deg ?? 0,
        }));
        const { error: placeErr } = await supabase.from("staging_placements").insert(rows);
        if (placeErr) throw new Error(placeErr.message);
      }

      // 4) renovation_plans (only if there are edits)
      let renovationPlanId: string | null = null;
      if (hasEdits) {
        const { data: plan, error: planErr } = await supabase
          .from("renovation_plans")
          .insert({
            owner_id: uid,
            building_model_id: buildingModelId,
            name: "Design surface plan",
            edits: bundle.edits,
          })
          .select("id")
          .single();
        if (planErr || !plan) throw new Error(planErr?.message ?? "Failed to create renovation plan.");
        renovationPlanId = plan.id as string;
      }

      // 5) renovation_projects (the top record tying it all together)
      const { data: project, error: projErr } = await supabase
        .from("renovation_projects")
        .insert({
          owner_id: uid,
          name: "Untitled design",
          kind: hasEdits ? "renovation" : "empty_staging",
          origin: "manual_desktop",
          status: "ready_for_ar",
          building_model_id: buildingModelId,
          staging_layout_id: layoutId,
          renovation_plan_id: renovationPlanId,
        })
        .select("id")
        .single();
      if (projErr || !project) throw new Error(projErr?.message ?? "Failed to create project.");

      setPublishedProjectId(project.id as string);
    } catch (e) {
      setPublishError(e instanceof Error ? e.message : "Publish failed.");
    } finally {
      setPublishing(false);
    }
  }, [bundle, hasEdits]);

  // --- Render helpers ---
  const gridLines = useMemo(() => {
    const lines: React.ReactNode[] = [];
    const minor = GRID_STEP_M * 4; // draw a line every 1m for clarity
    for (let x = 0; x <= EXTENT_W_M + 1e-6; x += minor) {
      const a = planToScreen({ x, y: 0 });
      const b = planToScreen({ x, y: EXTENT_H_M });
      lines.push(
        <line key={`vx${x}`} x1={a.sx} y1={a.sy} x2={b.sx} y2={b.sy} className="grid-line" />,
      );
    }
    for (let y = 0; y <= EXTENT_H_M + 1e-6; y += minor) {
      const a = planToScreen({ x: 0, y });
      const b = planToScreen({ x: EXTENT_W_M, y });
      lines.push(
        <line key={`hy${y}`} x1={a.sx} y1={a.sy} x2={b.sx} y2={b.sy} className="grid-line" />,
      );
    }
    return lines;
  }, []);

  const origin = planToScreen({ x: 0, y: 0 });

  // The underlay's screen rect. The image's top-left (plan x, y) maps to a
  // screen point via planToScreen (which flips y); width/height in px are the
  // plan extents × PX_PER_M.
  const underlayScreen = useMemo(() => {
    if (!underlay) return null;
    const tl = planToScreen({ x: underlay.x, y: underlay.y });
    return {
      x: tl.sx,
      y: tl.sy,
      width: underlay.widthM * PX_PER_M,
      height: underlay.heightM * PX_PER_M,
    };
  }, [underlay]);

  return (
    <main>
      <p>
        <a href="/">← Home</a>
      </p>
      <h2>Design surface</h2>
      <p className="subtle">
        Draw a top-down plan (walls + furniture), preview the payload, and publish
        it as the locked project the AR app reads. Coordinates are in meters
        (x = east, y = north); the grid snaps to {GRID_STEP_M} m.
      </p>

      <div className="design-layout">
        {/* ---------- Canvas + toolbar ---------- */}
        <section className="design-canvas-col">
          <div className="design-toolbar" role="toolbar" aria-label="Editor mode">
            <button
              type="button"
              className={mode === "wall" ? "tool-btn active" : "tool-btn"}
              aria-pressed={mode === "wall"}
              onClick={() => {
                setMode("wall");
                setSelectedItemId(null);
              }}
            >
              Wall
            </button>
            <button
              type="button"
              className={mode === "furniture" ? "tool-btn active" : "tool-btn"}
              aria-pressed={mode === "furniture"}
              onClick={() => {
                setMode("furniture");
                setPendingStart(null);
              }}
            >
              Furniture
            </button>
            <button
              type="button"
              className={mode === "calibrate" ? "tool-btn active" : "tool-btn"}
              aria-pressed={mode === "calibrate"}
              onClick={() => {
                setMode("calibrate");
                setPendingStart(null);
                setSelectedItemId(null);
                setCalibPick(null);
              }}
            >
              Calibrate scale
            </button>
            <span className="design-toolbar-spacer" />
            {mode === "wall" && pendingStart && (
              <button type="button" className="tool-btn" onClick={() => setPendingStart(null)}>
                Cancel wall
              </button>
            )}
            <button type="button" className="tool-btn" onClick={clearAll}>
              Clear all
            </button>
          </div>

          <div className="design-canvas-wrap">
            <svg
              ref={svgRef}
              className="design-canvas"
              viewBox={`0 0 ${SVG_W} ${SVG_H}`}
              width={SVG_W}
              height={SVG_H}
              role="img"
              aria-label="Top-down plan editor canvas"
              onMouseMove={onCanvasMove}
              onMouseLeave={onCanvasLeave}
              onClick={onCanvasClick}
            >
              {/* plan background */}
              <rect
                x={PAD}
                y={PAD}
                width={EXTENT_W_M * PX_PER_M}
                height={EXTENT_H_M * PX_PER_M}
                className="design-plan-bg"
              />

              {/* blueprint underlay — behind grid/walls/items; a tracing aid */}
              {underlay && underlayScreen && (
                <image
                  href={underlay.dataUrl}
                  x={underlayScreen.x}
                  y={underlayScreen.y}
                  width={underlayScreen.width}
                  height={underlayScreen.height}
                  opacity={underlay.opacity}
                  preserveAspectRatio="none"
                  className="design-underlay"
                  aria-hidden="true"
                />
              )}

              {gridLines}

              {/* origin marker */}
              <circle cx={origin.sx} cy={origin.sy} r={4} className="design-origin" />
              <text x={origin.sx + 6} y={origin.sy - 6} className="design-axis-label">
                0,0
              </text>

              {/* existing walls (dashed/red hint when marked for removal) */}
              {walls.map((w) => {
                const a = planToScreen(w.start);
                const b = planToScreen(w.end);
                const removed = removedWallIds.has(w.id);
                return (
                  <line
                    key={w.id}
                    x1={a.sx}
                    y1={a.sy}
                    x2={b.sx}
                    y2={b.sy}
                    className={removed ? "design-wall removed" : "design-wall"}
                    strokeWidth={Math.max(3, w.thicknessM * PX_PER_M)}
                  />
                );
              })}

              {/* pending wall preview */}
              {mode === "wall" && pendingStart && cursor && (
                <line
                  x1={planToScreen(pendingStart).sx}
                  y1={planToScreen(pendingStart).sy}
                  x2={planToScreen(cursor).sx}
                  y2={planToScreen(cursor).sy}
                  className="design-wall-preview"
                />
              )}
              {mode === "wall" && pendingStart && (
                <circle
                  cx={planToScreen(pendingStart).sx}
                  cy={planToScreen(pendingStart).sy}
                  r={4}
                  className="design-wall-anchor"
                />
              )}

              {/* furniture items */}
              {items.map((it) => {
                const c = planToScreen({ x: it.x, y: it.y });
                const wM = (it.widthM ?? DEFAULT_FOOTPRINT_M) * it.scale;
                const dM = (it.depthM ?? DEFAULT_FOOTPRINT_M) * it.scale;
                const wpx = wM * PX_PER_M;
                const dpx = dM * PX_PER_M;
                const selected = it.id === selectedItemId;
                return (
                  <g
                    key={it.id}
                    transform={`translate(${c.sx} ${c.sy}) rotate(${it.yawDeg})`}
                    className={selected ? "design-item selected" : "design-item"}
                    onClick={(e) => {
                      e.stopPropagation();
                      setSelectedItemId(it.id);
                    }}
                  >
                    <rect
                      x={-wpx / 2}
                      y={-dpx / 2}
                      width={wpx}
                      height={dpx}
                      className="design-item-rect"
                    />
                    {/* facing indicator (north before rotation) */}
                    <line x1={0} y1={0} x2={0} y2={-dpx / 2} className="design-item-facing" />
                    <text className="design-item-label" textAnchor="middle" y={3}>
                      {it.label}
                    </text>
                  </g>
                );
              })}

              {/* calibration picks (two clicks on a feature of known length) */}
              {mode === "calibrate" && calibPick && (
                <>
                  <circle
                    cx={planToScreen(calibPick.p1).sx}
                    cy={planToScreen(calibPick.p1).sy}
                    r={5}
                    className="design-calib-pt"
                  />
                  {calibPick.p2 ? (
                    <>
                      <line
                        x1={planToScreen(calibPick.p1).sx}
                        y1={planToScreen(calibPick.p1).sy}
                        x2={planToScreen(calibPick.p2).sx}
                        y2={planToScreen(calibPick.p2).sy}
                        className="design-calib-line"
                      />
                      <circle
                        cx={planToScreen(calibPick.p2).sx}
                        cy={planToScreen(calibPick.p2).sy}
                        r={5}
                        className="design-calib-pt"
                      />
                    </>
                  ) : (
                    cursor && (
                      <line
                        x1={planToScreen(calibPick.p1).sx}
                        y1={planToScreen(calibPick.p1).sy}
                        x2={planToScreen(cursor).sx}
                        y2={planToScreen(cursor).sy}
                        className="design-calib-line"
                      />
                    )
                  )}
                </>
              )}

              {/* snap cursor */}
              {cursor && (
                <circle
                  cx={planToScreen(cursor).sx}
                  cy={planToScreen(cursor).sy}
                  r={3}
                  className="design-cursor"
                />
              )}
            </svg>
            <div className="design-canvas-hint subtle">
              {mode === "wall"
                ? pendingStart
                  ? "Click again to finish the wall segment."
                  : "Click to drop the wall start point."
                : mode === "calibrate"
                  ? calibPick && calibPick.p2
                    ? "Two points set — enter the real length at right, then Set scale."
                    : calibPick
                      ? "Click the second point of the known feature."
                      : "Click two points on a feature whose real length you know."
                  : "Pick an item at right, then click on the canvas to place it."}
              {cursor && (
                <span className="design-coord">
                  {" "}
                  · cursor {cursor.x.toFixed(2)}, {cursor.y.toFixed(2)} m
                </span>
              )}
            </div>
          </div>
        </section>

        {/* ---------- Side panel ---------- */}
        <aside className="design-side">
          {/* ---------- Blueprint underlay + calibration + import ---------- */}
          <div className="card design-panel">
            <h3>Blueprint &amp; scale</h3>
            <p className="meta">
              Upload a floor-plan image (a builder blueprint or a CubiCasa /
              Matterport export), calibrate it to one known dimension, then trace
              walls over it to true scale. The image is a tracing aid only — it is
              never part of the published payload. PDFs aren’t supported; export an
              image (PNG/JPG) first.
            </p>

            <label className="design-field">
              <span>Upload blueprint (PNG/JPG)</span>
              <input
                ref={blueprintInputRef}
                type="file"
                accept="image/png,image/jpeg,image/*"
                onChange={onBlueprintFile}
                aria-label="Upload blueprint image"
              />
            </label>

            {underlay && (
              <>
                <label className="design-field">
                  <span>Opacity: {Math.round(underlay.opacity * 100)}%</span>
                  <input
                    type="range"
                    min={0.1}
                    max={1}
                    step={0.05}
                    value={underlay.opacity}
                    onChange={(e) => setUnderlayOpacity(Number(e.target.value))}
                    aria-label="Blueprint opacity"
                  />
                </label>
                <div className="meta">
                  Blueprint ≈ {underlay.widthM.toFixed(2)}×
                  {underlay.heightM.toFixed(2)} m on the plan.
                </div>
                <button type="button" className="tool-btn danger" onClick={removeBlueprint}>
                  Remove blueprint
                </button>
              </>
            )}

            {/* Calibration form (appears once two points are picked) */}
            {underlay && (
              <fieldset className="design-edit-group">
                <legend>Calibrate scale</legend>
                {mode !== "calibrate" && (
                  <div className="meta">
                    Switch to the <strong>Calibrate scale</strong> tool, then click two
                    points on a feature of known length.
                  </div>
                )}
                {mode === "calibrate" && !calibPick && (
                  <div className="meta">Click the first point on the canvas.</div>
                )}
                {mode === "calibrate" && calibPick && !calibPick.p2 && (
                  <div className="meta">Click the second point on the canvas.</div>
                )}
                {calibPick && calibPick.p2 && (
                  <>
                    <div className="meta">
                      Measured span: {planDistance(calibPick.p1, calibPick.p2).toFixed(2)} m
                      (current scale).
                    </div>
                    <label className="design-field">
                      <span>Real length</span>
                      <input
                        type="number"
                        min={0}
                        step="any"
                        value={calibValue}
                        onChange={(e) => setCalibValue(e.target.value)}
                        placeholder="e.g. 3"
                        aria-label="Known real length value"
                      />
                    </label>
                    <label className="design-field">
                      <span>Unit</span>
                      <select
                        value={calibUnit}
                        onChange={(e) =>
                          setCalibUnit(e.target.value === "m" ? "m" : "ft")
                        }
                        aria-label="Known length unit"
                      >
                        <option value="ft">feet (ft)</option>
                        <option value="m">meters (m)</option>
                      </select>
                    </label>
                    <label className="design-field design-check">
                      <input
                        type="checkbox"
                        checked={calibRescaleWalls}
                        onChange={(e) => setCalibRescaleWalls(e.target.checked)}
                        aria-label="Also rescale already-drawn walls"
                      />
                      <span>Also rescale already-drawn walls</span>
                    </label>
                    <div className="design-toolbar">
                      <button
                        type="button"
                        className="tool-btn active"
                        onClick={applyCalibration}
                        disabled={
                          !Number.isFinite(
                            scaleFactor(
                              planDistance(calibPick.p1, calibPick.p2),
                              metersFrom(Number(calibValue), calibUnit),
                            ),
                          )
                        }
                      >
                        Set scale
                      </button>
                      <button type="button" className="tool-btn" onClick={cancelCalibration}>
                        Reset picks
                      </button>
                    </div>
                  </>
                )}
                {calibReadout && <div className="meta">{calibReadout}</div>}
              </fieldset>
            )}

            {/* Geometry JSON import */}
            <fieldset className="design-edit-group">
              <legend>Import floor plan (JSON)</legend>
              <p className="meta">
                Load a building-model geometry JSON (e.g. a CubiCasa / Matterport
                export, or a published <code>building_models.geometry</code>). Its
                walls become editable wall segments.
              </p>
              <label className="design-field">
                <span>Import geometry</span>
                <input
                  ref={geometryInputRef}
                  type="file"
                  accept="application/json,.json"
                  onChange={onGeometryFile}
                  aria-label="Import floor plan geometry JSON"
                />
              </label>
              {importError && (
                <div className="card design-error">
                  <h3>Couldn’t import</h3>
                  <div className="meta">{importError}</div>
                </div>
              )}
            </fieldset>
          </div>

          {mode === "furniture" && (
            <div className="card design-panel">
              <h3>Place furniture</h3>
              {catalogLoading && <div className="meta">Loading catalog…</div>}
              {catalogError && (
                <div className="meta">
                  Couldn’t load the vendor catalog ({catalogError}). You can still
                  place generic labelled boxes below.
                </div>
              )}
              {!catalogLoading && !catalogError && catalog.length === 0 && (
                <div className="meta">
                  No vendor catalog items yet — place a generic box with a typed
                  label (it’ll have no catalog id; map it later).
                </div>
              )}

              <label className="design-field">
                <span>Item</span>
                <select
                  value={pickedCatalogId}
                  onChange={(e) => setPickedCatalogId(e.target.value)}
                  aria-label="Catalog item to place"
                >
                  <option value="">Generic box (label only)</option>
                  {catalog.map((c) => (
                    <option key={c.id} value={c.id}>
                      {c.name}
                      {c.width_m && c.depth_m ? ` (${c.width_m}×${c.depth_m} m)` : ""}
                    </option>
                  ))}
                </select>
              </label>

              {!pickedCatalogId && (
                <label className="design-field">
                  <span>Generic label</span>
                  <input
                    type="text"
                    value={genericLabel}
                    onChange={(e) => setGenericLabel(e.target.value)}
                    placeholder="e.g. Sofa"
                    aria-label="Generic item label"
                  />
                </label>
              )}
            </div>
          )}

          {/* Selected item editor */}
          {selectedItem && (
            <div className="card design-panel">
              <h3>Selected: {selectedItem.label}</h3>
              <label className="design-field">
                <span>Yaw: {selectedItem.yawDeg}°</span>
                <input
                  type="range"
                  min={0}
                  max={359}
                  step={5}
                  value={selectedItem.yawDeg}
                  onChange={(e) =>
                    updateItem(selectedItem.id, { yawDeg: Number(e.target.value) })
                  }
                  aria-label="Item yaw degrees"
                />
              </label>
              <label className="design-field">
                <span>Scale: {selectedItem.scale.toFixed(2)}×</span>
                <input
                  type="range"
                  min={0.25}
                  max={3}
                  step={0.05}
                  value={selectedItem.scale}
                  onChange={(e) =>
                    updateItem(selectedItem.id, { scale: Number(e.target.value) || 1 })
                  }
                  aria-label="Item scale"
                />
              </label>
              <button
                type="button"
                className="tool-btn danger"
                onClick={() => deleteItem(selectedItem.id)}
              >
                Delete item
              </button>
            </div>
          )}

          {/* Walls list */}
          <div className="card design-panel">
            <h3>Walls ({walls.length})</h3>
            {walls.length === 0 && <div className="meta">No walls yet.</div>}
            <ul className="design-list">
              {walls.map((w) => (
                <li key={w.id}>
                  <span className="meta">
                    {w.start.x.toFixed(2)},{w.start.y.toFixed(2)} →{" "}
                    {w.end.x.toFixed(2)},{w.end.y.toFixed(2)} ·{" "}
                    {wallLengthM(w).toFixed(2)} m
                  </span>
                  <button
                    type="button"
                    className="tool-btn small danger"
                    onClick={() => deleteWall(w.id)}
                    aria-label={`Delete wall ${w.id}`}
                  >
                    ✕
                  </button>
                </li>
              ))}
            </ul>
          </div>

          {/* Renovation edits */}
          <div className="card design-panel">
            <h3>Edits (renovation)</h3>
            <p className="meta">
              Mark renovation changes against the drawn walls. Adding any edit
              publishes a renovation plan (project kind <span className="pill">renovation</span>).
            </p>

            {materialsLoading && <div className="meta">Loading materials…</div>}
            {materialsError && (
              <div className="meta">
                Couldn’t load materials ({materialsError}). Finish changes need the
                materials catalog; wall removal &amp; ceiling height still work.
              </div>
            )}
            {!materialsLoading && !materialsError && materials.length === 0 && (
              <div className="meta">
                No materials yet — seed the catalog to enable finish changes.
              </div>
            )}

            {/* Change wall finish */}
            <fieldset className="design-edit-group">
              <legend>Change wall finish</legend>
              <label className="design-field">
                <span>Wall</span>
                <select
                  value={finishWallId}
                  onChange={(e) => setFinishWallId(e.target.value)}
                  aria-label="Wall to refinish"
                >
                  <option value="">Select a wall…</option>
                  {walls.map((w) => (
                    <option key={w.id} value={w.id}>
                      {wallLabel(w)}
                    </option>
                  ))}
                </select>
              </label>
              <label className="design-field">
                <span>Material</span>
                <select
                  value={finishMaterialId}
                  onChange={(e) => setFinishMaterialId(e.target.value)}
                  aria-label="Finish material"
                  disabled={materials.length === 0}
                >
                  <option value="">Select a material…</option>
                  {materials.map((m) => (
                    <option key={m.id} value={m.id}>
                      {m.name}
                      {m.brand ? ` · ${m.brand}` : ""}
                      {m.category ? ` (${m.category})` : ""}
                    </option>
                  ))}
                </select>
              </label>
              <button
                type="button"
                className="tool-btn"
                onClick={addWallFinishEdit}
                disabled={!finishWallId || !finishMaterialId}
              >
                Add finish change
              </button>
            </fieldset>

            {/* Remove wall */}
            <fieldset className="design-edit-group">
              <legend>Remove wall</legend>
              <label className="design-field">
                <span>Wall</span>
                <select
                  value={removeWallId}
                  onChange={(e) => setRemoveWallId(e.target.value)}
                  aria-label="Wall to mark for removal"
                >
                  <option value="">Select a wall…</option>
                  {walls.map((w) => (
                    <option key={w.id} value={w.id}>
                      {wallLabel(w)}
                    </option>
                  ))}
                </select>
              </label>
              <button
                type="button"
                className="tool-btn danger"
                onClick={addRemoveWallEdit}
                disabled={!removeWallId}
              >
                Mark wall for removal
              </button>
            </fieldset>

            {/* Change ceiling height */}
            <fieldset className="design-edit-group">
              <legend>Change ceiling height</legend>
              <label className="design-field">
                <span>New height (m)</span>
                <input
                  type="number"
                  min={1}
                  max={6}
                  step={0.05}
                  value={ceilingHeightInput}
                  onChange={(e) => setCeilingHeightInput(e.target.value)}
                  aria-label="New ceiling height in meters"
                />
              </label>
              <button
                type="button"
                className="tool-btn"
                onClick={addCeilingHeightEdit}
                disabled={
                  !Number.isFinite(Number(ceilingHeightInput)) ||
                  Number(ceilingHeightInput) <= 0
                }
              >
                Add ceiling-height change
              </button>
            </fieldset>

            {/* Current edits list */}
            <h4 className="design-edit-list-head">Current edits ({edits.length})</h4>
            {edits.length === 0 && <div className="meta">No edits yet.</div>}
            <ul className="design-list">
              {edits.map((e, i) => (
                <li key={e.id ?? i}>
                  <span className="meta">
                    {summarizeEdit(e, {
                      materialName: (id) => materialNameById.get(id),
                      targetLabel: (id) => wallLabelById.get(id),
                    })}
                  </span>
                  <button
                    type="button"
                    className="tool-btn small danger"
                    onClick={() => deleteEdit(i)}
                    aria-label={`Delete edit: ${summarizeEdit(e, {
                      materialName: (id) => materialNameById.get(id),
                      targetLabel: (id) => wallLabelById.get(id),
                    })}`}
                  >
                    ✕
                  </button>
                </li>
              ))}
            </ul>
          </div>

          {/* Items list */}
          <div className="card design-panel">
            <h3>Furniture ({items.length})</h3>
            {items.length === 0 && <div className="meta">No items yet.</div>}
            <ul className="design-list">
              {items.map((it) => (
                <li key={it.id}>
                  <button
                    type="button"
                    className={
                      it.id === selectedItemId ? "design-list-pick active" : "design-list-pick"
                    }
                    onClick={() => setSelectedItemId(it.id)}
                  >
                    {it.label} · {it.x.toFixed(2)},{it.y.toFixed(2)}{" "}
                    {it.catalogItemId ? "" : "(generic)"}
                  </button>
                  <button
                    type="button"
                    className="tool-btn small danger"
                    onClick={() => deleteItem(it.id)}
                    aria-label={`Delete item ${it.label}`}
                  >
                    ✕
                  </button>
                </li>
              ))}
            </ul>
          </div>

          {/* Live material-cost estimate from the assigned wall finishes */}
          <div className="card design-panel design-cost">
            <div className="project-card-head">
              <h3>Estimated cost</h3>
              <span className="pill pill-positive">{formatUsd(cost.total)}</span>
            </div>
            {cost.lines.length === 0 ? (
              <div className="meta">
                Assign a wall finish (Edits → Change wall finish) to see a material estimate.
              </div>
            ) : (
              <>
                <ul className="design-list">
                  {cost.lines.map((l, i) => (
                    <li key={`${l.wallId}-${i}`}>
                      <span className="meta">
                        {l.wallId} · {l.materialName} — {l.quantity} {l.unitLabel}
                      </span>
                      <span className="meta">{formatUsd(l.subtotal)}</span>
                    </li>
                  ))}
                </ul>
                <div className="meta" style={{ marginTop: 8 }}>
                  Materials {formatUsd(cost.subtotal)} + {Math.round(cost.wasteFactor * 100)}% waste ·{" "}
                  <span className="pill" style={{ color: "var(--advisory)" }}>advisory — material only, not a quote</span>
                </div>
              </>
            )}
          </div>
        </aside>
      </div>

      {/* ---------- Payload panel ---------- */}
      <section className="design-payload">
        <div className="design-payload-head">
          <h3>Payload preview</h3>
          <div className="design-payload-actions">
            <button type="button" className="tool-btn" onClick={copyJson}>
              Copy JSON
            </button>
            <button type="button" className="tool-btn" onClick={downloadJson}>
              Download .json
            </button>
          </div>
        </div>
        <textarea
          className="design-json"
          readOnly
          value={bundleJson}
          aria-label="Project payload JSON preview"
          rows={16}
        />
      </section>

      {/* ---------- Publish ---------- */}
      <section className="card design-publish">
        <h3>Publish</h3>
        {!authChecked && <div className="meta">Checking sign-in…</div>}

        {authChecked && !userId && (
          <div>
            <p className="meta">
              Publishing writes the locked project to Supabase, which requires
              sign-in (row-level security: owner_id = your user). Enter your email
              for a magic link, click it in your inbox, then return to this page
              and publish.
            </p>
            <label className="design-field">
              <span>Email</span>
              <input
                type="email"
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                placeholder="you@example.com"
                aria-label="Email for magic link"
              />
            </label>
            <button type="button" className="tool-btn active" onClick={sendMagicLink}>
              Send magic link
            </button>
            {magicSent && (
              <div className="meta" style={{ marginTop: 8 }}>
                Magic link sent to {email.trim()}. Click it, then return here.
              </div>
            )}
          </div>
        )}

        {authChecked && userId && !publishedProjectId && (
          <div>
            <p className="meta">Signed in. Ready to publish this design.</p>
            <button
              type="button"
              className="tool-btn active"
              onClick={publish}
              disabled={publishing}
            >
              {publishing ? "Publishing…" : "Publish to AR"}
            </button>
          </div>
        )}

        {publishedProjectId && (
          <div className="card design-success">
            <h3>Published ✓</h3>
            <div className="meta">
              Project id: <code>{publishedProjectId}</code> — status{" "}
              <span className="pill">ready_for_ar</span>. The AR app can now load
              this locked payload.
            </div>
          </div>
        )}

        {publishError && (
          <div className="card design-error">
            <h3>Couldn’t publish</h3>
            <div className="meta">{publishError}</div>
          </div>
        )}
      </section>
    </main>
  );
}
