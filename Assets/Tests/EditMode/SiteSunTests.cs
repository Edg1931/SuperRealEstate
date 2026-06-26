using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using SuperRealEstate.Construction;

namespace SuperRealEstate.Tests
{
    public class SiteSunTests
    {
        private static Parcel Lot20() => new Parcel
        {
            Boundary = new List<Vector2> { new(0,0), new(20,0), new(20,20), new(0,20) }
        };

        private static SiteBuilding House(params Vector2[] pts)
            => new SiteBuilding { Footprint = new List<Vector2>(pts) };

        [Test]
        public void Setback_CenteredHouse_Compliant()
        {
            var house = House(new(5,5), new(15,5), new(15,15), new(5,15)); // 5 m to every line
            var r = SetbackChecker.Check(Lot20(), house, requiredSetbackM: 3f);
            Assert.IsTrue(r.Compliant);
            Assert.IsTrue(r.InsideLot);
            Assert.AreEqual(5f, r.MinSetbackM, 0.01f);
        }

        [Test]
        public void Setback_TooClose_NotCompliant()
        {
            var house = House(new(5,5), new(15,5), new(15,15), new(5,15)); // 5 m, but 7 required
            var r = SetbackChecker.Check(Lot20(), house, requiredSetbackM: 7f);
            Assert.IsFalse(r.Compliant);
            Assert.IsTrue(r.InsideLot);
        }

        [Test]
        public void Setback_OverLine_FlagsOutside()
        {
            var house = House(new(15,5), new(25,5), new(25,15), new(15,15)); // spills past x=20
            var r = SetbackChecker.Check(Lot20(), house, requiredSetbackM: 3f);
            Assert.IsFalse(r.InsideLot);
            Assert.IsFalse(r.Compliant);
        }

        [Test]
        public void Sun_Equator_Equinox_Is12hAndDueEast()
        {
            var s = SunPath.Compute(latitudeDeg: 0f, dayOfYear: 80);
            Assert.AreEqual(12f, s.DaylightHours, 0.3f);
            Assert.AreEqual(90f, s.SunriseAzimuthDeg, 2f); // due east
            Assert.AreEqual(270f, s.SunsetAzimuthDeg, 2f); // due west
            Assert.AreEqual(90f, s.NoonAltitudeDeg, 1f);   // overhead
        }

        [Test]
        public void Sun_Lat40_Equinox_NoonAltitude50()
        {
            var s = SunPath.Compute(40f, 80);
            Assert.AreEqual(11.95f, s.DaylightHours, 0.4f);
            Assert.AreEqual(49.6f, s.NoonAltitudeDeg, 1f);
        }

        [Test]
        public void Sun_Lat40_SummerSolstice_LongDay_SunriseNortheast()
        {
            var s = SunPath.Compute(40f, 172);
            Assert.Greater(s.DaylightHours, 13f);          // long summer day
            Assert.Less(s.SunriseAzimuthDeg, 90f);         // rises north of east
            Assert.AreEqual(73.45f, s.NoonAltitudeDeg, 1f);
        }
    }
}
