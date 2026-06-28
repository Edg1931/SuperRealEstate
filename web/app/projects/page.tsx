"use client";

import { useCallback, useEffect, useState } from "react";
import { supabase } from "@/lib/supabase";
import { useAuth } from "@/lib/useAuth";

// --- Row shape (only the columns we select) --------------------------------
interface ProjectRow {
  id: string;
  name: string | null;
  kind: string | null;
  status: string | null;
  origin: string | null;
  created_at: string | null;
  building_model_id: string | null;
  staging_layout_id: string | null;
  renovation_plan_id: string | null;
}

const SELECT_COLUMNS =
  "id,name,kind,status,origin,created_at,building_model_id,staging_layout_id,renovation_plan_id";

// Map a project status to a pill style variant. Unknown statuses fall back to a
// neutral pill so we never throw on data we don't recognise.
function statusPillClass(status: string | null): string {
  switch (status) {
    case "ready_for_ar":
      return "pill pill-positive";
    case "designed":
      return "pill pill-accent";
    case "archived":
      return "pill pill-subtle";
    case "draft":
    default:
      return "pill pill-muted";
  }
}

const DATE_FMT = new Intl.DateTimeFormat(undefined, {
  year: "numeric",
  month: "short",
  day: "numeric",
});

function formatDate(iso: string | null): string {
  if (!iso) return "—";
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return "—";
  return DATE_FMT.format(d);
}

type LoadState = "idle" | "loading" | "loaded" | "error";

export default function ProjectsPage() {
  const { userId, email, authChecked, sendMagicLink, signOut } = useAuth();

  // Sign-in form state.
  const [emailInput, setEmailInput] = useState("");
  const [magicSent, setMagicSent] = useState(false);
  const [authError, setAuthError] = useState<string | null>(null);

  // Projects fetch state.
  const [projects, setProjects] = useState<ProjectRow[]>([]);
  const [loadState, setLoadState] = useState<LoadState>("idle");
  const [loadError, setLoadError] = useState<string | null>(null);

  // Fetch this user's projects whenever we have a signed-in user. RLS scopes the
  // rows to owner_id = auth.uid() automatically.
  useEffect(() => {
    if (!userId) {
      setProjects([]);
      setLoadState("idle");
      setLoadError(null);
      return;
    }

    let active = true;
    setLoadState("loading");
    setLoadError(null);

    (async () => {
      const { data, error } = await supabase
        .from("renovation_projects")
        .select(SELECT_COLUMNS)
        .order("created_at", { ascending: false });

      if (!active) return;

      if (error) {
        setLoadError(error.message);
        setProjects([]);
        setLoadState("error");
        return;
      }

      setProjects((data as ProjectRow[] | null) ?? []);
      setLoadState("loaded");
    })();

    return () => {
      active = false;
    };
  }, [userId]);

  const onSendMagicLink = useCallback(async () => {
    setAuthError(null);
    setMagicSent(false);
    const result = await sendMagicLink(emailInput);
    if (!result.ok) {
      setAuthError(result.error);
      return;
    }
    setMagicSent(true);
  }, [emailInput, sendMagicLink]);

  return (
    <main>
      <p>
        <a href="/">← Home</a>
      </p>
      <h2>My projects</h2>

      {/* Checking sign-in --------------------------------------------------- */}
      {!authChecked && <p className="subtle">Checking sign-in…</p>}

      {/* Signed out: magic-link sign-in ------------------------------------- */}
      {authChecked && !userId && (
        <div className="card" style={{ maxWidth: 460 }}>
          <h3>Sign in to see your projects</h3>
          <p className="meta">
            Your projects are private (row-level security: owner_id = your user),
            so you need to be signed in to view them. Enter your email for a magic
            link, click it in your inbox, then return to this page.
          </p>
          <label className="design-field">
            <span>Email</span>
            <input
              type="email"
              value={emailInput}
              onChange={(e) => setEmailInput(e.target.value)}
              placeholder="you@example.com"
              aria-label="Email for magic link"
            />
          </label>
          <button type="button" className="tool-btn active" onClick={onSendMagicLink}>
            Send magic link
          </button>
          {magicSent && (
            <div className="meta" style={{ marginTop: 8 }}>
              Magic link sent to {emailInput.trim()}. Click it, then return here.
            </div>
          )}
          {authError && (
            <div className="meta" style={{ marginTop: 8, color: "var(--caution)" }}>
              {authError}
            </div>
          )}
        </div>
      )}

      {/* Signed in ---------------------------------------------------------- */}
      {authChecked && userId && (
        <>
          <div className="projects-bar">
            <span className="subtle">
              Signed in{email ? ` as ${email}` : ""}.
            </span>
            <button type="button" className="tool-btn" onClick={() => void signOut()}>
              Sign out
            </button>
          </div>

          {loadState === "loading" && <p className="subtle">Loading your projects…</p>}

          {loadState === "error" && (
            <div className="card design-error">
              <h3>Couldn’t load your projects</h3>
              <div className="meta">{loadError}</div>
            </div>
          )}

          {loadState === "loaded" && projects.length === 0 && (
            <p className="subtle">
              No projects yet —{" "}
              <a href="/design">create one in the Design surface →</a>
            </p>
          )}

          {loadState === "loaded" && projects.length > 0 && (
            <div className="grid">
              {projects.map((p) => {
                const artifacts: string[] = [];
                if (p.building_model_id) artifacts.push("model ✓");
                if (p.staging_layout_id) artifacts.push("staging ✓");
                if (p.renovation_plan_id) artifacts.push("plan ✓");
                return (
                  <div className="card project-card" key={p.id}>
                    <div className="project-card-head">
                      <h3>{p.name?.trim() || "Untitled"}</h3>
                      <span className={statusPillClass(p.status)}>
                        {p.status ?? "unknown"}
                      </span>
                    </div>
                    <div className="meta">
                      {p.kind ?? "—"}
                      {p.origin ? ` · ${p.origin}` : ""}
                    </div>
                    <div className="meta" style={{ marginTop: 6 }}>
                      Created {formatDate(p.created_at)}
                    </div>
                    <div className="project-artifacts" style={{ marginTop: 8 }}>
                      {artifacts.length > 0 ? (
                        artifacts.map((a) => (
                          <span className="pill pill-subtle" key={a}>
                            {a}
                          </span>
                        ))
                      ) : (
                        <span className="meta">No design artifacts attached yet.</span>
                      )}
                    </div>
                  </div>
                );
              })}
            </div>
          )}
        </>
      )}
    </main>
  );
}
