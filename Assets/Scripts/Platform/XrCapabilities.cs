namespace SuperRealEstate.Platform
{
    /// <summary>Target XR runtimes. All headset/glasses targets go through OpenXR + AR Foundation.</summary>
    public enum XrPlatform
    {
        VisionOS,          // Apple Vision Pro (PolySpatial / ARKit)
        AndroidXrHeadset,  // Samsung Galaxy XR (Android XR / OpenXR)
        AndroidXrGlasses,  // Project Aura-class Android XR glasses (tethered)
        IosPhone,          // ARKit
        AndroidPhone       // ARCore
    }

    /// <summary>How the user sees the real world — drives whether wall-removal portals work.</summary>
    public enum PassthroughKind
    {
        None,
        Screen,            // phone/tablet: real world is camera video on a screen
        VideoPassthrough,  // headset cameras → display (compositing works)
        OpticalSeeThrough  // glasses: real light through lenses (can't subtract/replace pixels)
    }

    /// <summary>
    /// What a target device can do. Feature code branches on these instead of on
    /// device names — so adding Android XR is "enable a provider + fill a
    /// profile", not a rewrite. Pure data; unit-tested.
    /// </summary>
    public readonly struct XrCapabilities
    {
        public readonly XrPlatform Platform;
        public readonly bool PlaneDetection;
        public readonly bool SceneMeshDepth;
        public readonly bool WorldAnchors;
        public readonly bool SharedAnchors;   // multi-device co-location
        public readonly bool EyeTracking;
        public readonly bool HandTracking;
        public readonly bool Controllers;
        public readonly bool StandaloneCompute; // false = tethered to a phone/PC
        public readonly PassthroughKind Passthrough;

        public XrCapabilities(XrPlatform platform, bool planeDetection, bool sceneMeshDepth,
            bool worldAnchors, bool sharedAnchors, bool eyeTracking, bool handTracking,
            bool controllers, bool standaloneCompute, PassthroughKind passthrough)
        {
            Platform = platform;
            PlaneDetection = planeDetection;
            SceneMeshDepth = sceneMeshDepth;
            WorldAnchors = worldAnchors;
            SharedAnchors = sharedAnchors;
            EyeTracking = eyeTracking;
            HandTracking = handTracking;
            Controllers = controllers;
            StandaloneCompute = standaloneCompute;
            Passthrough = passthrough;
        }

        // --- derived feature gates ---

        /// <summary>Room measurement needs depth/plane sensing.</summary>
        public bool SupportsRoomMeasurement => PlaneDetection && SceneMeshDepth;

        /// <summary>Gaze+pinch UI needs eye tracking; otherwise fall back to controller/touch.</summary>
        public bool SupportsGazeUi => EyeTracking;

        /// <summary>
        /// Wall-removal portal composites a scan over the real wall — needs the
        /// real world rendered as pixels (video passthrough or a screen). Optical
        /// see-through glasses can't replace real light, so portals don't apply.
        /// </summary>
        public bool SupportsWallPortal =>
            Passthrough == PassthroughKind.VideoPassthrough || Passthrough == PassthroughKind.Screen;
    }

    /// <summary>Capability profiles per target (verify against the live SDKs as they mature).</summary>
    public static class XrCapabilityProfiles
    {
        public static XrCapabilities For(XrPlatform platform) => platform switch
        {
            XrPlatform.VisionOS => new XrCapabilities(
                platform, planeDetection: true, sceneMeshDepth: true, worldAnchors: true,
                sharedAnchors: true, eyeTracking: true, handTracking: true, controllers: false,
                standaloneCompute: true, PassthroughKind.VideoPassthrough),

            XrPlatform.AndroidXrHeadset => new XrCapabilities(
                platform, planeDetection: true, sceneMeshDepth: true, worldAnchors: true,
                sharedAnchors: true, eyeTracking: true, handTracking: true, controllers: true,
                standaloneCompute: true, PassthroughKind.VideoPassthrough),

            XrPlatform.AndroidXrGlasses => new XrCapabilities(
                platform, planeDetection: true, sceneMeshDepth: false, worldAnchors: true,
                sharedAnchors: true, eyeTracking: true, handTracking: false, controllers: false,
                standaloneCompute: false, PassthroughKind.OpticalSeeThrough),

            XrPlatform.IosPhone => new XrCapabilities(
                platform, planeDetection: true, sceneMeshDepth: true, worldAnchors: true,
                sharedAnchors: true, eyeTracking: false, handTracking: false, controllers: false,
                standaloneCompute: true, PassthroughKind.Screen),

            XrPlatform.AndroidPhone => new XrCapabilities(
                platform, planeDetection: true, sceneMeshDepth: true, worldAnchors: true,
                sharedAnchors: true, eyeTracking: false, handTracking: false, controllers: false,
                standaloneCompute: true, PassthroughKind.Screen),

            _ => new XrCapabilities(platform, false, false, false, false, false, false, false, false, PassthroughKind.None),
        };
    }
}
