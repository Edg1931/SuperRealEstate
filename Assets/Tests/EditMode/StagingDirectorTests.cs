using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using SuperRealEstate.Staging;

namespace SuperRealEstate.Tests
{
    public class StagingDirectorTests
    {
        // 4m x 3m rectangular room, origin corner.
        private static List<Vector3> Room() => new List<Vector3>
        {
            new Vector3(0, 0, 0), new Vector3(4, 0, 0), new Vector3(4, 0, 3), new Vector3(0, 0, 3),
        };

        private static StagingDirector.DirectorItem Sofa() => new StagingDirector.DirectorItem
        { id = "sofa", name = "Sofa", widthM = 2.0f, depthM = 0.9f, heightM = 0.8f, priceUsd = 1200f };

        private static StagingDirector.DirectorItem OwnChair() => new StagingDirector.DirectorItem
        { id = "chair", name = "Client's chair", widthM = 0.7f, depthM = 0.7f, heightM = 1.0f, userFurniture = true };

        [Test]
        public void ParsePlan_ReadsPlacementsAndSummary()
        {
            string json = "{\"summary\":\"Warm modern layout\",\"placements\":[" +
                          "{\"itemId\":\"sofa\",\"x\":2.0,\"z\":1.0,\"yawDegrees\":90,\"note\":\"faces window\"}]}";
            var plan = StagingDirector.ParsePlan(json);

            Assert.AreEqual("Warm modern layout", plan.Summary);
            Assert.AreEqual(1, plan.Placements.Count);
            Assert.AreEqual("sofa", plan.Placements[0].itemId);
            Assert.AreEqual(90f, plan.Placements[0].yawDegrees, 1e-3f);
        }

        [Test]
        public void ParsePlan_BadJson_ReturnsEmptyPlanNotThrow()
        {
            Assert.AreEqual(0, StagingDirector.ParsePlan("not json {{{").Placements.Count);
            Assert.AreEqual(0, StagingDirector.ParsePlan(null).Placements.Count);
            Assert.AreEqual(0, StagingDirector.ParsePlan("{\"placements\":[{\"x\":1}]}").Placements.Count,
                "placement without an itemId must be dropped");
        }

        [Test]
        public void Validate_AcceptsFittingPlacement_AndSumsCatalogCost()
        {
            var plan = new StagingDirector.DirectorPlan
            {
                Placements = { new StagingDirector.PlannedPlacement { itemId = "sofa", x = 2f, z = 1.5f } },
            };
            var v = StagingDirector.Validate(Room(), plan, new[] { Sofa() });

            Assert.AreEqual(1, v.Accepted.Count);
            Assert.AreEqual("sofa", v.Accepted[0].CatalogItemId);
            Assert.IsTrue(string.IsNullOrEmpty(v.Accepted[0].FurnitureAssetId));
            Assert.AreEqual(1200f, v.CatalogCostUsd, 1e-3f);
        }

        [Test]
        public void Validate_UserFurniture_MapsToFurnitureAssetId_AndIsFree()
        {
            var plan = new StagingDirector.DirectorPlan
            {
                Placements = { new StagingDirector.PlannedPlacement { itemId = "chair", x = 2f, z = 1.5f } },
            };
            var v = StagingDirector.Validate(Room(), plan, new[] { OwnChair() });

            Assert.AreEqual(1, v.Accepted.Count);
            Assert.AreEqual("chair", v.Accepted[0].FurnitureAssetId);
            Assert.AreEqual(0f, v.CatalogCostUsd, 1e-3f);
        }

        [Test]
        public void Validate_NearMiss_IsRescuedByNudgeTowardCenter()
        {
            // Sofa centered 0.2m from the wall — footprint (0.45m half-depth) pokes out.
            var plan = new StagingDirector.DirectorPlan
            {
                Placements = { new StagingDirector.PlannedPlacement { itemId = "sofa", x = 2f, z = 0.2f } },
            };
            var v = StagingDirector.Validate(Room(), plan, new[] { Sofa() });

            Assert.AreEqual(1, v.Accepted.Count, "a near-miss should be nudged inward, not rejected");
            Assert.Greater(v.Accepted[0].Position.z, 0.2f, "nudge moves it toward the room center");
        }

        [Test]
        public void Validate_HopelessPlacement_IsRejectedWithReason()
        {
            // Way outside the room — beyond any rescue nudge.
            var plan = new StagingDirector.DirectorPlan
            {
                Placements = { new StagingDirector.PlannedPlacement { itemId = "sofa", x = 40f, z = 40f } },
            };
            var v = StagingDirector.Validate(Room(), plan, new[] { Sofa() });

            Assert.AreEqual(0, v.Accepted.Count);
            Assert.AreEqual(1, v.Rejected.Count);
            StringAssert.Contains("Sofa", v.Rejected[0]);
        }

        [Test]
        public void Validate_UnknownItem_IsRejected()
        {
            var plan = new StagingDirector.DirectorPlan
            {
                Placements = { new StagingDirector.PlannedPlacement { itemId = "made-up", x = 2f, z = 1.5f } },
            };
            var v = StagingDirector.Validate(Room(), plan, new[] { Sofa() });

            Assert.AreEqual(0, v.Accepted.Count);
            StringAssert.Contains("not an offered item", v.Rejected[0]);
        }

        [Test]
        public void BuildRequestJson_CarriesStyleOutlineAndItems()
        {
            string json = StagingDirector.BuildRequestJson("warm modern", Room(),
                new[] { Sofa(), OwnChair() });

            StringAssert.Contains("\"style\":\"warm modern\"", json);
            StringAssert.Contains("\"items\":", json);
            StringAssert.Contains("\"sofa\"", json);
            StringAssert.Contains("\"userFurniture\":true", json);
            StringAssert.Contains("\"outline\":", json);
        }
    }
}
