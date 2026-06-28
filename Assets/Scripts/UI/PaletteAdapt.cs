using UnityEngine;

namespace SuperRealEstate.UI
{
    /// <summary>The palette resolved for the current ambient lighting.</summary>
    public readonly struct AdaptedPalette
    {
        public readonly Color Surface;        // panel fill (alpha = opacity)
        public readonly Color OnSurface;      // primary text/icons
        public readonly Color OnSurfaceMuted; // secondary text

        public AdaptedPalette(Color surface, Color onSurface, Color onSurfaceMuted)
        {
            Surface = surface;
            OnSurface = onSurface;
            OnSurfaceMuted = onSurfaceMuted;
        }
    }

    /// <summary>
    /// Adapts the "Spatial Glass" palette to ambient room luminance so legibility
    /// stays constant across a dim listing and a sun-filled one (a design-system
    /// requirement — see docs/DESIGN-SYSTEM.md). Pure + unit-tested.
    ///
    /// Rule: a **bright** room needs a **denser** panel (raise fill opacity) so
    /// text doesn't wash out against bright passthrough; a **dark** room can use a
    /// **lighter, more translucent** panel (the room shows through) and a slightly
    /// brighter-but-not-blooming near-white. Accent/positive/caution hues are left
    /// alone — only fill density and text luminance adapt.
    /// </summary>
    public static class PaletteAdapt
    {
        // Fill opacity range: translucent in the dark, dense in the bright.
        public const float MinSurfaceOpacity = 0.55f;
        public const float MaxSurfaceOpacity = 0.88f;

        /// <summary>Panel fill opacity for an ambient luminance in [0,1].</summary>
        public static float SurfaceOpacity(float ambient01)
            => Mathf.Lerp(MinSurfaceOpacity, MaxSurfaceOpacity, Mathf.Clamp01(ambient01));

        /// <summary>The full adapted palette for an ambient luminance in [0,1].</summary>
        public static AdaptedPalette For(float ambient01)
        {
            float a = Mathf.Clamp01(ambient01);

            // Fill: keep the hue, swap in the adapted opacity.
            Color surface = DesignTokens.Surface;
            surface.a = SurfaceOpacity(a);

            // Text: slightly brighter in the dark (more headroom before bloom),
            // eased back in bright rooms to avoid #FFF bloom on passthrough.
            float lum = Mathf.Lerp(0.99f, 0.90f, a); // dark → 0.99, bright → 0.90
            Color on = ScaleToLuminance(DesignTokens.OnSurface, lum);
            Color muted = ScaleToLuminance(DesignTokens.OnSurfaceMuted, Mathf.Lerp(0.82f, 0.74f, a));

            return new AdaptedPalette(surface, on, muted);
        }

        /// <summary>Rescale an (already near-white) color so its max channel hits <paramref name="targetMax"/>.</summary>
        private static Color ScaleToLuminance(Color c, float targetMax)
        {
            float max = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
            if (max <= 0.0001f) return new Color(targetMax, targetMax, targetMax, c.a);
            float k = targetMax / max;
            return new Color(c.r * k, c.g * k, c.b * k, c.a);
        }
    }
}
