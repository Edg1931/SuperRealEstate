# Scene assembly — Galaxy XR (Android XR), step by step

Mechanical guide to assemble the runnable AR scene around `RealEstateApp` and
build the **room-measure MVP** to the Samsung Galaxy XR. Do these in order. Exact
Unity menu labels can vary slightly by 6.x version; the component names and field
names are exact. visionOS deltas are at the end. Background: `docs/AndroidXR-Setup.md`,
`docs/PLATFORM-PLAYBOOK.md`.

## 0. Prereqs (once)

- Unity 6 LTS with **Android Build Support** (SDK/NDK/JDK).
- Open the project; let packages resolve (`Packages/manifest.json` already lists
  `com.unity.xr.androidxr-openxr`, `com.unity.xr.openxr`, `com.unity.xr.hands`,
  `com.unity.xr.arfoundation`). Pin versions if Unity flags any.
- A Galaxy XR device (developer mode + USB/ADB) or the **Android XR Emulator**
  (Android Studio → Device Manager → Android XR system image).

## 1. Enable the XR runtime

1. **Project Settings → XR Plug-in Management → Android tab → enable OpenXR.**
2. **OpenXR → Android XR** feature group: enable **Session**, **Plane detection**,
   **Depth/Scene mesh**, **Anchors**, **Hand Tracking**, **Eye Gaze Interaction**,
   **Passthrough**.
3. Under **Interaction Profiles** add **Eye Gaze Interaction Profile** and a
   **Hand Interaction Profile** (and a controller profile as fallback).
4. Fix any red ⚠ validation issues OpenXR lists for Android.

## 2. Player settings (Android)

- **Project Settings → Player → Android:** Scripting Backend **IL2CPP**, Target
  Architectures **ARM64**, Minimum API per Android XR (follow the OpenXR
  validation hint), Graphics API **Vulkan** (or as validation recommends).
- Set Company/Product name + package id.

## 3. Create the scene + XR rig

1. New scene `RealEstate.unity`.
2. **GameObject → XR → XR Origin (AR)** — creates **XR Origin** with a **Main
   Camera** (has `ARCameraManager`, `ARCameraBackground`, `TrackedPoseDriver`).
3. **GameObject → XR → AR Session** — adds the **AR Session** object.
4. Select **XR Origin** → **Add Component**: `AR Plane Manager`, `AR Anchor
   Manager` (and `AR Mesh Manager` if you want scene mesh).
   - On `AR Plane Manager`, leave a plane prefab empty for now (detection still
     works); set Detection Mode = **Horizontal + Vertical**.

## 4. Eye-gaze pointer (drives selection)

1. Create an empty child of **XR Origin** named **GazePointer**.
2. Add a **Tracked Pose Driver (Input System)** to it, bound to the **eye-gaze
   pose** action (OpenXR Eye Gaze). This makes GazePointer's transform follow
   where the user looks.
3. Create an **Input Actions** asset (or reuse the XRI default) with a **Select**
   action bound to **pinch / trigger** (Hand Interaction "pinch", plus controller
   trigger as fallback).

## 5. Add our components and wire them

Create these objects and set the fields (drag the referenced objects into the
slots):

