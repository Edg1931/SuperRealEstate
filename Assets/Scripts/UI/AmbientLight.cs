using UnityEngine;

namespace SuperRealEstate.UI
{
    /// <summary>
    /// The app's current ambient room luminance in [0,1], updated by the AR light
    /// probe and read by the UI builders so panels adapt their density to the room
    /// (see <see cref="PaletteAdapt"/>). A simple shared value rather than an event
    /// bus: builders read <see cref="Palette"/> at construction, so newly-spawned
    /// panels match the room without per-panel subscriptions. Defaults to a neutral
    /// mid value when no probe is running (e.g. tests, phone without estimation).
    /// </summary>
    public static class AmbientLight
    {
        /// <summary>Current ambient luminance, 0 (dark) .. 1 (bright). Default 0.5.</summary>
        public static float Current01 { get; private set; } = 0.5f;

        /// <summary>Set by the AR light probe (clamped).</summary>
        public static void Set(float ambient01) => Current01 = Mathf.Clamp01(ambient01);

        /// <summary>The palette adapted to the current ambient luminance.</summary>
        public static AdaptedPalette Palette => PaletteAdapt.For(Current01);
    }
}
