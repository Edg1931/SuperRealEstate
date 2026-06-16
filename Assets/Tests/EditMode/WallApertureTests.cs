using NUnit.Framework;
using UnityEngine;
using SuperRealEstate.Renovation;

namespace SuperRealEstate.Tests
{
    public class WallApertureTests
    {
        private const float Tol = 0.001f;

        [Test]
        public void Build_ComputesQuadCornersAreaCenter()
        {
            var wall = new Wall { Start = new Vector2(0f, 0f), End = new Vector2(4f, 0f), HeightM = 2.5f };
            var ap = WallApertureBuilder.Build(wall, floorY: 0f);

            Assert.AreEqual(4, ap.Corners.Length);
            Assert.AreEqual(new Vector3(0f, 0f, 0f), ap.Corners[0]); // bottom-start
            Assert.AreEqual(new Vector3(4f, 0f, 0f), ap.Corners[1]); // bottom-end
            Assert.AreEqual(new Vector3(4f, 2.5f, 0f), ap.Corners[2]); // top-end
            Assert.AreEqual(new Vector3(0f, 2.5f, 0f), ap.Corners[3]); // top-start

            Assert.AreEqual(10f, ap.AreaM2, Tol);                 // 4 × 2.5
            Assert.AreEqual(new Vector3(2f, 1.25f, 0f), ap.Center);
        }

        [Test]
        public void Build_NormalIsHorizontalAndPerpendicular()
        {
            var wall = new Wall { Start = new Vector2(0f, 0f), End = new Vector2(4f, 0f), HeightM = 2.5f };
            var ap = WallApertureBuilder.Build(wall);

            Assert.AreEqual(0f, ap.Normal.y, Tol);                // horizontal
            Assert.AreEqual(1f, ap.Normal.magnitude, Tol);        // unit length
            // wall runs along +X → normal along ±Z, perpendicular to the run.
            Assert.AreEqual(0f, Vector3.Dot(ap.Normal, new Vector3(1f, 0f, 0f)), Tol);
        }

        [Test]
        public void Build_RespectsFloorElevation()
        {
            var wall = new Wall { Start = new Vector2(0f, 0f), End = new Vector2(3f, 0f), HeightM = 2.0f };
            var ap = WallApertureBuilder.Build(wall, floorY: 10f);

            Assert.AreEqual(10f, ap.Corners[0].y, Tol);
            Assert.AreEqual(12f, ap.Corners[2].y, Tol);           // floor + height
        }
    }
}
