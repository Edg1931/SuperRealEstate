# Phone-first workflow — see the app without a laptop

You code a lot on your phone. This sets up two things so you can **see progress
from your phone**: a **live preview URL on every push**, and **build checks** you
can read in the GitHub mobile app.

## 1. Live preview URL on every push (Vercel) — the big one

Vercel rebuilds the web app and gives you a fresh URL **every time you push** —
open it on your phone to see the design surface, catalog, and projects live.

**One-time setup (do this once, from a browser — phone is fine):**
1. Go to **vercel.com → Add New → Project → Import** your GitHub repo
   (`Edg1931/SuperRealEstate`). Authorize Vercel for the repo if asked.
2. **Root Directory → `web`** (important — the app lives in the `web/` subfolder).
   Framework auto-detects as **Next.js**.
3. **Environment Variables** → add:
   - `NEXT_PUBLIC_SUPABASE_URL` = your Supabase project URL
   - `NEXT_PUBLIC_SUPABASE_ANON_KEY` = your Supabase anon/public key
   (Skip these to just see the UI; the app shows a "not configured" banner.)
4. **Deploy.** You get a production URL (your default branch) **and an automatic
   Preview URL for every other branch + every push.**

**After that, the loop is:**
- Push to your branch (e.g. `claude/ar-realtor-tools-n8pkqg`) from the phone.
- Vercel comments the **Preview URL** on the commit/PR within ~1 min.
- Tap it on your phone → the live app. Done. No laptop.

Tip: in **Vercel → Project → Settings → Git**, confirm **Preview Deployments**
are on for all branches (default). You can also "Promote to Production" from the
Vercel mobile site when a preview looks good.

## 2. Build checks in the GitHub mobile app

Two GitHub Actions run automatically (in `.github/workflows/`):

- **Web CI** (`web-ci.yml`) — builds the Next.js app on every push touching
  `web/`. Green = it compiles + type-checks. **No setup needed.** This catches
  breakage *before* Vercel even deploys.
- **Unity EditMode Tests** (`unity-tests.yml`) — runs the ~41 engine tests.
  **Gated/off by default** (Unity CI needs a license). To turn on later, see the
  comments at the top of that file (set the `ENABLE_UNITY_CI` variable + add the
  Unity license secrets).

**Read them on your phone:**
1. Install the **GitHub mobile app**, open the repo.
2. A commit/PR shows a ✓ or ✗ — tap **Checks** to see "build web" pass/fail and
   the logs. The **badge** at the top of the README also reflects the latest run.

## 3. The fastest "is it working" glance, phone-only

1. Push your change.
2. Open the GitHub app → see the ✓ on **Web CI** (it compiled).
3. Open the **Vercel preview URL** from the commit → see it *running*.

That's the whole loop — edit on phone → push → green check → tap the live URL.

## Optional: run the web app directly on a phone (no deploy)

If you use a phone IDE with a terminal (e.g. a-Shell, Termux, or a cloud dev env
like GitHub Codespaces in the browser):
```bash
cd web && npm install && npm run dev
```
Codespaces is the smoothest on a phone — it forwards `localhost:3000` to a
tappable URL. (Native phone terminals can struggle to build Next.js; Vercel
previews above are the reliable path.)
