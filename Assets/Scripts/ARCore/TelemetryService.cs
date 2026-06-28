using UnityEngine;
using SuperRealEstate.AppCore;
using SuperRealEstate.Onboarding;

namespace SuperRealEstate.ARCore
{
    /// <summary>Logs telemetry to the Unity console — the default dev sink.</summary>
    public sealed class DebugLogTelemetrySink : ITelemetrySink
    {
        public void Record(TelemetryEvent evt)
            => Debug.Log($"[telemetry] {evt.Name}{(string.IsNullOrEmpty(evt.Detail) ? "" : $" · {evt.Detail}")}"
                         + (evt.ValueMillis != 0 ? $" · {evt.ValueMillis}ms" : ""));
    }

    /// <summary>
    /// App-facing telemetry: forwards <see cref="Track"/> calls to a sink ONLY
    /// when analytics is allowed — the AND of the user's Analytics consent
    /// (<see cref="ConsentService"/>) and their settings opt-in
    /// (<see cref="SettingsService"/>). Both must be true, so analytics is off by
    /// default and stops the instant either is withdrawn. Swap the console sink
    /// for a Supabase/queue sink via <see cref="SetSink"/> when a backend lands.
    /// </summary>
    public sealed class TelemetryService : MonoBehaviour
    {
        [Tooltip("Consent gate — Analytics consent must be granted.")]
        [SerializeField] private ConsentService consent;
        [Tooltip("Settings — the user's analytics opt-in must be on.")]
        [SerializeField] private SettingsService settings;

        private Telemetry _telemetry;

        private void Awake()
        {
            _telemetry = new Telemetry(new DebugLogTelemetrySink());
            Refresh();

            if (consent != null) consent.OnConsentChanged.AddListener(OnConsentChanged);
            if (settings != null) settings.OnChanged.AddListener(OnSettingsChanged);
        }

        private void OnDestroy()
        {
            if (consent != null) consent.OnConsentChanged.RemoveListener(OnConsentChanged);
            if (settings != null) settings.OnChanged.RemoveListener(OnSettingsChanged);
        }

        /// <summary>Replace the sink (e.g. a Supabase uploader) at runtime.</summary>
        public void SetSink(ITelemetrySink sink)
        {
            bool enabled = _telemetry?.Enabled ?? false;
            _telemetry = new Telemetry(sink, enabled);
        }

        /// <summary>Record an event (dropped unless analytics is allowed).</summary>
        public void Track(string name, string detail = null, long valueMillis = 0)
            => _telemetry?.Track(name, detail, valueMillis);

        private void OnConsentChanged(ConsentType type, bool granted)
        {
            if (type == ConsentType.Analytics) Refresh();
        }

        private void OnSettingsChanged(AppSettings s) => Refresh();

        private void Refresh()
        {
            bool consented = consent != null && consent.IsGranted(ConsentType.Analytics);
            bool optedIn = settings != null && settings.Settings.AnalyticsOptIn;
            _telemetry?.SetEnabled(consented && optedIn);
        }
    }
}