| Object | Component | Fields to set |
|---|---|---|
| **App** (empty root) | `ARCore.RealEstateApp` | `supabaseUrl`, `supabaseAnonKey`; drag **App's** `ProjectStagingController` into `stagingController`; drag **Renderers/Staged** into `stagedSceneRenderer`; drag **App's** `SceneAppActions` into `sceneActions`; drag the Main Camera's `ArCameraFrameProvider` into `cameraFrameProvider`; drag **Renderers/Portal** into `wallPortalRenderer`; drag **App's** `SharedSessionSync` into `sharedSessionSync` |
| **App** | `ARCore.ProjectStagingController` | (configured at runtime by `RealEstateApp`) |
| **App** | `ARCore.SceneAppActions` | drag **XR Origin's** `RoomMeasureController` into `roomMeasure`; wire `OnMeasured`/`OnPlants`/`OnFinishes`/`OnInfo` to HUD text (the analyzer + plant ID + frame provider are injected at runtime by `RealEstateApp`) |
| **Main Camera** (under XR Origin) | `ARCore.ArCameraFrameProvider` | leave `cameraManager` empty (auto-finds the `ARCameraManager` on this object); `jpegQuality = 80`; `maxDimension = 1024` |
| **App** | `ARCore.XrSessionBootstrap` | `autoDetect = true`; `platform = AndroidXrHeadset`; add **FeatureBindings** (e.g. `WallRemovalPortal` → the portal objects, `GazeUi` → the menu) |
| **Renderers/Staged** | `ARRender.StagedSceneRenderer` | (optional) assign a `FurniturePrefabLibrary` asset (Create → SuperRealEstate → Furniture Prefab Library; map vendor/asset ids → real prefabs) — it auto-loads into `PrefabMap` on Awake, no code change |
| **App** | `ARCore.ConsentService` | persists app-level consents (camera/scan/etc.) to PlayerPrefs; the onboarding consent screen calls `Grant`/`Deny`; `RealEstateApp` injects it into `SceneAppActions` to gate capture. Drag into `RealEstateApp.consentService` |
| **App** | `ARCore.VoiceCommandController` | drag into `RealEstateApp.voiceCommandController`; set `RealEstateApp.targetPlatform` to your device. Feed recognized speech to `Submit(transcript)` and speak `OnReply` with platform TTS; `OnActionBlocked`/`OnError` for fallbacks |
| **App** | `ARCore.SettingsService` | units/quality/voice/analytics/UI-scale, persisted to PlayerPrefs; settings UI binds `OnChanged` and calls the setters |
| **App** | `ARCore.TelemetryService` | drag `ConsentService` + `SettingsService` in; only emits when Analytics consent AND opt-in are both on. `Track("event")` from feature code |
| **App** | `ARCore.OnboardingController` | drag `ConsentService` + the `RealEstateApp` in; bind `OnStep`/`OnStatus`/`OnCompleted` to the view below; buttons call `Next`/`GrantCamera`/`SignIn`/`SkipSignIn` |
| **UI/Onboarding** | `ARCore.OnboardingPanelView` | drag the `OnboardingController` in; assign a `Font` to label buttons (optional); set `anchor` to where the panel floats. Builds a panel per step with buttons wired to the controller (procedural, swap for prefabs later) |
| **UI/Settings** | `ARCore.SettingsPanelView` | drag the `SettingsService` in; assign a `TMP_FontAsset`; rows toggle units/quality/voice/analytics and refresh live from `OnChanged` |
| **UI/Tools** | `ARCore.RadialToolMenuView` | the core nav palette; assign a `TMP_FontAsset`, set `targetPlatform`; bind `OnToolSelected` → your tool router and `OnToolBlocked` → the HUD. Summon with `Show()`/`Toggle()` (e.g. a wrist/palm gesture). Unavailable tools auto-dim per device |
| **Main Camera** (under XR Origin) | `ARCore.AmbientLightProbe` | enable **Light Estimation** on the AR Camera Manager; feeds room luminance so panels densify in bright rooms / lighten in dim ones. No refs to set |
| **(note)** | TextMeshPro | one-time: **Window → TextMeshPro → Import TMP Essential Resources** so default-font labels render |
| **Renderers/Portal** | `ARRender.WallPortalRenderer` | (optional) set `stencilMaterial` (stencil shader) + `opaqueMaterial`; add revealed-room materials to `RevealedCaptureMaterials` by capture id later. `RealEstateApp` injects this into `SceneAppActions` so a "remove wall" command opens a portal |
| **Renderers/Systems** | `ARRender.SystemsOverlayRenderer` | — |
| **Renderers/Property** | `ARRender.PropertyOverlayRenderer` | — |
| **App** | `ARCore.SharedSessionSync` | `pollIntervalSeconds = 1.5`; tick `enableRealtime` for low-latency updates + presence; wire `OnPlacementChanged`/`OnPlacementRemoved` to the staging renderer, `OnParticipantJoined`/`OnParticipantLeft` to the roster UI, and `OnPresence` to avatars/cursors (service + Realtime channel injected at runtime by `RealEstateApp`). Call `BeginSync()` after a session is created/joined; `PublishPresence(...)` each frame to share head/pointer pose |
| **XR Origin** | `ARCore.RoomMeasureController` | drag the XR Origin's `AR Plane Manager` into `planeManager`; `defaultCeilingHeight = 2.5` |
| **XR Origin** | `ARCore.RoomScanController` | drag the `AR Plane Manager` into `planeManager` (Detection Mode = Horizontal + Vertical). Wire `OnModelCaptured` → `SceneAppActions.SetBuildingModel` so "remove that wall" works on a freshly scanned room (and/or save it as an `ar_scan` project) |
| **App** | `ARCore.PresenceAvatars` | wire `SharedSessionSync.OnPresence` → `Apply` and `OnParticipantLeft` → `Remove`; set `LocalUserId` from `RealEstateApp.Sessions.UserId` after sign-in so your own avatar isn't drawn; optional `headPrefab`. Call `SharedSessionSync.PublishPresence(...)` each frame with your head/pointer pose |
| **Input** (empty) | `ARCore.SpatialPointerInput` | `pointerOrigin = GazePointer (transform)`; `selectAction = your Select action`; `maxDistance = 8`; `targetMask = Default` |
| **GazePointer** | `ARCore.PointerPoseDriver` | `target = GazePointer (this transform)`; bind `positionAction`/`rotationAction` to the **OpenXR eye-gaze pose** (Android XR) or the **visionOS pointer pose**; on phones leave them empty and set `fallbackSource = Main Camera`. This is our alternative to a raw Tracked Pose Driver (step 4) |

