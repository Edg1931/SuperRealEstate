using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using SuperRealEstate.MaterialCost;
using SuperRealEstate.Renovation;

namespace SuperRealEstate.Tests
{
    public class RenovationEngineTests
    {
        private static BuildingModel BaseModel() => new BuildingModel
        {
            Id = "m1",
            Walls = new List<Wall>
            {
                new Wall { Id = "w1", Start = new Vector2(0f, 0f), End = new Vector2(4f, 0f), HeightM = 2.5f, IsExterior = true },
                new Wall { Id = "w2", Start = new Vector2(0f, 0f), End = new Vector2(0f, 3f), HeightM = 2.5f },
            },
            Rooms = new List<RoomDef>
            {
                new RoomDef
                {
                    Id = "room1", CeilingHeightM = 2.5f,
                    FloorOutline = new List<Vector2>
                    {
                        new Vector2(0,0), new Vector2(4,0), new Vector2(4,3), new Vector2(0,3),
                    },
                },
            },
        };

        private static RenovationPlan Plan(params RenovationEdit[] edits) =>
            new RenovationPlan { BuildingModelId = "m1", Edits = edits.ToList() };

        [Test]
        public void RemoveWall_DropsWall_AndDoesNotMutateBase()
        {
            var baseModel = BaseModel();
            var result = RenovationEngine.Apply(baseModel, Plan(
                new RenovationEdit { Kind = EditKind.RemoveWall, TargetId = "w1" }));

            Assert.AreEqual(1, result.Walls.Count);
            Assert.AreEqual("w2", result.Walls[0].Id);
            Assert.AreEqual(2, baseModel.Walls.Count); // base untouched
        }

        [Test]
        public void AddWall_AppendsWall()
        {
            var result = RenovationEngine.Apply(BaseModel(), Plan(
                new RenovationEdit { Kind = EditKind.AddWall, TargetId = "w3", Start = new Vector2(4, 0), End = new Vector2(4, 3) }));
            Assert.AreEqual(3, result.Walls.Count);
            Assert.IsTrue(result.Walls.Any(w => w.Id == "w3"));
        }

        [Test]
        public void ChangeCeilingHeight_UpdatesRoom()
        {
            var result = RenovationEngine.Apply(BaseModel(), Plan(
                new RenovationEdit { Kind = EditKind.ChangeCeilingHeight, TargetId = "room1", Value = 3.0f }));
            Assert.AreEqual(3.0f, result.Rooms[0].CeilingHeightM, 0.001f);
        }

        [Test]
        public void ChangeWallFinish_SetsMaterial()
        {
            var result = RenovationEngine.Apply(BaseModel(), Plan(
                new RenovationEdit { Kind = EditKind.ChangeWallFinish, TargetId = "w2", MaterialId = "sw7029" }));
            var w2 = result.Walls.First(w => w.Id == "w2");
            Assert.AreEqual("sw7029", w2.LeftFinishMaterialId);
            Assert.AreEqual("sw7029", w2.RightFinishMaterialId);
        }

        [Test]
        public void EstimatePlanCost_SumsDemoAndFinish()
        {
            var floor = new Material("lvp", "LVP", "Flooring", MaterialUnit.PerSquareMeter, 10f);
            Material Resolve(string id) => id == "lvp" ? floor : null;

            var plan = Plan(
                new RenovationEdit { Kind = EditKind.RemoveWall, TargetId = "w1" },          // exterior → demo + beam
                new RenovationEdit { Kind = EditKind.ChangeFloorFinish, TargetId = "room1", MaterialId = "lvp" });

            var items = RenovationEngine.EstimatePlanCost(BaseModel(), plan, Resolve);

            // Wall w1: 10 m² side → 107.64 ft² × $3 = 322.92 demo;
            //          4 m → 13.12 ft × $120 = 1574.8 beam (exterior).
            // Floor: 12 m² × $10 × 1.10 waste = 132.
            float total = RenovationCostEstimator.Total(items);
            Assert.AreEqual(322.92f + 1574.8f + 132f, total, 2f);
        }
    }
}
