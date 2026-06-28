// Presentational banner shown when the public Supabase env vars are missing.
// Server Component (no client hooks) — pages render it when
// isSupabaseConfigured() is false so a fresh deploy explains itself.
export default function EnvNotice() {
  return (
    <div className="card env-notice" role="status">
      <h3>Supabase isn’t configured</h3>
      <div className="meta">
        Set <code>NEXT_PUBLIC_SUPABASE_URL</code> and{" "}
        <code>NEXT_PUBLIC_SUPABASE_ANON_KEY</code> in your Vercel project
        (Settings → Environment Variables), then redeploy. Until then,
        catalog and saved data can’t load.
      </div>
    </div>
  );
}
