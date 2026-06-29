# Web companion (Vercel)

A Next.js app that displays the SuperRealEstate data on the web. It reads the
**same Supabase backend** the AR app writes to, so:

- Agents capture rooms/finishes/staging on the headset → they show up here.
- You manage the catalog/pricing here → it flows back to the glasses.

> The **AR experience itself runs on the headset** (Unity → Vision Pro / Galaxy
> XR), not in the browser. This web app is the 2D companion: catalog, saved
> rooms + estimates, recaps. (A non-AR Unity **WebGL** 3D viewer could be
> embedded later for in-browser walkthroughs.)

## Run locally

```bash
cd web
npm install
cp .env.local.example .env.local   # fill in your Supabase URL + anon key
npm run dev                         # http://localhost:3000
```

For the public catalog page to load without sign-in, apply the catalog read
policy: `supabase/migrations/0006_public_catalog_read.sql` (and seed the catalog
via `supabase/seed.sql`).

## Design surface (`/design`)

A top-down plan authoring tool (Microsoft-Layout-style). On a PC or phone you:

- **Upload a blueprint** — drop in a floor-plan image (a builder blueprint or a
  CubiCasa / Matterport export, PNG/JPG; PDFs aren't supported — export an image
  first). It renders as a translucent underlay behind the grid/walls/items
  (opacity slider, **Remove blueprint** to clear). It's a tracing aid only and is
  **never** part of the published payload.
- **Calibrate scale** — with the **Calibrate scale** tool, click two points on a
  feature whose real length you know, then enter that length (ft or m) and **Set
  scale**. The blueprint is rescaled about your first click so anything traced
  over it is to true real-world scale. By default already-drawn walls are left
  unchanged (calibrate before tracing); tick *Also rescale already-drawn walls*
  to bring them along. Math: `dPlan = hypot(p2 − p1)`, `dReal` = entered value in
  meters (ft × 0.3048), factor `f = dReal / dPlan`; the underlay's size scales by
  `f` and its top-left moves so the point under p1 stays fixed.
- **Import floor plan (JSON)** — load a building-model geometry JSON (the
  `{ walls:[{ start, end, thicknessM, heightM, isExterior }], … }` shape stored in
  `building_models.geometry`). Its walls become editable segments — choose
  **replace** or **append** when walls already exist; missing fields fall back to
  defaults (thickness 0.1 m, height 2.5 m, exterior). Malformed JSON shows an
  inline error card.
- **Draw walls** — click to drop a start point, click again to finish a segment
  (snaps to a 0.25 m grid). Trace over a calibrated blueprint so the published
  geometry matches the real house (and the AR walk).
- **Place furniture** — pick a vendor catalog item (loaded from
  `vendor_catalog_items`) or a generic labelled box, then click on the canvas;
  select a placed item to rotate (yaw) and scale.
- **Preview the payload** — a live JSON panel shows the exact bundle
  (`{ geometry, placements, edits }`), with **Copy JSON** / **Download .json**.
- **Publish** — writes the *locked* project payload the AR app reads.

Publishing **requires sign-in** (Supabase Auth magic link) because every table is
RLS-protected (`owner_id = auth.uid()`). The relevant migrations must be applied:
`building_models`, `staging_layouts`, `staging_placements`, `renovation_plans`,
`renovation_projects`, and `vendors` / `vendor_catalog_items`.

On publish, rows are inserted in this order, threading the returned ids:
`building_models` → `staging_layouts` → `staging_placements`
(→ `renovation_plans` only if there are edits) → `renovation_projects`
(`origin = manual_desktop`, `status = ready_for_ar`). Furniture is written as
**blueprint-authored** placements (`plan_x` / `plan_y` / `plan_yaw_deg`, `scale`,
`hasPlan = true`); `pos_*` are left at 0. Geometry conforms to the
`BuildingModelParser` contract (plan-view meters, x = east, y = north).

The pure serialization lives in `lib/design.ts` and reuses the payload types in
`lib/projectPayload.ts` — keep both in sync with the C# `ProjectPayloadParser`.
The blueprint calibration math (`metersFrom`, `scaleFactor`, `rescaleUnderlay`,
`rescaleWall`) and geometry import (`parseGeometryWalls`) live as pure,
DOM-free helpers in `lib/blueprint.ts`.

## My projects (`/projects`)

A signed-in account page listing your `renovation_projects` (newest first), each
card showing status (colour-hinted pill), kind + origin, created date, and which
design artifacts are attached (`model` / `staging` / `plan`). It **requires
sign-in** (Supabase Auth magic link) and relies on RLS (`owner_id = auth.uid()`)
to scope rows to you. The reusable auth hook lives in `lib/useAuth.ts`.

## Deploy to Vercel

1. Push this repo to GitHub (already your remote).
2. In Vercel → **New Project** → **Import** the repo.
3. Set **Root Directory = `web`** (the app isn't at the repo root). Vercel then
   **auto-detects Next.js** as the framework preset — leave the build/output/
   install commands at their defaults (this app's `web/vercel.json` already
   pins `framework: nextjs` and the standard commands for determinism).
4. Add **Environment Variables** (apply to Production **and** Preview):
   - `NEXT_PUBLIC_SUPABASE_URL` — Supabase → Project → **Settings → API → Project URL**
   - `NEXT_PUBLIC_SUPABASE_ANON_KEY` — same page, **Project API keys → `anon` / `public`**

   These are the only env vars needed. The Anthropic key never goes here
   (see below). Until both are set, every page renders an inline
   "Supabase isn’t configured" banner instead of failing silently.
5. **Deploy.** (Or with the CLI: `cd web && vercel` then `vercel --prod`.)

> **Supabase prerequisites:** the linked project must have the migrations
> applied. For the public `/catalog` page to load without sign-in, enable
> public read via `supabase/migrations/0006_public_catalog_read.sql` and seed
> the catalog with `supabase/seed.sql`. Sign-in-gated pages (`/design`
> publish, `/projects`) rely on the RLS-protected tables listed above.

## Where the Anthropic API key goes

**Not here.** The Anthropic key is server-side only and lives as a **Supabase
Edge Function secret** (used by `scene-insights` and `plant-id`), so both the AR
app and this web app share one proxy and the key never ships to a browser:

```bash
supabase secrets set ANTHROPIC_API_KEY=sk-ant-...   # get the key at console.anthropic.com
```

Only add `ANTHROPIC_API_KEY` to Vercel env vars if you later build a Next.js
**server route** that calls Claude directly — but the Supabase-secret approach is
recommended (one key, one place).

## Notes

- The anon key is **public by design** — row-level security protects the data,
  and private tables require the user to be signed in (Supabase Auth, to add).
- This app uses the Next.js App Router with server components; the catalog page
  uses ISR (`revalidate = 60`).
