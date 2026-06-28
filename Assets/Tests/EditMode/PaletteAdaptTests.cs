using NUnit.Framework;
using UnityEngine;
using SuperRealEstate.UI;

namespace SuperRealEstate.Tests
{
    public class PaletteAdaptTests
    {
        [Test]
        public void BrightRoom_DensifiesPanel()
        {
            float dark = PaletteAdapt.SurfaceOpacity(0f);
            float bright = PaletteAdapt.SurfaceOpacity(1f);
            Assert.Less(dark, bright, "bright rooms should use a denser (more opaque) panel");
            Assert.That(dark, Is.EqualTo(PaletteAdapt.MinSurfaceOpacity).Within(0.001f));
            Assert.That(bright, Is.EqualTo(PaletteAdapt.MaxSurfaceOpacity).Within(0.001f));
        }

        [Test]
        public void Opacity_IsMonotonicAndClamped()
        {
            Assert.That(PaletteAdapt.SurfaceOpacity(-1f), Is.EqualTo(PaletteAdapt.MinSurfaceOpacity).Within(0.001f));
            Assert.That(PaletteAdapt.SurfaceOpacity(2f), Is.EqualTo(PaletteAdapt.MaxSurfaceOpacity).Within(0.001f));
            Assert.Less(PaletteAdapt.SurfaceOpacity(0.25f), PaletteAdapt.SurfaceOpacity(0.75f));
        }

        [Test]
        public void Text_IsBrighterInTheDark_NeverBlooms()
        {
            AdaptedPalette dark = PaletteAdapt.For(0f);
            AdaptedPalette bright = PaletteAdapt.For(1f);

            float darkMax = Mathf.Max(dark.OnSurface.r, Mathf.Max(dark.OnSurface.g, dark.OnSurface.b));
            float brightMax = Mathf.Max(bright.OnSurface.r, Mathf.Max(bright.OnSurface.g, bright.OnSurface.b));

            Assert.Greater(darkMax, brightMax, "text should be brighter in dim rooms");
            Assert.LessOrEqual(darkMax, 1.0f, "never exceed full white (bloom)");
        }

        [Test]
        public void Surface_KeepsHue_AdaptsAlpha()
        {
            AdaptedPalette p = PaletteAdapt.For(0.5f);
            // Same RGB hue as the token, only alpha differs.
            Assert.That(p.Surface.r, Is.EqualTo(DesignTokens.Surface.r).Within(0.001f));
            Assert.That(p.Surface.g, Is.EqualTo(DesignTokens.Surface.g).Within(0.001f));
            Assert.That(p.Surface.b, Is.EqualTo(DesignTokens.Surface.b).Within(0.001f));
            Assert.That(p.Surface.a, Is.EqualTo(PaletteAdapt.SurfaceOpacity(0.5f)).Within(0.001f));
        }
    }
}
