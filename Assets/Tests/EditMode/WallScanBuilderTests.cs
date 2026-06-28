using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using SuperRealEstate.Renovation;

namespace SuperRealEstate.Tests
{
    public class WallScanBuilderTests
    {
        private const float Tol = 0.001f;

        [Test]
        public void TryBuildWall_ProjectsHorizontalExtentOntoFloor()
        {
            // A 4m-wide, 2.5m-tall plane centered at (2,1.25,0), running east-west.
            var surface = new ScannedSurface(new Vector3(2f, 1.25f, 0f), Vector3.right, 4f, 2.5f);
            bool ok = WallScanBuilder.TryBuildWall(surface, floorY: 0f, id: "w0", out Wall wall);

            Assert.IsTrue(ok);
            Assert.AreEqual("w0", wall.Id);
            Assert.That(Vector2.Distance(wall.Start, new Vector2(0f, 0f)), Is.LessThan(Tol));
            Assert.That(Vector2.Distance(wall.End, new Vector2(4f, 0f)), Is.LessThan(Tol));
            Assert.That(wall.LengthM, Is.EqualTo(4f).Within(Tol));
            Assert.That(wall.HeightM, Is.EqualTo(2.5f).Within(Tol));
        }

        [Test]
        public void TryBuildWall_DropsVerticalTiltFromAxis()
        {
            // Right axis tilted upward; only the horizontal part should define the run.
            var surface = new ScannedSurface(new Vector3(0f, 1f, 0f), new Vector3(0f, 0.3f, 1f), 2f, 2.4f);
            bool ok = WallScanBuilder.TryBuildWall(surface, 0f, "w", out Wall wall);

            Assert.IsTrue(ok);
            // Axis collapses to +z → wall runs along plan-y.
            Assert.That(Mathf.Abs(wall.Start.x), Is.LessThan(Tol));
            Assert.That(Mathf.Abs(wall.End.x), Is.LessThan(Tol));
            Assert.That(wall.LengthM, Is.EqualTo(2f).Within(Tol));
        }

        [Test]
        public void TryBuildWall_RejectsShortSurface()
        {
            var tiny = new ScannedSurface(Vector3.zero, Vector3.right, 0.2f, 2.5f);
            Assert.IsFalse(WallScanBuilder.TryBuildWall(tiny, 0f, "w", out _));
        }

        [Test]
        public void TryBuildWall_RejectsDegenerateHorizontalAxis()
        {
            // Purely vertical axis has no horizontal run.
            var bad = new ScannedSurface(Vector3.zero, Vector3.up, 3f, 2.5f);
            Assert.IsFalse(WallScanBuilder.TryBuildWall(bad, 0f, "w", out _));
        }

        [Test]
        public void BuildModel_KeepsValidWalls_AndStampsSource()
        {
            var surfaces = new List<ScannedSurface>
            {
                new ScannedSurface(new Vector3(2f, 1.25f, 0f), Vector3.right, 4f, 2.5f),
                new ScannedSurface(new Vector3(0f, 1f, 0.1f), Vector3.up, 3f, 2.5f),   // degenerate → dropped
                new ScannedSurface(new Vector3(4f, 1.25f, 2f), Vector3.forward, 4f, 2.5f),
            };

            BuildingModel model = WallScanBuilder.BuildModel(surfaces, floorY: 0f, id: "scan-1");

            Assert.AreEqual("scan-1", model.Id);
            Assert.AreEqual("ar_scan", model.SourceType);
            Assert.AreEqual(2, model.Walls.Count);
            Assert.AreEqual("w0", model.Walls[0].Id);
            Assert.AreEqual("w1", model.Walls[1].Id);
        }

        [Test]
        public void BuildModel_NullSurfaces_ReturnsEmptyModel()
        {
            BuildingModel model = WallScanBuilder.BuildModel(null);
            Assert.IsNotNull(model);
            Assert.AreEqual(0, model.Walls.Count);
        }
    }
}
