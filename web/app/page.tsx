export default function Home() {
  return (
    <main>
      <p className="subtle">
        The companion to the AR app. It reads the same Supabase backend, so what
        an agent captures on a headset shows up here on the web — and what you
        configure here (catalog, pricing) flows back to the glasses.
      </p>

      <nav className="tiles">
        <a className="card" href="/catalog">
          <h3>Materials &amp; finishes →</h3>
          <div className="meta">Browse the finish catalog with brands, colors, and pricing.</div>
        </a>
        <a className="card" href="/design">
          <h3>Design surface →</h3>
          <div className="meta">Draw a top-down plan (walls + furniture) and publish it as the locked project the AR app reads. <span className="pill">publish requires sign-in</span></div>
        </a>
        <div className="card">
          <h3>Saved rooms &amp; estimates</h3>
          <div className="meta">Measurements + cost estimates from walkthroughs. <span className="pill">requires sign-in</span></div>
        </div>
        <div className="card">
          <h3>Staging &amp; renovation plans</h3>
          <div className="meta">Layouts and renovation edits saved from the AR session. <span className="pill">coming soon</span></div>
        </div>
      </nav>
    </main>
  );
}
