using UnityEngine;

namespace SuperRealEstate.UI
{
    /// <summary>Where a UI element is anchored — the #1 comfort decision.</summary>
    public enum AnchorMode
    {
        WorldLocked,   // pinned to a real point/object (labels on a wall/plant)
        SurfaceLocked, // floats just off a surface, billboarded to the user
        WristLocked,   // tool palette summoned at the hand/wrist
        HeadLocked     // ONLY tiny persistent status (recording dot, peers)
    }

    /// <summary>
    /// Ergonomics constants for placing and sizing spatial UI comfortably and
    /// for eye-tracking hit-testing (see docs/DESIGN-SYSTEM.md). Keep content in
    /// the central cone, at comfortable depth, slightly below eye line.
    /// </summary>
    public static class SpatialComfort
    {
        // Placement
        public const float ComfortConeDeg   = 35f;   // keep interactive content within this central cone
        public const float DepthMinM        = 0.5f;  // closer than this strains vergence
        public const float DepthSweetM      = 1.3f;
        public const float DepthMaxM        = 2.0f;
        public const float BelowEyeLineDeg  = 10f;    // rest content slightly below the horizon

        // Eye-tracking hit targets
        public const float MinTargetAngularDeg  = 2.0f; // gaze targets must be at least this big
        public const float MinTargetSpacingDeg  = 1.0f;
        public const float HoverScale           = 1.03f;
        public const float HoverFadeSeconds     = 0.12f;

        // Dwell is an ACCESSIBILITY fallback, not the default selector.
        public const float DwellSelectSeconds = 0.8f;

        /// <summary>True if an angular target size is comfortably selectable by gaze.</summary>
        public static bool IsComfortableTarget(float angularSizeDeg) => angularSizeDeg >= MinTargetAngularDeg;

        /// <summary>Clamp a desired panel depth into the comfortable range.</summary>
        public static float ClampDepth(float meters) => Mathf.Clamp(meters, DepthMinM, DepthMaxM);

        /// <summary>Physical size (m) that subtends <paramref name="angularDeg"/> at a depth.</summary>
        public static float AngularToMeters(float angularDeg, float depthM)
            => 2f * depthM * Mathf.Tan(angularDeg * 0.5f * Mathf.Deg2Rad);

        /// <summary>Angular size (deg) a physical size subtends at a depth.</summary>
        public static float MetersToAngular(float sizeM, float depthM)
        {
            if (depthM <= 0f) return 0f;
            return 2f * Mathf.Atan(sizeM / (2f * depthM)) * Mathf.Rad2Deg;
        }

        /// <summary>Is a physical target comfortably gaze-selectable at this depth?</summary>
        public static bool IsComfortableTargetAt(float sizeM, float depthM)
            => MetersToAngular(sizeM, depthM) >= MinTargetAngularDeg;
    }
}
