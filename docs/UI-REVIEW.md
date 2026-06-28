# Spatial UI review — Vision Pro & Android XR

An expert pass on the app's spatial UI: how intuitive/beautiful/easy the navigation
is on **Apple Vision Pro (visionOS)** and **Samsung Galaxy XR (Android XR)**, what's
strong, what was weak, and what changed. Read with `docs/DESIGN-SYSTEM.md`.

## Verdict

The **design system is genuinely world-class at the model layer** — angular type
sizing, vergence-comfort depth, a no-Midas-touch interaction model, an anchor
taxonomy that reserves head-lock for tiny status, and a "never pure white /
frosted glass" palette tuned for passthrough. The **gap was the render layer**:
the runtime panel/button builders didn't yet *use* those principles. This pass
closes the biggest gaps so navigation reads the way the system intends.

## What was strong (keep)

- **Type in degrees, not pixels** (`DesignTokens.Type*Deg`) — text stays legible
  at any depth. This is the single most-missed thing in AR UIs; we had it.
- **Comfort model** (`SpatialComfort`): vergence band 0.5–2.0 m (sweet 1.3 m),
  35° central cone, content ~10° below the eye line, gaze targets ≥2°, spacing
  ≥1°, dwell as accessibility-only. Correct and rare.
- **No Midas touch** (`GazeInteractionModel`): gaze hovers/previews freely; only an
  explicit pinch/tap commits. Exactly right for eye-driven UIs.
- **One platform-agnostic input seam** (`SpatialPointerInput` + `GazeTarget`):
  features subscribe to the model, never to a device.

## What was weak → fixed this pass

1. **No hover/selection feedback.** The model raised hover/commit, but the visuals
   ignored it — on eye-gaze you couldn't tell what you were about to pick.
   → **`SpatialHoverFeedback`**: grows the target to `HoverScale` and brightens its
   accent on hover, with a press dip on commit, eased over `HoverFadeSeconds`.
   Auto-attached by `SpatialButtonBuilder`. **Biggest intuitiveness win, both
   platforms — and essential on Android XR, which has no system hover glow.**
2. **Panels didn't face the user.** Floating text read at an angle.
   → **`BillboardToUser`**: yaw-billboards (stays upright, turns toward you) with a
   lazy follow so it feels placed-in-world, not pasted-to-face; seats new panels in
   the comfort zone (central cone, sweet depth, slightly below eye line) once.
3. **Targets too close to gaze-select.** Button stacking used `SpaceS` (~0.01 m ≈
   0.44° at 1.3 m) — under the 1° minimum, so eye-gaze mis-selects between rows.
   → Views now space rows by an **angular gap** (`AngularToMeters(≈1.6°)` ≈ 0.036 m)
   and pad with `SpaceL`, so neighbors clear the spacing floor.
4. **Panels rebuilt jumpy.** Each step/toggle re-created the panel.
   → A **persistent billboarded root**: only the panel contents rebuild; placement
   and facing stay put, so advancing a step or flipping a setting doesn't lurch.

## Platform specifics

### Apple Vision Pro (visionOS / PolySpatial)
- **Never read raw gaze** — correct. We drive selection from the system pointer +
  a `SpatialTapGesture` (the OS does private gaze hover). Keep it that way for
  App Review.
- **System hover effect:** visionOS adds a glow to entities with a hover component.
  Our procedural quads don't get that automatically — `SpatialHoverFeedback` now
  supplies an explicit, on-brand hover so behavior is consistent with Android XR
  rather than relying on the system effect. When you author real prefabs, also add
  PolySpatial's hover effect so the system and our highlight agree.
- **Depth & legibility:** the frosted `Surface` (alpha 0.70) + below-eye-line
  placement read well on Vision Pro's high-PPD display. Keep panels ≤ the 35° cone.

### Samsung Galaxy XR (Android XR / OpenXR)
- **We do get an eye-gaze ray** (`PointerPoseDriver` ← OpenXR eye-gaze pose) + pinch.
  Because there is **no system hover highlight**, the new `SpatialHoverFeedback` is
  what makes targeting legible here — without it, eye-gaze selection was a guess.
- **Target sizing:** with the angular spacing fix, rows clear the ≥1° gap and the
  ≥2° size floor at sweet depth. Verify on-device and bump `buttonHeightM`/`Gap`
  if your eye-tracking calibration is coarse.
- **Passthrough contrast:** Galaxy XR passthrough can be brighter/noisier than
  Vision Pro — the edge halo + frosted fill help; consider the ambient-luminance
  palette adapt (design-system "adapts to ambient luminance") as a follow-up.

## Done in the follow-up pass

- **Crisp text → TextMeshPro.** `SpatialLabel` builds SDF world-text sized from
  the angular type scale (cap-height in degrees → meters at depth), replacing the
  aliased `TextMesh`. Assign a `TMP_FontAsset` (or use the TMP default after
  importing TMP Essentials). Buttons, titles, and settings rows all use it.
- **Ambient-luminance adapt.** Pure `PaletteAdapt` (tested) densifies the panel
  fill in bright rooms and lightens it in dim ones, easing text luminance to avoid
  bloom; `AmbientLightProbe` reads AR light estimation and feeds `AmbientLight`,
  which `SpatialPanelBuilder` reads so new panels match the room.
- **Radial tool menu view.** `RadialToolMenuView` builds the wrist palette from the
  `RadialToolMenu` model on an arc, dims tools unavailable on the device (via
  `ActionDispatcher.ResolveTool`), billboards + seats in the comfort zone, and
  raises `OnToolSelected` / `OnToolBlocked`.

## Still to do (beyond these passes)

- **Real background blur:** `PanelBlur` is a token but the placeholder material is
  unlit/transparent, not blurred. Author a "Spatial Glass" shader (kawase blur +
  edge halo) for the true frosted look.
- **Reduce Motion** hook (token exists; collapse motion durations when set).
- **Haptics/audio** confirm on pinch-commit (Galaxy XR controllers / hand pinch).
- **Per-font TMP calibration:** tune `SpatialLabel.CapUnitsAtBaseFont` for your
  chosen font so cap heights match the angular tokens exactly.
