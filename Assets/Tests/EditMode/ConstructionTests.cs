using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using SuperRealEstate.Construction;

namespace SuperRealEstate.Tests
{
    public class ConstructionTests
    {
        private static SystemElement Run(string id, BuildingSystem sys, params Vector3[] pts)
            => new SystemElement { Id = id, System = sys, Kind = ElementKind.Run, Points = new List<Vector3>(pts) };

        [Test]
        public void SystemMetrics_RunLength_AndLinearFeet()
        {
            var model = new SystemsModel { Elements = { Run("e1", BuildingSystem.Electrical, new Vector3(0,0,0), new Vector3(3,0,0), new Vector3(3,0,4)) } };
            // 3 + 4 = 7 m ≈ 22.97 ft.
            Assert.AreEqual(7f, SystemMetrics.TotalLengthM(model, BuildingSystem.Electrical), 0.001f);
            Assert.AreEqual(22.966f, SystemMetrics.TotalLinearFeet(model, BuildingSystem.Electrical), 0.01f);
        }

        [Test]
        public void SystemMetrics_FixtureCount()
        {
            var model = new SystemsModel();
            model.Elements.Add(new SystemElement { Id = "o1", System = BuildingSystem.Electrical, Kind = ElementKind.Fixture, Points = { Vector3.zero } });
            model.Elements.Add(new SystemElement { Id = "o2", System = BuildingSystem.Electrical, Kind = ElementKind.Fixture, Points = { Vector3.one } });
            Assert.AreEqual(2, SystemMetrics.FixtureCount(model, BuildingSystem.Electrical));
        }

        [Test]
        public void Clash_DuctCrossesPipe_WithinClearance()
        {
            var duct = Run("duct", BuildingSystem.Hvac, new Vector3(0,2,0), new Vector3(4,2,0));
            var pipe = Run("pipe", BuildingSystem.Plumbing, new Vector3(2,2.05f,-2), new Vector3(2,2.05f,2)); // crosses 5cm above
            var model = new SystemsModel { Elements = { duct, pipe } };

            var clashes = ClashDetector.Find(model, clearanceM: 0.10f);
            Assert.AreEqual(1, clashes.Count);
            Assert.Less(clashes[0].DistanceM, 0.10f);
        }

        [Test]
        public void Clash_SameSystem_NotReported()
        {
            var a = Run("a", BuildingSystem.Hvac, new Vector3(0,0,0), new Vector3(4,0,0));
            var b = Run("b", BuildingSystem.Hvac, new Vector3(2,0.01f,-2), new Vector3(2,0.01f,2));
            var model = new SystemsModel { Elements = { a, b } };
            Assert.IsEmpty(ClashDetector.Find(model, 0.10f));
        }

        [Test]
        public void Clash_FarApart_NoClash()
        {
            var a = Run("a", BuildingSystem.Hvac, new Vector3(0,0,0), new Vector3(4,0,0));
            var b = Run("b", BuildingSystem.Plumbing, new Vector3(0,3,0), new Vector3(4,3,0));
            Assert.IsEmpty(ClashDetector.Find(new SystemsModel { Elements = { a, b } }, 0.10f));
        }

        [Test]
        public void SegmentDistance_ParallelAndCrossing()
        {
            float d1 = ClashDetector.SegmentDistance(new Vector3(0,0,0), new Vector3(10,0,0), new Vector3(0,2,0), new Vector3(10,2,0), out _);
            Assert.AreEqual(2f, d1, 0.001f);

            float d2 = ClashDetector.SegmentDistance(new Vector3(0,0,0), new Vector3(10,0,0), new Vector3(5,0.5f,-5), new Vector3(5,0.5f,5), out _);
            Assert.AreEqual(0.5f, d2, 0.001f);
        }

        [Test]
        public void AsBuilt_FlagsDeviationBeyondTolerance()
        {
            var ok = AsBuiltVerifier.Check(new Vector3(1,1,1), new Vector3(1.01f,1,1), toleranceM: 0.05f);
            Assert.IsTrue(ok.WithinTolerance);

            var off = AsBuiltVerifier.Check(new Vector3(1,1,1), new Vector3(1.2f,1,1), toleranceM: 0.05f);
            Assert.IsFalse(off.WithinTolerance);
            Assert.AreEqual(0.2f, off.DeviationM, 0.001f);
        }
    }
}
