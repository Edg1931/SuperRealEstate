# How to see where we are — per platform

Ordered **fastest-to-slowest**. The quickest way to *see* progress is the **web
app** (minutes) and the **tests** (proves the engine logic). The two AR headset
apps need a one-time scene assembly in Unity (there's no committed `.unity` scene
yet — it's built from `docs/SCENE-ASSEMBLY-GalaxyXR.md`), then a device build.

Prereq for everything: clone the branch.
```bash
git clone <repo-url> && cd SuperRealEstate
git checkout claude/ar-realtor-tools-n8pkqg
```

---

## 0. Web companion (PC + phone browser) — ~5 min, no headset

This shows the catalog, the **design surface** (draw a plan + stage furniture +
publish), and **My projects**. The fastest "look at something real".

```bash
cd web
npm install
npm run dev
# open http://localhost:3000
```
- With **no env vars set**, the home page shows a "Supabase isn't configured"
  banner and the catalog/projects pages explain they need the backend — that's
  expected. The UI, design surface (drawing walls/furniture, payload preview,
  JSON export) all work offline.
- To light up data + sign-in + publish, create `web/.env.local` (copy
  `web/.env.local.example`) with your Supabase URL + anon key (see §2), then
  restart `npm run dev`.
- **On your phone:** it's responsive — open the same site. To deploy publicly,
  push to Vercel: import the repo, set **Root Directory = `web`**, add the two
  `NEXT_PUBLIC_*` env vars, deploy (see `web/README.md`).

**What you'll see working:** `/` home, `/catalog`, `/design` (author + publish a
project), `/projects` (your published projects after sign-in).

---

## 1. The test suite (the engine logic) — ~10 min, no headset

Proves the measured/tested core (measurement math, cost, co-location, realtime
parsing, consent/onboarding, settings/telemetry, palette adapt — ~41 tests).

1. Install **Unity 6 (6000.0.x)** via Unity Hub.
2. Unity Hub → **Add** → select the repo folder → open it (let packages resolve).
3. **Window → General → Test Runner → EditMode → Run All.** Everything green =
   the pure logic across all features is sound.
   - Headless/CI alternative:
     `Unity -batchmode -runTests -testPlatform EditMode -projectPath . -logFile -`

---

## 2. Supabase backend (shared by every app) — ~20 min, do once

Unlocks AI (plant/finish/voice/comps) + data + sign-in + sessions everywhere.

1. Create a project at supabase.com. Note **Settings → API**: Project URL +
   `anon` public key.
2. Apply migrations `supabase/migrations/0001…0009` and run `supabase/seed.sql`
   (Supabase SQL editor, or the Supabase CLI `supabase db push`).
3. Set secrets + deploy Edge Functions (Supabase CLI):
   ```bash
   supabase secrets set ANTHROPIC_API_KEY=sk-ant-...
   supabase secrets set RENTCAST_API_KEY=...   REGRID_API_KEY=...   # optional (comps/parcels)
   supabase functions deploy   # scene-insights, plant-id, voice-agent, comps, parcels, valuation, ...
   ```
4. Use that URL + anon key in: `web/.env.local`, and the AR app's `RealEstateApp`
   (`supabaseUrl` / `supabaseAnonKey` fields).

---

## 3. Samsung Galaxy XR — Android XR (no Mac needed) — ~1–2 hr first time

Follow `docs/SCENE-ASSEMBLY-GalaxyXR.md` (the exact component-by-component build),
in short:
1. Unity 6 with **Android Build Support** (SDK/NDK/JDK).
2. Open the project; **Project Settings → XR Plug-in Management → Android → enable
   OpenXR**, turn on the **Android XR** feature group (session, plane, depth,
   anchors, hand, eye-gaze, passthrough) + interaction profiles.
3. New scene → **XR Origin (AR)** + **AR Session** + Plane/Anchor managers; add our
   components (`RealEstateApp`, controllers, renderers, the UI views) and set the
   fields per the guide's table. Enter your Supabase URL/key on `RealEstateApp`.
   Import **TMP Essentials** (Window → TextMeshPro) so labels render.
4. **Build And Run** to a Galaxy XR (developer mode + ADB) — or the **Android XR
   Emulator** (Android Studio → Device Manager → Android XR system image).
5. In a real room: look around so planes converge, summon the tool menu, Measure.

> No device yet? The **Android XR Emulator** runs the build and is the fastest way
> to see the headset app without hardware.

---

## 4. Apple Vision Pro — visionOS (needs a Mac + Xcode) — ~1–2 hr first time

Follow `docs/VisionOS-Setup.md` + `docs/GETTING-STARTED-MAC.md`:
1. On an Apple-Silicon Mac: Unity 6 with **visionOS Build Support** + **Xcode**.
2. Same scene/components as Galaxy XR, plus **XR Plug-in Management → enable Apple
   visionOS**, add **PolySpatial** + a **Volume Camera** (Unbounded for world
   overlays). Drive `SpatialPointerInput` from the PolySpatial pointer +
   **SpatialTapGesture** (never read raw gaze).
3. **Build** → Unity emits an Xcode project → open it → sign with your Apple team →
   **Run** on the Vision Pro (or the visionOS Simulator for UI, though AR needs the
   device).

---

## 5. Phone AR companion (iOS/Android) — same Unity project

1. **File → Build Settings → switch platform** to iOS or Android.
2. The capability profiles auto-disable headset-only features (e.g. wall-removal
   portals) with a reason; input falls back to **camera-as-pointer + screen tap**.
3. Build to the phone (Android: APK/AAB; iOS: via Xcode). Measure, plant/finish
   ID, and the design viewer work on a phone screen.

---

## TL;DR — least effort to most

1. `cd web && npm install && npm run dev` → see the **web app** now.
2. Unity → **Test Runner** → green tests = the **engine** is sound.
3. Stand up **Supabase** → data + AI + sign-in everywhere.
4. **Galaxy XR** (or its emulator) → first headset build, no Mac.
5. **Vision Pro** (Mac + Xcode) → the second headset.
6. **Phone** build → the mobile AR companion.
