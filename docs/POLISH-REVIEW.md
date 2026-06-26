# Review — what's working, what to improve, polish & new ideas

A candid self-review of the project so far.

## What we're doing well

- **Cross-platform from day one.** AR Foundation/OpenXR means Galaxy XR, Vision
  Pro, and phones share ~90% of the code. Adding Android XR is adapters, not a
  port. `XrCapabilities` + `FeatureAvailability` make "what works where" explicit.
- **Pure, tested core.** Measurement, cost, fit, blueprint, finishes,
  finish-matching, renovation engine, landscape, UI math, view-models, feature
  gating, and the gaze model are all platform-agnostic and unit-tested — the
  logic is trustworthy before any device is involved.
- **Clean seams.** Every hard/AR/cloud thing is behind a contract
  (`IBackendClient`, `ISceneAnalyzer`, `IPlantIdentifier`, `IPortalRenderer`,
  `ISpatialAnchorService`, `IFinishCatalog`, `IBuildingModelImporter`), so the
  in-editor/on-device work is additive.
- **Responsible-AI baked in.** Advisory flag is a first-class UI role; condition/
  code/load-bearing/brand/comp outputs carry disclaimers — not bolted on.
- **Keys server-side.** Anthropic key lives only as a Supabase secret; clients
  use the anon key. Edge Functions proxy everything.
- **Honest scoping.** We don't claim bid-grade cost, code compliance, or live
  diminished-reality where they're unsolved — we ship the achievable slice and
  flag the frontier.

## Where to improve (next quality bar)

- **No on-device scene yet.** The biggest gap: a runnable AR scene (the Phase 1
  spatial UI). Highest priority for "see it on the Galaxy XR."
- **Importers are stubs.** RoomPlan USDZ / CubiCasa DXF parsing is contracts-only.
- **States & resilience.** We need first-class **loading / empty / error /
  offline** states everywhere data is fetched; retry/backoff on Edge Functions;
  graceful degradation when a feature is gated off.
- **Cost realism.** Add ZIP-localized cost factors (RSMeans/Kukun-style) + labor;
  a "get real bids" handoff. Today's prices are seeded placeholders.
- **Onboarding & permissions.** Camera/scan consent, first-run tutorial, and
  per-device input coaching (gaze+pinch vs touch).
- **Telemetry & analytics** (privacy-respecting) to learn what's used.
- **Security pass.** Review RLS policies end-to-end, Edge Function input
  validation, rate limiting, and the public-catalog exposure.
- **Localization / units.** Metric/imperial toggle exists in data; need full i18n
  and locale-aware currency for international buyers.
- **Accessibility validation.** We specced it (dynamic type, colorblind-safe,
  voice, dwell) — now verify on device.
- **Performance budgets.** Define and enforce draw-call/entity/texture budgets,
  splat LODs, foveation — especially for mobile-class Android XR.

## Polish backlog (the "feels great" layer)

- Micro-interactions: gaze-hover glow + 1.03× scale (specced) actually tuned;
  gentle 150–300 ms motion; reduce-motion honored.
- Spatial audio cues for selection/placement/confirmation; subtle haptics on
  pinch (where supported).
- Beautiful empty/loading/error cards (matching Spatial Glass).
- Confirmation + undo for every destructive/costly action.
- A coherent icon set + a settings panel (units, handedness, voice, contrast).
- Onboarding "first room in 60 seconds" flow.

## New feature ideas (beyond docs/FEATURES-BACKLOG.md)

- 🟡 **Voice agent** (Gemini on Android XR / on-device + our Edge Functions):
  fully conversational — "what's this tree, and how much to replace it?"
- 🟡 **Disclosure/inspection doc scan** → AI summary pinned to the right rooms.
- 🟡 **Live translation** of the agent's narration for international buyers.
- 🟡 **Investor mode**: rent estimate, cap rate, cash-on-cash, rehab ROI overlay.
- 🟡 **Energy/efficiency read**: window/insulation/orientation cues → rough
  utility + comfort notes (advisory).
- 🟡 **Moving logistics**: from staged furniture, estimate truck size / box count.
- 🔵 **New-construction punch list**: compare as-built scan vs. plan, flag deltas.
- 🔵 **"Vibe/lifestyle" layers** (handled carefully, fair-housing-safe): natural
  light, quiet, walkability — neutral, sourced, never demographic.

## Suggested near-term sequence

1. Android XR adapter + a minimal **runnable measurement scene** on Galaxy XR.
2. visionOS adapter in parallel (same scene, PolySpatial).
3. Loading/empty/error states + onboarding across the web + AR.
4. ZIP-localized cost + finish-catalog over Supabase.
5. Voice agent (Gemini) as the Android-XR-native wow moment.
