using System;
using System.Collections.Generic;
using UnityEngine;
using SuperRealEstate.AppCore;
using SuperRealEstate.ARRender;
using SuperRealEstate.UI;

namespace SuperRealEstate.ARCore
{
    /// <summary>
    /// A spatial settings panel bound to <see cref="SettingsService"/>: rows that
    /// toggle/cycle units, render quality, voice, and analytics opt-in. Each tap
    /// calls the service, which persists and fires <c>OnChanged</c> — and this
    /// rebuilds so every label reflects the live value. Procedural design-system
    /// UI (<see cref="SpatialPanelBuilder"/>/<see cref="SpatialButtonBuilder"/>);
    /// swap in authored prefabs later without touching the service.
    /// </summary>
    public sealed class SettingsPanelView : MonoBehaviour
    {
        [SerializeField] private SettingsService settings;
        [SerializeField] private Transform anchor;
        [Tooltip("Font for row text (optional; rows still build without it).")]
        [SerializeField] private Font font;

        [SerializeField] private float panelWidthM = 0.7f;
        [SerializeField] private float rowWidthM = 0.62f;
        [SerializeField] private float rowHeightM = 0.08f;

        private GameObject _current;

        private void Awake() { if (anchor == null) anchor = transform; }

        private void OnEnable()
        {
            if (settings == null) return;
            settings.OnChanged.AddListener(Rebuild);
            Rebuild(settings.Settings);
        }

        private void OnDisable()
        {
            if (settings != null) settings.OnChanged.RemoveListener(Rebuild);
            Clear();
        }

        private void Rebuild(AppSettings s)
        {
            Clear();
            if (s == null) return;

            var rows = new List<Row>
            {
                new Row("set.units", $"Units: {s.Units}",
                    () => settings.SetUnits(s.Units == UnitSystem.Imperial ? UnitSystem.Metric : UnitSystem.Imperial)),
                new Row("set.quality", $"Quality: {s.Quality}",
                    () => settings.SetQuality(NextQuality(s.Quality))),
                new Row("set.voice", $"Voice: {(s.VoiceEnabled ? "On" : "Off")}",
                    () => settings.SetVoiceEnabled(!s.VoiceEnabled)),
                new Row("set.analytics", $"Analytics: {(s.AnalyticsOptIn ? "On" : "Off")}",
                    () => settings.SetAnalyticsOptIn(!s.AnalyticsOptIn)),
            };

            float pad = DesignTokens.SpaceM;
            float height = pad * 2f + rows.Count * (rowHeightM + DesignTokens.SpaceS);
            _current = SpatialPanelBuilder.Build(panelWidthM, height, "SettingsPanel");
            _current.transform.SetParent(anchor, worldPositionStays: false);

            float y = height * 0.5f - pad - rowHeightM * 0.5f;
            foreach (Row r in rows)
            {
                GameObject btn = SpatialButtonBuilder.Build(r.Id, r.Label, rowWidthM, rowHeightM, r.OnSelect, font);
                btn.transform.SetParent(_current.transform, worldPositionStays: false);
                btn.transform.localPosition = new Vector3(0f, y, -0.004f);
                y -= rowHeightM + DesignTokens.SpaceS;
            }
        }

        private static QualityLevel NextQuality(QualityLevel q) => q switch
        {
            QualityLevel.Low => QualityLevel.Balanced,
            QualityLevel.Balanced => QualityLevel.High,
            _ => QualityLevel.Low,
        };

        private void Clear()
        {
            if (_current == null) return;
            if (Application.isPlaying) Destroy(_current); else DestroyImmediate(_current);
            _current = null;
        }

        private readonly struct Row
        {
            public readonly string Id;
            public readonly string Label;
            public readonly Action OnSelect;
            public Row(string id, string label, Action onSelect) { Id = id; Label = label; OnSelect = onSelect; }
        }
    }
}
