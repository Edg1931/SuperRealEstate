using System;
using UnityEngine;
using UnityEngine.Events;
using SuperRealEstate.AppCore;

namespace SuperRealEstate.ARCore
{
    /// <summary>
    /// Persists <see cref="AppSettings"/> (units, quality, voice, analytics opt-in,
    /// UI scale) to <see cref="PlayerPrefs"/> and notifies listeners on change.
    /// The settings UI reads <see cref="Settings"/> and calls the setters; feature
    /// code (measurement formatting, voice enable, UI scale) subscribes to
    /// <see cref="OnChanged"/>.
    /// </summary>
    public sealed class SettingsService : MonoBehaviour
    {
        private const string PrefKey = "sre.settings.v1";

        [Serializable] public sealed class SettingsEvent : UnityEvent<AppSettings> { }

        [Tooltip("Raised whenever any setting changes (and once on load).")]
        public SettingsEvent OnChanged = new SettingsEvent();

        private AppSettings _settings;

        /// <summary>Current settings (loaded from prefs on first access).</summary>
        public AppSettings Settings => _settings ??= Load();

        private void Awake()
        {
            _settings = Load();
            OnChanged.Invoke(_settings);
        }

        public void SetUnits(UnitSystem units) { Settings.Units = units; Commit(); }
        public void SetQuality(QualityLevel quality) { Settings.Quality = quality; Commit(); }
        public void SetVoiceEnabled(bool on) { Settings.VoiceEnabled = on; Commit(); }
        public void SetAnalyticsOptIn(bool on) { Settings.AnalyticsOptIn = on; Commit(); }
        public void SetUiScale(float scale) { Settings.UiScale = scale; Commit(); }

        /// <summary>Persist + notify after a change.</summary>
        public void Commit()
        {
            PlayerPrefs.SetString(PrefKey, Settings.Serialize());
            PlayerPrefs.Save();
            OnChanged.Invoke(Settings);
        }

        private static AppSettings Load()
            => AppSettings.Deserialize(PlayerPrefs.GetString(PrefKey, string.Empty));
    }
}
