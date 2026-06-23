# Android XR setup (Galaxy XR & Project Aura)

Android XR is a first-class target alongside visionOS — and crucially **the same
Unity project**, not a separate app. Because the AR layer is AR Foundation over
OpenXR, bringing up Android XR is "enable a provider + per-device input/UI," not
a rewrite. This covers the **Samsung Galaxy XR** headset and **Project
Aura-class** glasses.

## Why it's low-cost (what's already shared)

Everything except the thin AR/render layer is platform-agnostic and already
tested: measurement, cost, fit, blueprint, finishes, finish-matching,
renovation engine, landscape calculators, the design-system tokens + interaction
model, presentation view-models, the Supabase backend client, the Edge Functions,
and the web companion. The capability profiles in
`Assets/Scripts/Platform/XrCapabilities.cs` let features light up per device
without branching on device names.

## Prerequisites

- **Unity 6 LTS** with **Android Build Support** (SDK/NDK/JDK modules).
- Packages (already in `Packages/manifest.json`, pin to current releases):
  `com.unity.xr.androidxr-openxr`, `com.unity.xr.openxr`, `com.unity.xr.hands`,
  `com.unity.xr.arfoundation`.
- An **Android XR device or the Android XR Emulator** (Android Studio).

## Editor configuration

1. **Project Settings → XR Plug-in Management → Android tab** → enable **OpenXR**.
2. In **OpenXR → Android XR**, enable the feature groups you use:
   - **Session / reference space**, **Plane detection**, **Depth / scene mesh**,
     **Anchors** (and persistent/cloud anchors for shared sessions),
   - **Hand Tracking** (`com.unity.xr.hands`), **Eye Gaze Interaction** (for the
     gaze+pinch UI), **Passthrough** (composition layer).
3. Set the interaction profile(s): eye-gaze + hand, plus a controller profile as
   fallback.
4. The **same AR scene** as visionOS works here — XR Origin, AR Session, AR Plane
   Manager, and our `RoomMeasureController`. AR Foundation routes to the Android
   XR provider automatically when it's the active loader.

## Input & UI mapping

- **Galaxy XR**: gaze + pinch (eye + hand) — identical interaction model to
  Vision Pro; the design system's `Interaction.PrimaryModality(GalaxyXR)` returns
  `GazePinch`. Controllers are an available fallback.
- **Project Aura (glasses)**: eye tracking yes, but **optical see-through** and
  **tethered** — so the wall-removal *portal* feature is gated off
  (`XrCapabilities.SupportsWallPortal == false` for optical glasses) and heavy
  compute leans on the tethered phone. Measurement still works where depth is
  available; otherwise fall back to companion-phone capture.

## Build & run

1. Switch platform to **Android**; set the XR loader to Android XR (OpenXR).
2. **Build and Run** to the device/emulator (standard Android `.apk`/`.aab`).
3. Verify in a real room: planes converge → `RoomMeasureController.CaptureRoom`
   returns dimensions matching a tape measure.

## The honest hard parts

- **Provider maturity**: Android XR + its Unity provider are new (2025/2026) —
  pin package versions, expect some feature flags to move. Test on hardware.
- **Per-device capability differences** (headset vs. optical glasses): handled by
  `XrCapabilities` so features degrade gracefully rather than break.
- **Passthrough/portal rendering** differs from visionOS at the rendering layer
  (OpenXR composition vs. PolySpatial) — the `IPortalRenderer` contract is the
  same; the implementation is per-platform.
- **Shared spatial anchors** across iOS/visionOS/Android for one co-located
  session need a cross-platform anchor strategy (cloud/persistent anchors) — the
  `ISpatialAnchorService` contract already abstracts this.

## Effort estimate

- **Most of the app: 0 extra work** (shared, tested code + backend + web).
- **AR bring-up on Android XR**: enable the provider, map input, verify
  measurement + anchors — days, not a rebuild, *because the architecture chose
  AR Foundation/OpenXR up front.*
- **The real shared cost** is the on-device UI/scene work we already owe for
  visionOS — done once in AR Foundation, it serves both, with per-device input
  affordances from the design system.
