import "./globals.css";
import Link from "next/link";
import type { Metadata, Viewport } from "next";

export const metadata: Metadata = {
  title: "SuperRealEstate",
  description:
    "Web companion for the SuperRealEstate AR app — browse the finish catalog and view saved rooms, estimates, and renovation projects from the same Supabase backend.",
};

// Next 14+ viewport API (not the deprecated metadata.viewport).
export const viewport: Viewport = {
  width: "device-width",
  initialScale: 1,
  themeColor: "#0e1116",
};

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="en">
      <body>
        <header className="site">
          <Link href="/" className="brand">SuperRealEstate</Link>
          <nav aria-label="Primary">
            <Link href="/">Home</Link>
            <Link href="/catalog">Catalog</Link>
            <Link href="/design">Design</Link>
            <Link href="/projects">Projects</Link>
          </nav>
        </header>
        <div className="container">{children}</div>
      </body>
    </html>
  );
}
