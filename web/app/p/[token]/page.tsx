"use client";

// Public, chrome-light presentation page. NO auth: resolves a share token via
// the PUBLIC `get_shared_project` RPC (SECURITY DEFINER — the unguessable token
// is the capability). This is what a buyer opens on their phone, so it stays
// light: project name, a large read-only plan, and an advisory cost estimate.

import { useCallback, useEffect, useMemo, useState } from "react";
import Link from "next/link";
import { useParams } from "next/navigation";
import { supabase } from "@/lib/supabase";
import PlanView, { type PlanGeometry, type PlanPlacement } from "@/app/_components/PlanView";
import { parseGeometryWalls } from "@/lib/blueprint";
import { estimateFinishCost, formatUsd, type CostMaterial } from "@/lib/costEstimate";
import type { RenovationEdit } from "@/lib/projectPayload";
import type { DesignWall } from "@/lib/design";

// The shape `get_shared_project` returns (jsonb). Tolerant — every field may be
// absent / null on malformed data.
interface SharedProject {
  name?: string | null;
  kind?: string | null;
  status?: string | null;
  geometry?: PlanGeometry | null;
  placements?: PlanPlacement[] | null;
  edits?: RenovationEdit[] | null;
}

interface MaterialRow {
  id: string;
  name: string;
  price_per_unit: number | null;
  unit: string | null;
}

type LoadState = "loading" | "loaded" | "unavailable" | "error";

export default function PublicProjectPage(): React.ReactElement {
  const params = useParams<{ token: string }>();
  const token = typeof params?.token === "string" ? params.token : "";

  const [shared, setShared] = useState<SharedProject | null>(null);
  const [materials, setMaterials] = useState<MaterialRow[]>([]);
  const [loadState, setLoadState] = useState<LoadState>("loading");
  const [loadError, setLoadError] = useState<string | null>(null);

  const load = useCallback(async () => {
    if (!token) {
      setLoadState("unavailable");
      return;
    }
    setLoadState("loading");
    setLoadError(null);

    // Resolve the token + fetch the public materials catalog in parallel.
    const [rpcRes, matRes] = await Promise.all([
      supabase.rpc("get_shared_project", { p_token: token }),
      supabase.from("materials").select("id,name,price_per_unit,unit"),
    ]);

    if (rpcRes.error) {
      setLoadError(rpcRes.error.message);
      setLoadState("error");
      return;
    }

    const payload = rpcRes.data as SharedProject | null;
    if (!payload) {
      setLoadState("unavailable");
      return;
    }

    setShared(payload);
    setMaterials((matRes.data as MaterialRow[] | null) ?? []);
    setLoadState("loaded");
  }, [token]);

  useEffect(() => {
    let active = true;
    (async () => {
      if (active) await load();
    })();
    return () => {
      active = false;
    };
  }, [load]);

  const geometry = shared?.geometry ?? null;
  const placements = useMemo<PlanPlacement[]>(
    () => (Array.isArray(shared?.placements) ? (shared?.placements ?? []) : []),
    [shared],
  );
  const edits = useMemo<RenovationEdit[]>(
    () => (Array.isArray(shared?.edits) ? (shared?.edits ?? []) : []),
    [shared],
  );

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

  return (
    <main className="present">
      {loadState === "loading" && <p className="subtle">Loading…</p>}

      {(loadState === "unavailable" || loadState === "error") && (
        <div className="card present-card" style={{ maxWidth: 460, margin: "48px auto" }}>
          <h3>This link isn’t available</h3>
          <p className="meta">
            {loadState === "error"
              ? "Something went wrong loading this project. Please try again later."
              : "This share link is no longer active, or it was never shared. Ask whoever sent it for an up-to-date link."}
          </p>
          <p className="meta" style={{ marginTop: 12 }}>
            <Link href="/">Go to SuperRealEstate →</Link>
          </p>
        </div>
      )}

      {loadState === "loaded" && shared && (
        <article className="present-shell">
          <header className="present-head">
            <h1 className="present-title">{shared.name?.trim() || "Project preview"}</h1>
            {shared.kind && <span className="pill present-kind">{shared.kind}</span>}
          </header>

          <section className="present-plan card">
            <PlanView
              geometry={geometry}
              placements={placements}
              ariaLabel={`Floor plan of ${shared.name ?? "this project"}`}
            />
          </section>

          {cost.lines.length > 0 && (
            <section className="present-cost card">
              <div className="project-card-head">
                <h3>Estimated finishes</h3>
                <span className="pill pill-positive">{formatUsd(cost.total)}</span>
              </div>
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
                Includes {Math.round(cost.wasteFactor * 100)}% waste ·{" "}
                <span className="pill" style={{ color: "var(--advisory)" }}>advisory — material only, not a quote</span>
              </div>
            </section>
          )}

          <footer className="present-footer subtle">
            Made with <Link href="/">SuperRealEstate</Link>
          </footer>
        </article>
      )}
    </main>
  );
}
