using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace SuperRealEstate.AppCore
{
    /// <summary>Measurement units shown across the app.</summary>
    public enum UnitSystem { Imperial, Metric }

    /// <summary>Render-quality tier (passthrough overlays, staged scene detail).</summary>
    public enum QualityLevel { Low, Balanced, High }

    /// <summary>
    /// User preferences — units, render quality, voice, analytics opt-in, and UI
    /// scale. Pure + serializable (the AR layer persists it via PlayerPrefs).
    /// Defaults are sensible for a US realtor audience (Imperial, balanced,
    /// voice on, analytics OFF until opted in).
    /// </summary>
    public sealed class AppSettings
    {
        public UnitSystem Units = UnitSystem.Imperial;
        public QualityLevel Quality = QualityLevel.Balanced;
        public bool VoiceEnabled = true;
        public bool AnalyticsOptIn = false;   // opt-in only (privacy default)
        public float UiScale = 1.0f;          // 0.75..1.5 spatial UI scale

        /// <summary>A clamped copy of <see cref="UiScale"/> in the supported range.</summary>
        public float ClampedUiScale => UiScale < 0.75f ? 0.75f : (UiScale > 1.5f ? 1.5f : UiScale);

        /// <summary>Compact <c>key=value;…</c> serialization (deterministic order).</summary>
        public string Serialize()
        {
            var parts = new List<string>
            {
                $"units={Units}",
                $"quality={Quality}",
                $"voice={(VoiceEnabled ? 1 : 0)}",
                $"analytics={(AnalyticsOptIn ? 1 : 0)}",
                $"uiscale={UiScale.ToString("0.###", CultureInfo.InvariantCulture)}",
            };
            var sb = new StringBuilder();
            for (int i = 0; i < parts.Count; i++)
            {
                if (i > 0) sb.Append(';');
                sb.Append(parts[i]);
            }
            return sb.ToString();
        }

        /// <summary>Parse <see cref="Serialize"/> output. Unknown/missing keys keep defaults.</summary>
        public static AppSettings Deserialize(string s)
        {
            var settings = new AppSettings();
            if (string.IsNullOrWhiteSpace(s)) return settings;

            foreach (string pair in s.Split(';'))
            {
                int eq = pair.IndexOf('=');
                if (eq <= 0) continue;
                string key = pair.Substring(0, eq).Trim().ToLowerInvariant();
                string val = pair.Substring(eq + 1).Trim();

                switch (key)
                {
                    case "units":
                        if (Enum.TryParse(val, true, out UnitSystem u)) settings.Units = u;
                        break;
                    case "quality":
                        if (Enum.TryParse(val, true, out QualityLevel q)) settings.Quality = q;
                        break;
                    case "voice":
                        settings.VoiceEnabled = val == "1";
                        break;
                    case "analytics":
                        settings.AnalyticsOptIn = val == "1";
                        break;
                    case "uiscale":
                        if (float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out float f))
                            settings.UiScale = f;
                        break;
                }
            }
            return settings;
        }
    }
}
