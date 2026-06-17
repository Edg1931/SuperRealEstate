import "./globals.css";
import type { Metadata } from "next";

export const metadata: Metadata = {
  title: "SuperRealEstate — Web Companion",
  description: "Browse the catalog and view saved rooms and estimates from the AR app.",
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
