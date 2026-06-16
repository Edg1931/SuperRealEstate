using UnityEngine;

namespace SuperRealEstate.UI
{
    /// <summary>
    /// The single source of truth for the "Spatial Glass" look (see
    /// docs/DESIGN-SYSTEM.md). Every spatial view reads these so the language is
    /// consistent across visionOS, Android XR, and phone. Sizes that must stay
    /// legible at any depth are expressed in **angular degrees**, not pixels.
    /// </summary>
    public static class DesignTokens
    {
        // --- Color roles (never pure white: #FFF blooms on passthrough) ---
        public static readonly Color Surface       = new Color(0.10f, 0.11f, 0.13f, 0.70f); // frosted panel
        public static readonly Color OnSurface     = new Color(0.93f, 0.94f, 0.96f, 1f);
        public static readonly Color OnSurfaceMuted = new Color(0.74f, 0.76f, 0.80f, 1f);
        public static readonly Color Accent        = new Color(0.31f, 0.66f, 0.88f, 1f);     // selection / primary
        public static readonly Color Positive      = new Color(0.36f, 0.75f, 0.54f, 1f);     // fits / savings
        public static readonly Color Caution       = new Color(0.88f, 0.66f, 0.31f, 1f);     // over-budget / tight
        public static readonly Color Advisory      = new Color(0.95f, 0.74f, 0.36f, 1f);     // estimates / ⚠ flags

        // --- Spatial Glass material (panels float in the room) ---
        public const float PanelOpacity   = 0.70f;
        public const float PanelBlur      = 0.5f;   // 0..1 background blur strength
        public const float EdgeHalo       = 0.15f;  // faint rim to separate from busy backgrounds
        public const float ShadowSoftness = 0.6f;

        // --- Corner radius / stroke (meters at 1 m depth; scale with distance) ---
        public const float CornerRadiusM = 0.02f;
        public const float StrokeM       = 0.0015f;

        // --- Type scale: cap height in DEGREES of visual angle ---
        public const float TypeDisplayDeg = 1.20f;
        public const float TypeTitleDeg   = 0.80f;
        public const float TypeBodyDeg    = 0.50f;
        public const float TypeCaptionDeg = 0.40f;
        public const float TypeMinDeg     = 0.35f;  // never smaller
        public const float MaxLineWidthDeg = 28f;   // ~40 chars; keep lines short

        // --- Spacing scale (meters at 1 m depth) ---
        public const float SpaceXs = 0.005f;
        public const float SpaceS  = 0.010f;
        public const float SpaceM  = 0.020f;
        public const float SpaceL  = 0.040f;
        public const float SpaceXl = 0.080f;

        // --- Motion (seconds); honor Reduce Motion by collapsing to ~0 ---
        public const float MotionFast   = 0.15f;
        public const float MotionMedium = 0.25f;
        public const float MotionSlow   = 0.30f;
    }
}
