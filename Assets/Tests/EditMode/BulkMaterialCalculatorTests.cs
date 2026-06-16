using NUnit.Framework;
using SuperRealEstate.Landscape;

namespace SuperRealEstate.Tests
{
    public class BulkMaterialCalculatorTests
    {
        [Test]
        public void Mulch_FlowerBed_CubicYards()
        {
            // 10 m² bed (≈107.64 ft²) at 3" deep = 26.91 ft³ ≈ 0.997 cu yd.
            Assert.AreEqual(0.997f, BulkMaterialCalculator.CubicYards(10f, 3f), 0.01f);
        }

        [Test]
        public void Mulch_FlowerBed_Bags_RoundsUp()
        {
            // 26.91 ft³ / 2 ft³ per bag = 13.45 → 14 bags.
            Assert.AreEqual(14, BulkMaterialCalculator.Bags(10f, 3f));
        }

        [Test]
        public void Concrete_Slab_Volume()
        {
            // 20 m² (≈215.3 ft²) at 4" = 71.76 ft³ ≈ 2.658 cu yd.
            Assert.AreEqual(2.658f, BulkMaterialCalculator.ConcreteCubicYards(20f, 4f), 0.01f);
        }

        [Test]
        public void Sod_IncludesWaste()
        {
            // 50 m² (≈538.2 ft²) × 1.05 = 565.1 ft².
            Assert.AreEqual(565.1f, BulkMaterialCalculator.SodSqFt(50f), 0.5f);
        }

        [Test]
        public void Pavers_Count_WithWaste()
        {
            // 10 m² (≈107.64 ft²) × 1.10 = 118.4 ft²; 6"×12" paver = 0.5 ft² → 237 pavers.
            Assert.AreEqual(237, BulkMaterialCalculator.Pavers(10f, 6f, 12f));
        }

        [Test]
        public void Fence_PostsAndPanels()
        {
            // 24.384 m run = 80 ft; 8 ft panels → 10 panels, 11 posts.
            var f = BulkMaterialCalculator.Fence(24.384f);
            Assert.AreEqual(80f, f.LinearFeet, 0.1f);
            Assert.AreEqual(10, f.Panels);
            Assert.AreEqual(11, f.Posts);
        }
    }
}
