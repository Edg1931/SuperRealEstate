import "./globals.css";
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
        <div className="container">
          <header className="site">
            <h1>SuperRealEstate</h1>
            <span className="tag">web companion</span>
          </header>
          {children}
        </div>
      </body>
    </html>
  );
}
