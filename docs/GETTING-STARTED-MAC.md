# Getting started on a Mac

A practical path to view where the project is. Ordered easiest → heaviest, with
what each step actually shows you.

> **Set expectations first.** Today the repo is **tested logic + backend + a web
> companion + the AR architecture/contracts** — not yet a finished AR app you can
> put on the Vision Pro. The fastest way to *see* progress is the **web companion**
> and the **Unity test suite**. The on-device AR scene (UI prefabs, overlays) is
> the in-editor work still flagged in `ROADMAP.md`.

## 0. Get the code

```bash
git clone https://github.com/Edg1931/superrealestate.git
cd superrealestate
git checkout claude/ar-realtor-tools-n8pkqg   # everything lives on this branch
```
(Already cloned? `git fetch origin && git checkout claude/ar-realtor-tools-n8pkqg && git pull`.)

## 1. Fastest visual — the web companion (5 min)

Prereq: Node 18+ (`brew install node`).

```bash
cd web
npm install
cp .env.local.example .env.local     # leave blank for now to just see the shell
npm run dev                          # open http://localhost:3000
```
You'll see the dashboard immediately. The **catalog page** populates once Supabase
is set up (step 3). Without it, the page loads and shows a friendly "couldn't load
catalog" note — that's expected.

## 2. See the tested logic — Unity (15 min)

This proves the measurement / cost / fit / blueprint / finish-matching / landscape
/ UI math without a headset.

1. Install **Unity Hub**, then **Unity 6 LTS** (6000.0.x) with **visionOS** and
   **iOS** Build Support checked.
2. Unity Hub → **Add** → select the repo root (the folder containing `Assets/`,
   `Packages/`, `ProjectSettings/`). Open it.
3. First open resolves packages (AR Foundation, PolySpatial, …) — a few minutes.
   If a package version errors, it's the placeholder versions in
   `Packages/manifest.json` — ping me and I'll pin them to your installed Unity.
4. **Window → General → Test Runner → EditMode → Run All.** You should see the
   suites pass (measurement, cost, fit, blueprint, finish quantity/matcher,
   renovation, landscape, spatial UI, presentation). That green is "where we are."

## 3. Make data + AI live — Supabase (20 min)

Prereq: a free project at supabase.com; `brew install supabase/tap/supabase`.

```bash
supabase login
supabase link --project-ref <your-project-ref>
supabase db push                         # applies migrations 0001..0006
# seed the catalog: paste supabase/seed.sql into the Supabase SQL editor and run

# AI features (scene insights + plant ID):
supabase secrets set ANTHROPIC_API_KEY=sk-ant-...   # from console.anthropic.com
supabase functions deploy scene-insights
supabase functions deploy plant-id
```
Then put your **Project URL** + **anon key** (Supabase → Project Settings → API)
into `web/.env.local` and restart `npm run dev` — the catalog page now fills in.

## 4. The full AR build — Vision Pro (heaviest)

Prereq: **Xcode** (App Store) + an Apple developer account.

1. In Unity: switch platform to **visionOS**; **XR Plug-in Management → Apple
   visionOS**; enable **PolySpatial**. Full steps in `docs/VisionOS-Setup.md`.
2. **Build** → produces an Xcode project → open in Xcode → sign with your team →
   run on the **Vision Pro** (or the visionOS simulator for UI only).

> Reality check: there isn't a built AR scene yet, so this step currently runs
> the project skeleton, not a finished walkthrough. It's the next chunk of work
> (Phase 1 spatial UI + scene wiring). Steps 1–3 are where you'll actually *see*
> the current state.

## Recommended first session

1. Step 1 (web shell) → 2. Step 2 (run the tests) → 3. Step 3 (Supabase + seed) →
   reload the web catalog. That gives you a real, clickable view of the catalog
   and a green test suite proving the engine — in well under an hour.
