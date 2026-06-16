using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using SuperRealEstate.RoomMeasure;

namespace SuperRealEstate.Tests
{
    public class FloorOutlineBuilderTests
    {
        private const float Tolerance = 0.001f;

        // A 4 x 3 boundary square in plane-local space.
        private static List<Vector2> RectBoundary() => new List<Vector2>
        {
            new Vector2(0f, 0f),
            new Vector2(4f, 0f),
            new Vector2(4f, 3f),
            new Vector2(0f, 3f),
        };

        [Test]
        public void IdentityPose_MapsLocalXyToWorldXz()
        {
            var outline = FloorOutlineBuilder.FromPlaneBoundary(Pose.identity, RectBoundary());

            Assert.AreEqual(4, outline.Count);
            // local (4,3) -> world (4, 0, 3)
            Assert.AreEqual(new Vector3(4f, 0f, 3f), outline[2]);
            // Area is preserved through the mapping.
            Assert.AreEqual(12f, MeasurementService.FloorArea(outline), Tolerance);
        }

        [Test]
        public void TranslatedPose_OffsetsOutlineButKeepsArea()
        {
            var pose = new Pose(new Vector3(10f, 1.5f, -5f), Quaternion.identity);
            var outline = FloorOutlineBuilder.FromPlaneBoundary(pose, RectBoundary());

            Assert.AreEqual(new Vector3(10f, 1.5f, -5f), outline[0]);
            Assert.AreEqual(12f, MeasurementService.FloorArea(outline), Tolerance);
        }

        [Test]
        public void RotatedPose_PreservesArea()
        {
            // Yaw 90 degrees: area is rotation-invariant in the XZ plane.
            var pose = new Pose(Vector3.zero, Quaternion.Euler(0f, 90f, 0f));
            var outline = FloorOutlineBuilder.FromPlaneBoundary(pose, RectBoundary());

            Assert.AreEqual(12f, MeasurementService.FloorArea(outline), Tolerance);
        }

        [Test]
        public void BuildRoom_FeedsMeasurementService()
        {
            var room = FloorOutlineBuilder.BuildRoom(Pose.identity, RectBoundary(), ceilingHeight: 2.5f);
            var m = MeasurementService.Compute(room);

            Assert.AreEqual(12f, m.FloorAreaSqM, Tolerance);
            Assert.AreEqual(14f, m.PerimeterM, Tolerance);
            Assert.AreEqual(35f, m.WallAreaSqM, Tolerance);   // 14 * 2.5
            Assert.AreEqual(30f, m.VolumeCubicM, Tolerance);  // 12 * 2.5
        }

        [Test]
        public void NullBoundary_ReturnsEmpty()
        {
            var outline = FloorOutlineBuilder.FromPlaneBoundary(Pose.identity, null);
            Assert.IsEmpty(outline);
        }
    }
}
