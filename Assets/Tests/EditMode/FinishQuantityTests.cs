using NUnit.Framework;
using SuperRealEstate.MaterialCost;

namespace SuperRealEstate.Tests
{
    public class FinishQuantityTests
    {
        [Test]
        public void PaintGallons_RoundsUp_WithCoatsAndCoverage()
        {
            // 30 m² ≈ 322.9 ft², 2 coats = 645.8 ft², / 350 ft²/gal = 1.85 → 2 gal.
            Assert.AreEqual(2, FinishQuantity.PaintGallons(30f));
        }

        [Test]
        public void PaintGallons_OneCoat_SmallWall()
        {
            // 10 m² ≈ 107.6 ft², 1 coat / 350 = 0.31 → 1 gal.
            Assert.AreEqual(1, FinishQuantity.PaintGallons(10f, coats: 1));
        }

        [Test]
        public void PaintCost_MultipliesGallonsByPrice()
        {
            // 2 gallons (from 30 m²) × $45/gal = $90 (e.g. a Sherwin-Williams color).
            Assert.AreEqual(90f, FinishQuantity.PaintCost(30f, pricePerGallon: 45f), 0.01f);
        }

        [Test]
        public void FlooringBoxes_IncludesWaste_RoundsUp()
        {
            // 20 m² ≈ 215.3 ft² × 1.10 = 236.8 ft², / 20 ft² per box = 11.84 → 12 boxes.
            Assert.AreEqual(12, FinishQuantity.FlooringBoxes(20f, sqFtPerBox: 20f));
        }

        [Test]
        public void FinishCost_PerSquareMeter()
        {
            var tile = new Material("t", "Tile", "Flooring", MaterialUnit.PerSquareMeter, 10f);
            // 10 m² × $10, no waste.
            Assert.AreEqual(100f, FinishQuantity.FinishCost(10f, tile, wasteFactor: 0f), 0.01f);
        }

        [Test]
        public void FinishCost_PerSquareFoot_ConvertsArea()
        {
            var lvp = new Material("f", "LVP", "Flooring", MaterialUnit.PerSquareFoot, 3f);
            // 10 m² = 107.64 ft² × $3, no waste = 322.9.
            Assert.AreEqual(322.92f, FinishQuantity.FinishCost(10f, lvp, wasteFactor: 0f), 0.1f);
        }
    }
}
