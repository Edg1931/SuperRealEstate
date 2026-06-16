using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using SuperRealEstate.MaterialCost;
using SuperRealEstate.Renovation;

namespace SuperRealEstate.Tests
{
    public class RenovationTests
    {
        private const float Tol = 0.01f;

        [Test]
        public void Wall_Length_And_SideArea_MinusOpenings()
        {
            var wall = new Wall
            {
                Start = new Vector2(0f, 0f),
                End = new Vector2(4f, 0f),
                HeightM = 2.5f,
                Openings = new List<Opening>
                {
                    new Opening { Kind = OpeningKind.Door, WidthM = 0.9f, HeightM = 2.0f },
                },
            };

            Assert.AreEqual(4f, wall.LengthM, Tol);
            // 4 * 2.5 = 10 m² gross, minus 0.9*2.0 = 1.8 → 8.2 m².
            Assert.AreEqual(8.2f, wall.SideAreaM2, Tol);
        }

        [Test]
        public void Room_FloorArea_FromOutline()
        {
            var room = new RoomDef
            {
                FloorOutline = new List<Vector2>
                {
                    new Vector2(0f, 0f), new Vector2(4f, 0f),
                    new Vector2(4f, 3f), new Vector2(0f, 3f),
                },
            };
            Assert.AreEqual(12f, room.FloorAreaM2, Tol);
        }

        [Test]
        public void LoadBearing_ExteriorPerpendicular_IsLikely()
        {
            var a = LoadBearingAdvisor.Assess(isExterior: true, lengthM: 5f, perpendicularToJoists: true);
            Assert.AreEqual(LoadBearingLikelihood.Likely, a.Likelihood);
            Assert.IsNotEmpty(a.Disclaimer); // never silent
        }

        [Test]
        public void LoadBearing_ShortInteriorParallel_IsLow()
        {
            var a = LoadBearingAdvisor.Assess(isExterior: false, lengthM: 2f, perpendicularToJoists: false);
            Assert.AreEqual(LoadBearingLikelihood.Low, a.Likelihood);
        }

        [Test]
        public void WallRemoval_NonLoadBearing_IsDemoOnly()
        {
            // 10 m² side area ≈ 107.64 ft² × $3/ft² = $322.9, single line item.
            var items = RenovationCostEstimator.WallRemoval(10f, 4f, isLoadBearing: false).ToList();
            Assert.AreEqual(1, items.Count);
            Assert.AreEqual(322.92f, RenovationCostEstimator.Total(items), 0.5f);
        }

        [Test]
        public void WallRemoval_LoadBearing_AddsBeamLine()
        {
            // Demo (10 m² → 107.64 ft² × 3 = 322.9) + beam (4 m → 13.12 ft × $120 = 1574.8).
            var items = RenovationCostEstimator.WallRemoval(10f, 4f, isLoadBearing: true).ToList();
            Assert.AreEqual(2, items.Count);
            Assert.AreEqual(322.92f + 1574.8f, RenovationCostEstimator.Total(items), 1f);
        }

        [Test]
        public void FinishChange_UsesMaterialCost()
        {
            var paint = new Material("p", "Eggshell", "Paint", MaterialUnit.PerSquareMeter, 5f);
            var item = RenovationCostEstimator.FinishChange("Repaint wall", 8.2f, paint, wasteFactor: 0f);
            Assert.AreEqual(41f, item.Subtotal, Tol); // 8.2 × 5
        }
    }
}
