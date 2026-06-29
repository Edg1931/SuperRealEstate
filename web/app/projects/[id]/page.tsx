"use client";

// Owner project detail. Requires sign-in (RLS scopes the project row to the
// owner). Shows the plan (read-only PlanView), an advisory finish-cost estimate
// (reusing lib/costEstimate), and owner actions: share / unshare a public link,
// and re-open the geometry in the design surface for further editing.

import { useCallback, useEffect, useMemo, useState } from "react";
import Link from "next/link";
import { useParams } from "next/navigation";
import { supabase } from "@/lib/supabase";
import { useAuth } from "@/lib/useAuth";
import PlanView, { type PlanGeometry, type PlanPlacement } from "@/app/_components/PlanView";
import { parseGeometryWalls } from "@/lib/blueprint";
import { estimateFinishCost, formatUsd, type CostMaterial } from "@/lib/costEstimate";
import type { RenovationEdit } from "@/lib/projectPayload";
import type { DesignWall } from "@/lib/design";

// sessionStorage key the design surface reads to re-open a saved geometry.
// (Local const — App Router page files may only export the default component
// + Next's reserved exports; the design page reads the same literal string.)
const REOPEN_GEOMETRY_KEY = "sre.reopen.geometry";

interface ProjectRow {
  id: string;
  name: string | null;
  kind: string | null;
  status: string | null;
  share_token: string | null;
  building_model_id: string | null;
  staging_layout_id: string | null;
  renovation_plan_id: string | null;
}

interface MaterialRow {
  id: string;
  name: string;
  brand: string | null;
  category: string | null;
  price_per_unit: number | null;
  unit: string | null;
}

function statusPillClass(status: string | null): string {
  switch (status) {
    case "ready_for_ar":
      return "pill pill-positive";
    case "designed":
      return "pill pill-accent";
    case "archived":
      return "pill pill-subtle";
    default:
      return "pill pill-muted";
  }
}

type LoadState = "loading" | "loaded" | "error" | "not_found";

