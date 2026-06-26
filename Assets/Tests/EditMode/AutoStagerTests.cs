using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using SuperRealEstate.Collaboration;
using SuperRealEstate.Projects;
using SuperRealEstate.Renovation;
using SuperRealEstate.Staging;

namespace SuperRealEstate.Tests
{
    public class AutoStagerTests
    {
        private static BuildingModel OneWallModel() => new BuildingModel
        {
            Id = "m1",
            Walls = new List<Wall> { new Wall { Id = "w1", Start = new Vector2(0,0), End = new Vector2(4,0), HeightM = 2.5f } },
        };

        private static BlueprintTransform Identity() => BlueprintTransform.Solve(
            new Vector2(0,0), new Vector2(1,0), new Vector2(0,0), new Vector2(1,0));

        [Test]
        public void StageFromBlueprint_AppliesRenovation_AndPlacesFurniture()
        {
            var plan = new RenovationPlan { Edits = { new RenovationEdit { Kind = EditKind.RemoveWall, TargetId = "w1" } } };
            var placements = new List<BlueprintPlacement>
            {
                new BlueprintPlacement { CatalogItemId = "sofa", PlanPosition = new Vector2(2, 0) },
            };

            var scene = AutoStager.StageFromBlueprint(OneWallModel(), plan, placements, Identity());

            Assert.AreEqual(0, scene.Model.Walls.Count);              // wall removed
            Assert.AreEqual(1, scene.Placements.Count);
            Assert.AreEqual("sofa", scene.Placements[0].CatalogItemId);
            Assert.AreEqual(new Vector3(2f, 0f, 0f), scene.Placements[0].Position); // plan (2,0) → world
        }

        [Test]
        public void StageFromAnchor_NoPlan_KeepsModel_AndPlacesAnchorRelative()
        {
            var anchorLocal = new List<Placement> { new Placement("chair", new Vector3(1, 0, 1)) };
            var frame = new AnchorFrame(Pose.identity);

            var scene = AutoStager.StageFromAnchor(OneWallModel(), null, anchorLocal, frame);

            Assert.AreEqual(1, scene.Model.Walls.Count);             // unchanged
            Assert.AreEqual(new Vector3(1f, 0f, 1f), scene.Placements[0].Position);
        }

        [Test]
        public void EmptyStaging_NullModel_StillPlacesFurniture()
        {
            var placements = new List<BlueprintPlacement> { new BlueprintPlacement { FurnitureAssetId = "mine", PlanPosition = new Vector2(1, 1) } };
            var scene = AutoStager.StageFromBlueprint(null, null, placements, Identity());

            Assert.IsNull(scene.Model);
            Assert.AreEqual(1, scene.Placements.Count);
        }
    }
}
