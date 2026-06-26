# Viewing the project across devices

Honest version first: most of the app is **tested logic + backend + scaffolds**,
not yet an assembled AR app. So today you can fully *see* it on the **web** and on
the **Mac (tests)**; the **headset app** needs the scene assembled first (see
`docs/NEXT-STEPS.md`). Here's what works on each device right now.

| Device | What you can view today | How |
|---|---|---|
| **Phone / tablet** | The web companion (catalog, and saved data once auth is added) | open the deployed URL in any browser |
| **Computer (any)** | Web companion + the code/docs/progress on GitHub | browser |
| **Mac** | The Unity project + the green test suite (proof the engine works) | Unity Hub → run EditMode tests |
| **Galaxy XR / Vision Pro** | The web companion in the headset's browser today; the AR app once the scene is assembled | headset browser now; native build later |

## 1. Web companion on every device (the real "multi-device" view)

This is the one thing that runs on phone, tablet, laptop, and the headset's
browser — all reading the same backend.

1. **Supabase** (once): create a free project at supabase.com; apply migrations
   `0001`–`0009` (`supabase db push`) and run `supabase/seed.sql`. Copy the
   Project URL + anon key (Settings → API).
2. **Deploy to Vercel** (once): import the repo, set **Root Directory = `web`**,
   add env vars `NEXT_PUBLIC_SUPABASE_URL` + `NEXT_PUBLIC_SUPABASE_ANON_KEY`,
   deploy. (Details in `web/README.md`.)
3. **Open the Vercel URL on any device** — phone, tablet, computer, and the
   Galaxy XR / Vision Pro **browser**. Same catalog everywhere.

(Want it live without doing this yourself? I can deploy it via the Vercel
integration if you share the Supabase URL + anon key.)

## 2. GitHub from anywhere

Open `github.com/Edg1931/superrealestate` (branch
`claude/ar-realtor-tools-n8pkqg`) on any device to browse the code, the docs
(`VISION.md`, `ROADMAP.md`, the `docs/` folder), and the commit history — a quick
way to review progress from a phone.

## 3. Mac — see the engine actually work

1. Unity Hub → add the repo root → open with Unity 6 LTS.
2. **Window → General → Test Runner → EditMode → Run All** → watch the suites go
   green (measurement, cost, fit, blueprint, finishes, renovation, landscape,
   construction/MEP, sun, overlays, platform, voice, project payload, …).
   That green is the most honest "where we are" on a single screen.

## 4. The headset app (the milestone that makes "walk it on the Galaxy XR" real)

Not runnable yet — the AR scene (XR rig + the wired components) isn't assembled.
That's the #1 next step in `docs/NEXT-STEPS.md`. Until then, use the headset's
**web browser** to view the companion. Once the scene is assembled and built
(per `docs/AndroidXR-Setup.md` / `docs/VisionOS-Setup.md`), the same project
deploys to both headsets.