export default function ProjectDetailPage(): React.ReactElement {
  const params = useParams<{ id: string }>();
  const projectId = typeof params?.id === "string" ? params.id : "";
  const { userId, authChecked } = useAuth();

  const [project, setProject] = useState<ProjectRow | null>(null);
  const [geometry, setGeometry] = useState<PlanGeometry | null>(null);
  const [placements, setPlacements] = useState<PlanPlacement[]>([]);
  const [edits, setEdits] = useState<RenovationEdit[]>([]);
  const [materials, setMaterials] = useState<MaterialRow[]>([]);
  const [loadState, setLoadState] = useState<LoadState>("loading");
  const [loadError, setLoadError] = useState<string | null>(null);

  // Share state.
  const [shareToken, setShareToken] = useState<string | null>(null);
  const [shareBusy, setShareBusy] = useState(false);
  const [shareError, setShareError] = useState<string | null>(null);
  const [copied, setCopied] = useState(false);

  // Re-open feedback.
  const [reopenError, setReopenError] = useState<string | null>(null);

  // Fetch the project + its attached artifacts once we have a signed-in user.
  useEffect(() => {
    if (!authChecked) return;
    if (!userId) {
      setLoadState("loaded"); // render the sign-in prompt
      return;
    }
    if (!projectId) {
      setLoadState("not_found");
      return;
    }

    let active = true;
    setLoadState("loading");
    setLoadError(null);

    (async () => {
      // 1) The project row (RLS scopes to owner).
      const { data: proj, error: projErr } = await supabase
        .from("renovation_projects")
        .select(
          "id,name,kind,status,share_token,building_model_id,staging_layout_id,renovation_plan_id",
        )
        .eq("id", projectId)
        .maybeSingle();

      if (!active) return;
      if (projErr) {
        setLoadError(projErr.message);
        setLoadState("error");
        return;
      }
      if (!proj) {
        setLoadState("not_found");
        return;
      }

      const row = proj as ProjectRow;
      setProject(row);
      setShareToken(row.share_token);

      // 2..4) Geometry, placements, edits, plus the materials catalog — in
      // parallel (each is independent of the others).
      const [geomRes, placeRes, planRes, matRes] = await Promise.all([
        row.building_model_id
          ? supabase.from("building_models").select("geometry").eq("id", row.building_model_id).maybeSingle()
          : Promise.resolve({ data: null, error: null }),
        row.staging_layout_id
          ? supabase
              .from("staging_placements")
              .select("id,plan_x,plan_y,plan_yaw_deg,scale,catalog_item_id")
              .eq("layout_id", row.staging_layout_id)
          : Promise.resolve({ data: [], error: null }),
        row.renovation_plan_id
          ? supabase.from("renovation_plans").select("edits").eq("id", row.renovation_plan_id).maybeSingle()
          : Promise.resolve({ data: null, error: null }),
        supabase.from("materials").select("id,name,brand,category,price_per_unit,unit"),
      ]);

      if (!active) return;

      const geom = (geomRes.data as { geometry?: PlanGeometry } | null)?.geometry ?? null;
      setGeometry(geom);
      setPlacements((placeRes.data as PlanPlacement[] | null) ?? []);
      const planEdits = (planRes.data as { edits?: RenovationEdit[] } | null)?.edits;
      setEdits(Array.isArray(planEdits) ? planEdits : []);
      setMaterials((matRes.data as MaterialRow[] | null) ?? []);
      setLoadState("loaded");
    })();

    return () => {
      active = false;
    };
  }, [authChecked, userId, projectId]);

  // Derive DesignWall[] from the geometry so the cost helper (which works on
  // DesignWall) can price the assigned wall finishes. Tolerant: skips gracefully.
  const walls = useMemo<DesignWall[]>(() => {
    if (!geometry || !Array.isArray(geometry.walls) || geometry.walls.length === 0) return [];
    try {
      return parseGeometryWalls(geometry);
    } catch {
      return [];
    }
  }, [geometry]);

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

  const shareUrl = useMemo(() => {
    if (!shareToken || typeof window === "undefined") return null;
    return `${window.location.origin}/p/${shareToken}`;
  }, [shareToken]);

  const enableSharing = useCallback(async () => {
    setShareError(null);
    setShareBusy(true);
    try {
      const { data, error } = await supabase.rpc("enable_project_sharing", { p_project: projectId });
      if (error) throw new Error(error.message);
      const token = typeof data === "string" ? data : null;
      if (!token) throw new Error("Sharing didn't return a link token.");
      setShareToken(token);
    } catch (e) {
      setShareError(e instanceof Error ? e.message : "Couldn't enable sharing.");
    } finally {
      setShareBusy(false);
    }
  }, [projectId]);

  const disableSharing = useCallback(async () => {
    setShareError(null);
    setShareBusy(true);
    try {
      const { error } = await supabase.rpc("disable_project_sharing", { p_project: projectId });
      if (error) throw new Error(error.message);
      setShareToken(null);
      setCopied(false);
    } catch (e) {
      setShareError(e instanceof Error ? e.message : "Couldn't disable sharing.");
    } finally {
      setShareBusy(false);
    }
  }, [projectId]);

  const copyShareLink = useCallback(async () => {
    if (!shareUrl) return;
    try {
      await navigator.clipboard.writeText(shareUrl);
      setCopied(true);
      window.setTimeout(() => setCopied(false), 1800);
    } catch {
      // Clipboard may be blocked; the input is still selectable.
    }
  }, [shareUrl]);

  // Hand the geometry to /design via sessionStorage, then navigate. We use a
  // full navigation so the design page re-mounts and reads the key on mount.
  const reopenInDesign = useCallback(() => {
    setReopenError(null);
    if (!geometry || !Array.isArray(geometry.walls) || geometry.walls.length === 0) {
      setReopenError("This project has no editable wall geometry.");
      return;
    }
    try {
      window.sessionStorage.setItem(REOPEN_GEOMETRY_KEY, JSON.stringify(geometry));
      window.location.assign("/design");
    } catch {
      setReopenError("Couldn't open this geometry in the design surface.");
    }
  }, [geometry]);

  // ---- Render --------------------------------------------------------------
  return (
    <main>
      <p>
        <Link href="/projects">← Back to projects</Link>
      </p>

      {!authChecked && <p className="subtle">Checking sign-in…</p>}

      {authChecked && !userId && (
        <div className="card" style={{ maxWidth: 460 }}>
          <h3>Sign in to view this project</h3>
          <p className="meta">
            Projects are private (row-level security: owner_id = your user). Sign in
            from the <Link href="/projects">projects page</Link>, then return here.
          </p>
        </div>
      )}

      {authChecked && userId && loadState === "loading" && <p className="subtle">Loading project…</p>}

      {authChecked && userId && loadState === "error" && (
        <div className="card design-error">
          <h3>Couldn’t load this project</h3>
          <div className="meta">{loadError}</div>
        </div>
      )}

      {authChecked && userId && loadState === "not_found" && (
        <div className="card">
          <h3>Project not found</h3>
          <p className="meta">
            This project doesn’t exist or isn’t yours.{" "}
            <Link href="/projects">Back to your projects →</Link>
          </p>
        </div>
      )}

      {authChecked && userId && loadState === "loaded" && project && (
        <>
          <div className="project-card-head" style={{ marginBottom: 10 }}>
            <h2 style={{ margin: 0 }}>{project.name?.trim() || "Untitled"}</h2>
            <span className={statusPillClass(project.status)}>{project.status ?? "unknown"}</span>
          </div>
          <p className="meta" style={{ marginTop: 0 }}>
            {project.kind ?? "—"}
            {shareToken ? " · shared" : ""}
          </p>

          <div className="detail-grid">
            {/* Plan */}
            <section className="card">
              <h3>Plan</h3>
              <PlanView geometry={geometry} placements={placements} ariaLabel={`Plan of ${project.name ?? "project"}`} />
            </section>

            {/* Cost + actions */}
            <aside className="detail-side">
              <section className="card design-cost">
                <div className="project-card-head">
                  <h3>Estimated cost</h3>
                  <span className="pill pill-positive">{formatUsd(cost.total)}</span>
                </div>
                {cost.lines.length === 0 ? (
                  <div className="meta">No priced wall finishes in this project.</div>
                ) : (
                  <>
                    <ul className="design-list">
                      {cost.lines.map((l, i) => (
                        <li key={`${l.wallId}-${i}`}>
                          <span className="meta">
                            {l.materialName} — {l.quantity} {l.unitLabel}
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
              </section>

              {/* Share */}
              <section className="card">
                <h3>Share with a client</h3>
                <p className="meta">
                  Create a public, read-only link (no sign-in) to this project’s plan
                  and estimate. Anyone with the link can view it; turning sharing off
                  invalidates it.
                </p>

                {shareToken && shareUrl ? (
                  <>
                    <div className="share-link-row">
                      <input
                        type="text"
                        className="share-link-input"
                        readOnly
                        value={shareUrl}
                        aria-label="Public share link"
                        onFocus={(e) => e.currentTarget.select()}
                      />
                      <button type="button" className="tool-btn" onClick={copyShareLink}>
                        {copied ? "Copied ✓" : "Copy"}
                      </button>
                    </div>
                    <div className="design-toolbar" style={{ marginTop: 8 }}>
                      <a className="tool-btn" href={shareUrl} target="_blank" rel="noreferrer">
                        Open link ↗
                      </a>
                      <button type="button" className="tool-btn danger" onClick={disableSharing} disabled={shareBusy}>
                        {shareBusy ? "Working…" : "Stop sharing"}
                      </button>
                    </div>
                  </>
                ) : (
                  <button type="button" className="tool-btn active" onClick={enableSharing} disabled={shareBusy}>
                    {shareBusy ? "Creating link…" : "Create share link"}
                  </button>
                )}

                {shareError && (
                  <div className="meta" style={{ marginTop: 8, color: "var(--danger)" }}>
                    {shareError}
                  </div>
                )}
              </section>

              {/* Re-open in design surface */}
              <section className="card">
                <h3>Keep editing</h3>
                <p className="meta">
                  Re-open this project’s walls in the design surface to adjust the plan
                  and publish a new version.
                </p>
                <button type="button" className="tool-btn" onClick={reopenInDesign}>
                  Open in design surface →
                </button>
                {reopenError && (
                  <div className="meta" style={{ marginTop: 8, color: "var(--danger)" }}>
                    {reopenError}
                  </div>
                )}
              </section>
            </aside>
          </div>
        </>
      )}
    </main>
  );
}
