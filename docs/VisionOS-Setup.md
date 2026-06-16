# Vision Pro (visionOS) setup

Phase 1 targets Apple Vision Pro first via **Unity PolySpatial**. Most of this
is Editor + Xcode configuration that can't be scripted from the repo, so it
lives here as a checklist. The C# in this repo (measurement/cost core + the
`RoomMeasureController` glue) is the part that's already written.

## Prerequisites

- **Mac with Apple Silicon** + **Xcode** (latest), with the visionOS SDK.
- **Unity 6 LTS** with the **visionOS Build Support** module.
- An Apple Developer account for on-device deployment to Vision Pro.
- Confirm and pin package versions in `Packages/manifest.json` against the
  current releases (Unity 6 LTS, `com.unity.polyspatial*`, AR Foundation).

## Editor configuration

1. **Open the project** in Unity 6 (the `Packages/manifest.json` pulls AR
   Foundation, PolySpatial, OpenXR, Input System, Test Framework).
2. **XR Plug-in Management** → enable **Apple visionOS** for the visionOS
   platform. Set the app mode to **Mixed Reality (Immersive)** so plane/scene
   data and passthrough are available.
3. **PolySpatial**: enable PolySpatial; add a **Volume Camera** set to
   *Unbounded* for full-space room scanning.
4. **Scene**: create an AR scene with an **XR Origin**, **AR Session**, and an
   **AR Plane Manager**. Set the plane detection mode to **Horizontal**
   (floor + ceiling). Add the **AR Mesh Manager** if you want full scene
   reconstruction later.
5. Add **`RoomMeasureController`** (this repo) to the XR Origin (or a sibling)
   and assign the `ARPlaneManager` reference.
6. Wire a UI control (button / pinch / gaze-confirm) to call
   `RoomMeasureController.CaptureRoom(...)`, and bind `OnRoomMeasured` to the
   measurement UI panel (Phase 1 UI work).

## Build & run

1. Switch platform to **visionOS**.
2. **Build** → produces an Xcode project.
3. Open in **Xcode**, sign with your team, and run on the **Vision Pro** (or the
   visionOS simulator for UI checks — note the simulator has no real plane data).

## Verify (on-device)

1. Look around a real room so planes converge on the floor (and ceiling).
2. Trigger `CaptureRoom` → confirm the reported floor area / perimeter / height
   match a tape measure within tolerance.
3. Proceed to Phase 2 (material selection + cost) once measurement is trusted.

## Notes on portability to Galaxy XR (Android XR), later

The `RoomMeasureController` and the entire measurement/cost core are
backend-agnostic. Bringing up Galaxy XR is mostly: enable the **Android XR /
OpenXR** provider in XR Plug-in Management, switch platform to Android, and
re-test — no rewrite of the measurement logic.
