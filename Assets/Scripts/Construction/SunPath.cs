using System;

namespace SuperRealEstate.Construction
{
    public readonly struct SunResult
    {
        public readonly float DaylightHours;
        public readonly float SunriseAzimuthDeg; // compass bearing from north (90 = due east)
        public readonly float SunsetAzimuthDeg;  // (270 = due west)
        public readonly float NoonAltitudeDeg;   // sun height at solar noon
        public readonly bool PolarDayOrNight;

        public SunResult(float daylight, float sunriseAz, float sunsetAz, float noonAlt, bool polar)
        {
            DaylightHours = daylight; SunriseAzimuthDeg = sunriseAz; SunsetAzimuthDeg = sunsetAz;
            NoonAltitudeDeg = noonAlt; PolarDayOrNight = polar;
        }
    }

    /// <summary>
    /// Where the sun rises and sets and how high it climbs, for a latitude + day
    /// of year. Lets a buyer/builder standing on the lot see which way the home
    /// faces the morning/evening sun — for window placement, patios, and solar.
    /// Simplified solar-position math (Cooper declination); pure + tested.
    /// </summary>
    public static class SunPath
    {
        public static SunResult Compute(float latitudeDeg, int dayOfYear)
        {
            double lat = Deg2Rad(latitudeDeg);

            // Cooper's equation for solar declination.
            double declDeg = 23.45 * Math.Sin(Deg2Rad(360.0 / 365.0 * (284 + dayOfYear)));
            double decl = Deg2Rad(declDeg);

            // Sunrise/sunset hour angle: cos(H) = -tan(lat)·tan(decl).
            double cosH = -Math.Tan(lat) * Math.Tan(decl);
            bool polar = cosH < -1.0 || cosH > 1.0;
            cosH = Clamp(cosH, -1.0, 1.0);
            double H = Rad2Deg(Math.Acos(cosH)); // degrees
            float daylight = (float)(2.0 * H / 15.0);

            // Azimuth at sunrise (altitude 0): cos(Az) = sin(decl)/cos(lat). From north, east side.
            double cosAz = Clamp(Math.Sin(decl) / Math.Cos(lat), -1.0, 1.0);
            double azDeg = Rad2Deg(Math.Acos(cosAz));
            float sunriseAz = (float)azDeg;
            float sunsetAz = (float)(360.0 - azDeg);

            float noonAlt = (float)(90.0 - Math.Abs(latitudeDeg - declDeg));

            return new SunResult(daylight, sunriseAz, sunsetAz, noonAlt, polar);
        }

        private static double Deg2Rad(double d) => d * Math.PI / 180.0;
        private static double Rad2Deg(double r) => r * 180.0 / Math.PI;
        private static double Clamp(double v, double lo, double hi) => v < lo ? lo : (v > hi ? hi : v);
    }
}