Notes:
- `RealEstateApp` builds the Supabase backend + project store on `Awake` and calls
  `ProjectStagingController.Configure(...)` — so leave the controller's slots
  empty; they're set in code. It likewise builds the `scene-insights` + `plant-id`
  Edge Function clients from the same Supabase config and calls
  `SceneAppActions.Configure(analyzer, plantId, ArCameraFrameProvider.CaptureJpeg)`
  — so the analyzer/plant/frame slots on `SceneAppActions` are set in code too.
- Plant ID and finish recognition only work once `supabaseUrl`/`supabaseAnonKey`
  are set and the camera is available; without them those actions speak a friendly
  "not available" message instead of failing. `ArCameraFrameProvider` returns
  `null` when the platform gates passthrough-camera access (callers handle it).
- `SpatialPointerInput.Model` is the shared `GazeInteractionModel`; UI/cards add a
  `GazeTarget` (with a Collider) and react via its `OnHover`/`OnSelect` events.

## 6. Trigger the first measurement

For the MVP, wire one trigger to `RoomMeasureController.CaptureRoom(out var m)`:
- Quickest: a temporary `GazeTarget` "Measure" card whose `OnSelect` calls a tiny
  glue method that calls `CaptureRoom` and logs/render the result, **or**
- Use the voice path later (`voice-agent` → `ActionDispatcher` →
  `IAppActions.MeasureRoomAsync`).

## 7. Build & run

1. **File → Build Settings → Android → Switch Platform**; add the scene.
2. **Build And Run** to the device (or the Android XR Emulator).
3. In a real room: look around so planes converge on the floor, trigger Measure,
   confirm the reported floor area/perimeter/height match a tape measure.

## 8. Go-live data (so the catalog/AI work)

Set `RealEstateApp.supabaseUrl` + `supabaseAnonKey` (Supabase → Settings → API),
apply migrations `0001`–`0009`, run `seed.sql`, and
`supabase secrets set ANTHROPIC_API_KEY=...` + deploy the Edge Functions. After
sign-in, call `RealEstateApp.SetAccessToken(jwt)` so writes pass RLS.

## visionOS deltas (Vision Pro)

Same scene/components. Differences (see `docs/VisionOS-Setup.md`):
- Enable **Apple visionOS** in XR Plug-in Management; add **PolySpatial** + a
  **Volume Camera** (Unbounded for world overlays).
- Don't read gaze: drive `SpatialPointerInput` from PolySpatial's pointer + a
  **SpatialTapGesture** as the Select action; the system does hover highlighting.
- Build → Xcode → sign with your Apple team → run on Vision Pro.

## What's still needed for a polished app

Real furniture prefabs + materials, the design-system UI prefabs (insight card,
measurement HUD, radial menu, plant card), the wall-removal portal renderer, the
Cloud-Anchors `ISpatialAnchorService`, auth + onboarding, and the on-device room
scan / RoomPlan import. See `docs/SHIPPING.md` (M4–M9).
