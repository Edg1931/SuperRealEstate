import { supabase, type MaterialRow } from "@/lib/supabase";

export const revalidate = 60; // ISR: refresh catalog each minute

function unitLabel(unit: string): string {
  switch (unit) {
    case "per_sqft": return "/ ft²";
    case "per_sqm": return "/ m²";
    case "per_linft": return "/ lin ft";
    case "per_gallon": return "/ gal";
    default: return "each";
  }
}

export default async function CatalogPage() {
  const { data, error } = await supabase
    .from("materials")
    .select("id,name,category,unit,price_per_unit,brand,product_code,color_hex,buy_url")
    .order("category", { ascending: true })
    .order("name", { ascending: true });

  const rows = (data as MaterialRow[] | null) ?? [];

  return (
    <main>
      <p><a href="/">← Home</a></p>
      <h2>Materials &amp; finishes</h2>

      {error && (
        <div className="card">
          <h3>Couldn’t load the catalog</h3>
          <div className="meta">
            {error.message}. Check the Supabase env vars, and that public read is
            enabled (run migration <code>0006_public_catalog_read.sql</code>) or
            that you’re signed in.
          </div>
        </div>
      )}

      {!error && rows.length === 0 && (
        <p className="subtle">No materials yet — seed the catalog (see <code>supabase/seed.sql</code>).</p>
      )}

      <div className="grid">
        {rows.map((m) => (
          <div className="card" key={m.id}>
            <h3>
              {m.color_hex && <span className="swatch" style={{ background: m.color_hex }} />}
              {m.name}
            </h3>
            <div className="meta">
              {m.brand ? `${m.brand} · ` : ""}{m.product_code ?? m.category}
            </div>
            <div style={{ marginTop: 8 }}>
              <span className="price">${m.price_per_unit?.toFixed(2)}</span>{" "}
              <span className="meta">{unitLabel(m.unit)}</span>
            </div>
            {m.buy_url && <div style={{ marginTop: 8 }}><a href={m.buy_url} target="_blank" rel="noreferrer">Buy →</a></div>}
          </div>
        ))}
      </div>
    </main>
  );
}
