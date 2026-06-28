import Link from "next/link";

export default function NotFound() {
  return (
    <main>
      <div className="card">
        <h3>404 — page not found</h3>
        <div className="meta">
          That page doesn’t exist in the web companion. It might live on the
          headset, or the link may be out of date.
        </div>
        <p style={{ marginTop: 12 }}>
          <Link href="/">← Back home</Link>
        </p>
      </div>
    </main>
  );
}
