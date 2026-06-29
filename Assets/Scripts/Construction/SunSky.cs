using UnityEngine;

namespace SuperRealEstate.Construction
{
    /// <summary>
    /// Approximate sun position across the day from a <see cref="SunResult"/>, and
    /// the resulting directional-light direction — so a daylight preview can scrub
    /// the time of day ("see this kitchen at 5 pm in July"). Approximate by design
    /// (solar noon at 12:00, sinusoidal altitude, azimuth swept sunrise → south →
    /// sunset); good enough for a mood/daylight study, not an astronomical model.
    /// Pure + unit-tested.
    /// </summary>
    public static class SunSky
    {
        /// <summary>
        /// Sun azimuth (compass degrees from north, clockwise) + altitude (degrees
        /// above the horizon) at a local hour in [0,24]. Returns altitude 0 when the
        /// sun is below the horizon.
        /// </summary>
        public static (float azimuthDeg, float altitudeDeg) AltAzAt(SunResult sun, float localHour)
        {
            if (sun.PolarDayOrNight)
                return (180f, Mathf.Max(0f, sun.NoonAltitudeDeg));

            float daylight = Mathf.Clamp(sun.DaylightHours, 0f, 24f);
            if (daylight <= 0f) return (sun.SunriseAzimuthDeg, 0f);

            const float noon = 12f;
            float sunrise = noon - daylight * 0.5f;
            float sunset = noon + daylight * 0.5f;

            if (localHour <= sunrise) return (sun.SunriseAzimuthDeg, 0f);
            if (localHour >= sunset) return (sun.SunsetAzimuthDeg, 0f);

            float frac = (localHour - sunrise) / (sunset - sunrise); // 0..1 across the day
            float altitude = Mathf.Max(0f, sun.NoonAltitudeDeg) * Mathf.Sin(frac * Mathf.PI);
            float azimuth = frac < 0.5f
                ? Mathf.Lerp(sun.SunriseAzimuthDeg, 180f, frac * 2f)
                : Mathf.Lerp(180f, sun.SunsetAzimuthDeg, (frac - 0.5f) * 2f);

            return (azimuth, altitude);
        }

        /// <summary>
        /// World-space direction the sunlight travels (set a directional light's
        /// <c>forward</c> to this). Convention: +Z = north, +X = east, +Y = up;
        /// azimuth measured clockwise from north.
        /// </summary>
        public static Vector3 LightDirection(float azimuthDeg, float altitudeDeg)
        {
            float az = azimuthDeg * Mathf.Deg2Rad;
            float al = altitudeDeg * Mathf.Deg2Rad;
            // Unit vector pointing toward the sun, then negate (light travels away from it).
            var toSun = new Vector3(Mathf.Sin(az) * Mathf.Cos(al), Mathf.Sin(al), Mathf.Cos(az) * Mathf.Cos(al));
            return toSun.sqrMagnitude > 1e-8f ? -toSun.normalized : Vector3.down;
        }
    }
}
