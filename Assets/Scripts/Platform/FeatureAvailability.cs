using System.Collections.Generic;

namespace SuperRealEstate.Platform
{
    /// <summary>User-facing capabilities the app can offer.</summary>
    public enum AppFeature
    {
        RoomMeasurement,
        MaterialCost,
        VirtualStaging,
        VendorCatalog,
        RenovationEditing,
        WallRemovalPortal,
        SurfaceFinishRecognition,
        PlantIdentification,
        SceneInsights,
        BlueprintAuthoring,
        SharedSession,
        GazeUi,
        LandscapeCalculators,
        PropertyOverlays   // comps / parcel lines / risk layers
    }

    public readonly struct FeatureStatus
    {
        public readonly AppFeature Feature;
        public readonly bool Available;
        public readonly string Reason; // why it's off (for UI + telemetry)

        public FeatureStatus(AppFeature feature, bool available, string reason)
        {
            Feature = feature; Available = available; Reason = reason;
        }
    }

    /// <summary>
    /// Resolves which features light up on a device from its
    /// <see cref="XrCapabilities"/> — the single place that answers "what works
    /// on Galaxy XR vs. Project Aura glasses vs. a phone vs. Vision Pro." Pure +
    /// tested, so the answer is consistent across the app and the marketing site.
    /// </summary>
    public static class FeatureAvailability
    {
        public static bool IsAvailable(XrCapabilities c, AppFeature feature) => feature switch
        {
            AppFeature.RoomMeasurement   => c.SupportsRoomMeasurement,
            AppFeature.WallRemovalPortal => c.SupportsWallPortal,
            AppFeature.GazeUi            => c.SupportsGazeUi,
            AppFeature.SharedSession     => c.SharedAnchors,
            AppFeature.VirtualStaging    => c.PlaneDetection,       // need a floor to place on
            AppFeature.RenovationEditing => c.PlaneDetection || !c.StandaloneCompute, // edit anywhere; AR view needs planes
            // Data / compute / 2D features run everywhere with a camera + network:
            AppFeature.MaterialCost              => true,
            AppFeature.VendorCatalog             => true,
            AppFeature.SurfaceFinishRecognition  => true,
            AppFeature.PlantIdentification       => true,
            AppFeature.SceneInsights             => true,
            AppFeature.BlueprintAuthoring        => true,
            AppFeature.LandscapeCalculators      => true,
            AppFeature.PropertyOverlays          => true,
            _ => true,
        };

        public static string Reason(XrCapabilities c, AppFeature feature)
        {
            if (IsAvailable(c, feature)) return "available";
            return feature switch
            {
                AppFeature.RoomMeasurement   => "needs depth/plane sensing",
                AppFeature.WallRemovalPortal => c.Passthrough == PassthroughKind.OpticalSeeThrough
                    ? "optical see-through can't replace real light (use a video-passthrough device)"
                    : "needs video passthrough or a screen",
                AppFeature.GazeUi            => "no eye tracking — use controller/touch",
                AppFeature.SharedSession     => "no shared anchoring",
                AppFeature.VirtualStaging    => "needs plane detection",
                _ => "unavailable on this device",
            };
        }

        public static IReadOnlyList<FeatureStatus> Resolve(XrCapabilities c)
        {
            var list = new List<FeatureStatus>();
            foreach (AppFeature f in System.Enum.GetValues(typeof(AppFeature)))
                list.Add(new FeatureStatus(f, IsAvailable(c, f), Reason(c, f)));
            return list;
        }
    }
}
