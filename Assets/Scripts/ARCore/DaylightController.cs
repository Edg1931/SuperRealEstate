using UnityEngine;
using SuperRealEstate.Construction;

namespace SuperRealEstate.ARCore
{
    /// <summary>
    /// Drives a directional light to approximate real sunlight for a property at a
    /// given latitude, day of year, and time of day — "see this kitchen at 5 pm in
    /// July." Scrub <see cref="SetTimeOfDay"/> (e.g. from a slider) to watch the
    /// light and shadows sweep across the staged room. Uses the tested
    /// <see cref="SunPath"/> + <see cref="SunSky"/> math (approximate daylight
    /// study, not an ephemeris). North is +Z; orient the staged scene accordingly
    /// (or offset via the scene's heading).
    /// </summary>
    public sealed class DaylightController : MonoBehaviour
    {
        [Tooltip("The scene's sun (a Directional Light).")]
        [SerializeField] private Light sun;

        [SerializeField] private float latitudeDeg = 39f;
        [Tooltip("Day of year (1–365). ~172 = Jun 21, ~355 = Dec 21.")]
        [Range(1, 365)] [SerializeField] private int dayOfYear = 172;
        [Tooltip("Local time of day (hours, 0–24).")]
        [Range(0f, 24f)] [SerializeField] private float timeOfDay = 17f;

        [SerializeField] private float maxIntensity = 1.2f;
        [Tooltip("Sun color across altitude 0→1 (warm at the horizon, white high).")]
        [SerializeField] private Gradient skyTint;

        public float TimeOfDay => timeOfDay;

        private void OnEnable() => Apply();
        private void OnValidate() => Apply();

        /// <summary>Scrub the time of day (hours, 0–24) — wire to a slider.</summary>
        public void SetTimeOfDay(float hour) { timeOfDay = Mathf.Clamp(hour, 0f, 24f); Apply(); }

        /// <summary>Set the day of year (1–365) — wire to a season picker.</summary>
        public void SetDayOfYear(int day) { dayOfYear = Mathf.Clamp(day, 1, 365); Apply(); }

        /// <summary>Set the property's latitude (degrees).</summary>
        public void SetLatitude(float lat) { latitudeDeg = Mathf.Clamp(lat, -90f, 90f); Apply(); }

        private void Apply()
        {
            if (sun == null) return;

            SunResult result = SunPath.Compute(latitudeDeg, dayOfYear);
            (float az, float alt) = SunSky.AltAzAt(result, timeOfDay);

            sun.transform.rotation = Quaternion.LookRotation(SunSky.LightDirection(az, alt), Vector3.up);

            // Dim toward the horizon; full strength when the sun is high.
            float k = Mathf.Clamp01(alt / 60f);
            sun.intensity = maxIntensity * k;
            if (skyTint != null) sun.color = skyTint.Evaluate(k);
        }
    }
}
