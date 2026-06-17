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

## Deploy to Vercel

1. Push this repo to GitHub (already your remote).
2. In Vercel → **New Project** → import the repo.
3. Set **Root Directory = `web`** (the app isn't at the repo root).
4. Add **Environment Variables** (Production + Preview):
   - `NEXT_PUBLIC_SUPABASE_URL`
   - `NEXT_PUBLIC_SUPABASE_ANON_KEY`
5. Deploy. (Or with the CLI: `cd web && vercel` then `vercel --prod`.)

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
