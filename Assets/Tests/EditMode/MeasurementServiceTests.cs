using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using SuperRealEstate.RoomMeasure;

namespace SuperRealEstate.Tests
{
    public class MeasurementServiceTests
    {
        private const float Tolerance = 0.001f;

        // A 4m x 3m rectangular room (in the XZ plane), 2.5m ceiling.
        private static RoomGeometry RectRoom(float width = 4f, float depth = 3f, float height = 2.5f)
        {
            var outline = new List<Vector3>
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(width, 0f, 0f),
                new Vector3(width, 0f, depth),
                new Vector3(0f, 0f, depth),
            };
            return new RoomGeometry(outline, height);
        }

        [Test]
        public void FloorArea_Rectangle_IsWidthTimesDepth()
        {
            Assert.AreEqual(12f, MeasurementService.Compute(RectRoom()).FloorAreaSqM, Tolerance);
        }

        [Test]
        public void FloorArea_IsWindingOrderIndependent()
        {
            var cw = RectRoom().FloorOutline;
            var reversed = new List<Vector3>(cw);
            reversed.Reverse();
            Assert.AreEqual(
                MeasurementService.FloorArea(cw),
                MeasurementService.FloorArea(reversed),
                Tolerance);
        }

        [Test]
        public void Perimeter_Rectangle_IsTwiceSumOfSides()
        {
            Assert.AreEqual(14f, MeasurementService.Compute(RectRoom()).PerimeterM, Tolerance);
        }

        [Test]
        public void WallArea_IsPerimeterTimesHeight()
        {
            // 14m perimeter * 2.5m = 35 m²
            Assert.AreEqual(35f, MeasurementService.Compute(RectRoom()).WallAreaSqM, Tolerance);
        }

        [Test]
        public void Volume_IsFloorAreaTimesHeight()
        {
            // 12 m² * 2.5m = 30 m³
            Assert.AreEqual(30f, MeasurementService.Compute(RectRoom()).VolumeCubicM, Tolerance);
        }

        [Test]
        public void CeilingArea_EqualsFloorArea()
        {
            var m = MeasurementService.Compute(RectRoom());
            Assert.AreEqual(m.FloorAreaSqM, m.CeilingAreaSqM, Tolerance);
        }

        [Test]
        public void FloorArea_ConvertsToSquareFeet()
        {
            // 12 m² ≈ 129.17 ft²
            Assert.AreEqual(129.167f, MeasurementService.Compute(RectRoom()).FloorAreaSqFt, 0.01f);
        }

        [Test]
        public void Compute_TooFewPoints_Throws()
        {
            var outline = new List<Vector3> { Vector3.zero, Vector3.right };
            var room = new RoomGeometry(outline, 2.5f);
            Assert.Throws<System.ArgumentException>(() => MeasurementService.Compute(room));
        }

        [Test]
        public void FloorArea_LShapedRoom_SumsCorrectly()
        {
            // L-shape: 4x4 square with a 2x2 corner removed -> 12 m².
            var outline = new List<Vector3>
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(4f, 0f, 0f),
                new Vector3(4f, 0f, 2f),
                new Vector3(2f, 0f, 2f),
                new Vector3(2f, 0f, 4f),
                new Vector3(0f, 0f, 4f),
            };
            Assert.AreEqual(12f, MeasurementService.FloorArea(outline), Tolerance);
        }
    }
}
