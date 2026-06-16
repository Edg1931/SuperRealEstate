using NUnit.Framework;
using SuperRealEstate.MaterialCost;
using SuperRealEstate.RoomMeasure;

namespace SuperRealEstate.Tests
{
    public class CostEstimatorTests
    {
        private const float Tolerance = 0.01f;

        // 12 m² floor, 35 m² walls, 14m perimeter, 2.5m height, 30 m³.
        private static RoomMeasurements SampleRoom() =>
            new RoomMeasurements(
                floorAreaSqM: 12f,
                wallAreaSqM: 35f,
                perimeterM: 14f,
                ceilingHeightM: 2.5f,
                volumeCubicM: 30f);

        [Test]
        public void Estimate_FloorPerSqFt_AppliesWaste()
        {
            // 12 m² = 129.167 ft². At $3/ft² with 10% waste:
            // 129.167 * 1.10 * 3 = 426.25
            var laminate = new Material("floor-laminate", "Laminate", "Flooring", MaterialUnit.PerSquareFoot, 3.00f);
            var item = CostEstimator.Estimate(SurfaceType.Floor, laminate, SampleRoom());

            Assert.AreEqual(0.10f, item.WasteFactor, Tolerance);
            Assert.AreEqual(129.167f, item.Quantity, 0.01f);
            Assert.AreEqual(426.25f, item.Subtotal, 0.1f);
        }

        [Test]
        public void Estimate_WallsPaintPerSqM_UsesWallArea()
        {
            // 35 m² walls, $5/m², 5% waste -> 35 * 1.05 * 5 = 183.75
            var paint = new Material("wall-paint", "Eggshell Paint", "Paint", MaterialUnit.PerSquareMeter, 5.00f);
            var item = CostEstimator.Estimate(SurfaceType.Walls, paint, SampleRoom());

            Assert.AreEqual(35f, item.Quantity, Tolerance);
            Assert.AreEqual(183.75f, item.Subtotal, Tolerance);
        }

        [Test]
        public void Estimate_TrimPerLinearFoot_UsesPerimeter()
        {
            // 14m perimeter = 45.93 ft, $2/ft, 10% waste -> 45.93 * 1.10 * 2 = 101.05
            var trim = new Material("trim-base", "Baseboard", "Trim", MaterialUnit.PerLinearFoot, 2.00f);
            var item = CostEstimator.Estimate(SurfaceType.Trim, trim, SampleRoom());

            Assert.AreEqual(45.932f, item.Quantity, 0.01f);
            Assert.AreEqual(101.05f, item.Subtotal, 0.1f);
        }

        [Test]
        public void Estimate_ExplicitWaste_OverridesDefault()
        {
            var tile = new Material("floor-tile", "Tile", "Flooring", MaterialUnit.PerSquareMeter, 10f);
            var item = CostEstimator.Estimate(SurfaceType.Floor, tile, SampleRoom(), wasteFactor: 0f);

            Assert.AreEqual(0f, item.WasteFactor, Tolerance);
            Assert.AreEqual(120f, item.Subtotal, Tolerance); // 12 * 1.0 * 10
        }

        [Test]
        public void Total_SumsLineItems()
        {
            var floor = new Material("f", "Floor", "Flooring", MaterialUnit.PerSquareMeter, 10f);
            var walls = new Material("w", "Walls", "Paint", MaterialUnit.PerSquareMeter, 5f);

            var room = SampleRoom();
            var items = new[]
            {
                CostEstimator.Estimate(SurfaceType.Floor, floor, room, 0f), // 120
                CostEstimator.Estimate(SurfaceType.Walls, walls, room, 0f), // 175
            };

            Assert.AreEqual(295f, CostEstimator.Total(items), Tolerance);
        }
    }
}
