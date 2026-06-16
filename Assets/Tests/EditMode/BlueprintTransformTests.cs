using NUnit.Framework;
using UnityEngine;
using SuperRealEstate.Staging;

namespace SuperRealEstate.Tests
{
    public class BlueprintTransformTests
    {
        private const float Tol = 0.001f;

        [Test]
        public void Identity_MapsPlanXyToWorldXz()
        {
            var t = BlueprintTransform.Solve(
                planA: new Vector2(0f, 0f), planB: new Vector2(1f, 0f),
                worldA: new Vector2(0f, 0f), worldB: new Vector2(1f, 0f));

            Assert.AreEqual(1f, t.Scale, Tol);
            Assert.AreEqual(0f, t.RotationDegrees, Tol);

            Vector3 w = t.ToWorld(new Vector2(2f, 3f));
            Assert.AreEqual(new Vector3(2f, 0f, 3f), w);
        }

        [Test]
        public void ReferencePoints_MapExactly_ForAnyCorrespondence()
        {
            // Messy correspondence with rotation, scale, translation, and floor.
            var planA = new Vector2(1f, 2f);
            var planB = new Vector2(4f, 3f);
            var worldA = new Vector2(10f, -5f);
            var worldB = new Vector2(7f, 1f);
            float floorY = 1.5f;

            var t = BlueprintTransform.Solve(planA, planB, worldA, worldB, floorY);

            Vector3 a = t.ToWorld(planA);
            Vector3 b = t.ToWorld(planB);

            // The two reference points must map exactly onto their world targets.
            Assert.That(Vector3.Distance(a, new Vector3(worldA.x, floorY, worldA.y)), Is.LessThan(0.01f));
            Assert.That(Vector3.Distance(b, new Vector3(worldB.x, floorY, worldB.y)), Is.LessThan(0.01f));
        }

        [Test]
        public void PreservesScaledDistances()
        {
            // World segment is 2x the plan segment → scale 2.
            var t = BlueprintTransform.Solve(
                planA: new Vector2(0f, 0f), planB: new Vector2(1f, 0f),
                worldA: new Vector2(0f, 0f), worldB: new Vector2(2f, 0f));

            Assert.AreEqual(2f, t.Scale, Tol);

            Vector3 p = t.ToWorld(new Vector2(3f, 1f));
            Vector3 q = t.ToWorld(new Vector2(5f, 1f));
            // plan distance 2 → world distance 4
            Assert.AreEqual(4f, Vector3.Distance(p, q), 0.01f);
        }

        [Test]
        public void Translation_OffsetsOrigin()
        {
            var t = BlueprintTransform.Solve(
                planA: new Vector2(0f, 0f), planB: new Vector2(1f, 0f),
                worldA: new Vector2(10f, 5f), worldB: new Vector2(11f, 5f));

            Vector3 origin = t.ToWorld(new Vector2(0f, 0f));
            Assert.AreEqual(new Vector3(10f, 0f, 5f), origin);
        }

        [Test]
        public void ToWorldPlacement_ComposesYaw_AndPreservesSourceAndScale()
        {
            // 90-degree blueprint rotation: plan +x maps to world +z.
            var t = BlueprintTransform.Solve(
                planA: new Vector2(0f, 0f), planB: new Vector2(1f, 0f),
                worldA: new Vector2(0f, 0f), worldB: new Vector2(0f, 1f));

            var bp = new BlueprintPlacement
            {
                CatalogItemId = "vendor-sofa",
                PlanPosition = new Vector2(2f, 0f),
                PlanYawDegrees = 30f,
                Scale = 1.25f,
            };

            Placement world = t.ToWorldPlacement(bp);

            Assert.AreEqual(PlacementSource.VendorCatalog, world.Source);
            Assert.AreEqual("vendor-sofa", world.CatalogItemId);
            Assert.AreEqual(1.25f, world.Scale, Tol);                       // item size unchanged
            Assert.AreEqual(30f + t.RotationDegrees, world.YawDegrees, Tol); // yaw composes
            // plan (2,0) under a 90-deg rotation lands on world +z axis at distance 2
            Assert.AreEqual(0f, world.Position.x, 0.01f);
            Assert.AreEqual(2f, world.Position.z, 0.01f);
        }

        [Test]
        public void IdenticalReferencePoints_Throws()
        {
            Assert.Throws<System.ArgumentException>(() =>
                BlueprintTransform.Solve(
                    new Vector2(1f, 1f), new Vector2(1f, 1f),
                    new Vector2(0f, 0f), new Vector2(5f, 0f)));
        }
    }
}
