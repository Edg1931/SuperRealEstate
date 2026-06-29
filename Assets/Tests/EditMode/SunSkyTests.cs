using NUnit.Framework;
using UnityEngine;
using SuperRealEstate.Construction;

namespace SuperRealEstate.Tests
{
    public class SunSkyTests
    {
        private const float Tol = 0.5f;

        [Test]
        public void AtSolarNoon_FacesSouth_AtNoonAltitude()
        {
            // 12 daylight hours, sunrise due east, sunset due west, noon alt 50°.
            var sun = new SunResult(12f, 90f, 270f, 50f, false);
            var (az, alt) = SunSky.AltAzAt(sun, 12f);
            Assert.That(az, Is.EqualTo(180f).Within(Tol));      // due south at noon
            Assert.That(alt, Is.EqualTo(50f).Within(Tol));       // peak altitude
        }

        [Test]
        public void BeforeSunrise_IsBelowHorizon()
        {
            var sun = new SunResult(12f, 90f, 270f, 50f, false);
            var (_, alt) = SunSky.AltAzAt(sun, 5f); // sunrise at 06:00
            Assert.That(alt, Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void Evening_SunIsLowAndToTheWest()
        {
            var sun = new SunResult(14f, 60f, 300f, 65f, false); // long summer day
            var (az, alt) = SunSky.AltAzAt(sun, 17f); // 5pm
            Assert.Greater(az, 180f);   // past south, toward the west
            Assert.Greater(alt, 0f);
            Assert.Less(alt, 65f);      // below the noon peak
        }

        [Test]
        public void LightDirection_OverheadPointsDown()
        {
            Vector3 d = SunSky.LightDirection(180f, 90f); // sun straight up
            Assert.That(Vector3.Distance(d, Vector3.down), Is.LessThan(0.01f));
        }

        [Test]
        public void LightDirection_EastHorizon_PointsWest()
        {
            // Sun on the eastern horizon (az 90, alt 0) → light travels west (-X).
            Vector3 d = SunSky.LightDirection(90f, 0f);
            Assert.That(Vector3.Distance(d, new Vector3(-1f, 0f, 0f)), Is.LessThan(0.01f));
        }
    }
}
