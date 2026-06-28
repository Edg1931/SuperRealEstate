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
import type { Point2 } from "@/lib/projectPayload";

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

type Mode = "wall" | "furniture";

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

  // ----- Furniture picker -----
  const [catalog, setCatalog] = useState<CatalogItem[]>([]);
  const [catalogError, setCatalogError] = useState<string | null>(null);
  const [catalogLoading, setCatalogLoading] = useState(true);
  const [pickedCatalogId, setPickedCatalogId] = useState<string>(""); // "" = generic
  const [genericLabel, setGenericLabel] = useState("Box");

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

  const bundle = useMemo(() => toProjectBundle(walls, items), [walls, items]);
  const bundleJson = useMemo(() => JSON.stringify(bundle, null, 2), [bundle]);

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
    [mode, pendingStart, clampToExtent, eventToPlan, pickedCatalogId, catalogById, genericLabel],
  );

  // --- Mutators ---
  const deleteWall = useCallback((id: string) => {
    setWalls((prev) => prev.filter((w) => w.id !== id));
  }, []);

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
    setPendingStart(null);
    setSelectedItemId(null);
  }, []);

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
              {gridLines}

              {/* origin marker */}
              <circle cx={origin.sx} cy={origin.sy} r={4} className="design-origin" />
              <text x={origin.sx + 6} y={origin.sy - 6} className="design-axis-label">
                0,0
              </text>

              {/* existing walls */}
              {walls.map((w) => {
                const a = planToScreen(w.start);
                const b = planToScreen(w.end);
                return (
                  <line
                    key={w.id}
                    x1={a.sx}
                    y1={a.sy}
                    x2={b.sx}
                    y2={b.sy}
                    className="design-wall"
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
