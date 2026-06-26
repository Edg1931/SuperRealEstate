using System.Collections.Generic;
using NUnit.Framework;
using SuperRealEstate.PropertyData;

namespace SuperRealEstate.Tests
{
    public class CompStatsTests
    {
        private static Comp MakeComp(float price, float sqft, float ppsf = 0f)
            => new Comp { Price = price, SqFt = sqft, PricePerSqFt = ppsf };

        [Test]
        public void MedianPricePerSqFt_OddCount_PicksMiddle()
        {
            var comps = new List<Comp>
            {
                MakeComp(100_000f, 1_000f), // 100/ft²
                MakeComp(300_000f, 1_000f), // 300/ft²
                MakeComp(200_000f, 1_000f), // 200/ft²
            };
            // Sorted $/ft²: 100, 200, 300 → median 200.
            Assert.AreEqual(200f, CompStats.MedianPricePerSqFt(comps), 0.01f);
        }

        [Test]
        public void MedianPricePerSqFt_EvenCount_AveragesMiddlePair()
        {
            var comps = new List<Comp>
            {
                MakeComp(100_000f, 1_000f), // 100
                MakeComp(200_000f, 1_000f), // 200
                MakeComp(300_000f, 1_000f), // 300
                MakeComp(600_000f, 1_000f), // 600
            };
            // Sorted: 100, 200, 300, 600 → median (200+300)/2 = 250.
            Assert.AreEqual(250f, CompStats.MedianPricePerSqFt(comps), 0.01f);
        }

        [Test]
        public void MedianPricePerSqFt_PrefersExplicitPricePerSqFt_FallsBackToDerived()
        {
            var comps = new List<Comp>
            {
                MakeComp(0f, 0f, ppsf: 150f),      // explicit only
                MakeComp(500_000f, 2_000f),         // derived 250
            };
            // Values 150 and 250 → median 200.
            Assert.AreEqual(200f, CompStats.MedianPricePerSqFt(comps), 0.01f);
        }

        [Test]
        public void MedianPricePerSqFt_IgnoresCompsWithoutUsableSqFt()
        {
            var comps = new List<Comp>
            {
                MakeComp(500_000f, 0f), // no sqft, no ppsf → ignored
                MakeComp(400_000f, 2_000f), // 200/ft²
            };
            Assert.AreEqual(200f, CompStats.MedianPricePerSqFt(comps), 0.01f);
        }

        [Test]
        public void AveragePrice_MeanOfPrices()
        {
            var comps = new List<Comp>
            {
                MakeComp(100_000f, 1_000f),
                MakeComp(200_000f, 1_000f),
                MakeComp(300_000f, 1_000f),
            };
            Assert.AreEqual(200_000f, CompStats.AveragePrice(comps), 0.01f);
        }

        [Test]
        public void PriceRange_ReturnsMinAndMax()
        {
            var comps = new List<Comp>
            {
                MakeComp(250_000f, 1_000f),
                MakeComp(180_000f, 1_000f),
                MakeComp(420_000f, 1_000f),
            };
            var (min, max) = CompStats.PriceRange(comps);
            Assert.AreEqual(180_000f, min, 0.01f);
            Assert.AreEqual(420_000f, max, 0.01f);
        }

        [Test]
        public void EstimateFromComps_MedianTimesSubjectArea()
        {
            var comps = new List<Comp>
            {
                MakeComp(100_000f, 1_000f), // 100
                MakeComp(200_000f, 1_000f), // 200
                MakeComp(300_000f, 1_000f), // 300
            };
            // Median 200/ft² × 1,500 ft² = 300,000.
            Assert.AreEqual(300_000f, CompStats.EstimateFromComps(1_500f, comps), 0.01f);
        }

        // --- empty / safety ---

        [Test]
        public void MedianPricePerSqFt_EmptyOrNull_ReturnsZero()
        {
            Assert.AreEqual(0f, CompStats.MedianPricePerSqFt(new List<Comp>()));
            Assert.AreEqual(0f, CompStats.MedianPricePerSqFt(null));
        }

        [Test]
        public void AveragePrice_EmptyOrNull_ReturnsZero()
        {
            Assert.AreEqual(0f, CompStats.AveragePrice(new List<Comp>()));
            Assert.AreEqual(0f, CompStats.AveragePrice(null));
        }

        [Test]
        public void PriceRange_Empty_ReturnsZeroZero()
        {
            var (min, max) = CompStats.PriceRange(new List<Comp>());
            Assert.AreEqual(0f, min);
            Assert.AreEqual(0f, max);
        }

        [Test]
        public void EstimateFromComps_NoComps_ReturnsZero()
        {
            Assert.AreEqual(0f, CompStats.EstimateFromComps(1_500f, new List<Comp>()));
        }

        [Test]
        public void EstimateFromComps_NonPositiveArea_ReturnsZero()
        {
            var comps = new List<Comp> { MakeComp(200_000f, 1_000f) };
            Assert.AreEqual(0f, CompStats.EstimateFromComps(0f, comps));
            Assert.AreEqual(0f, CompStats.EstimateFromComps(-100f, comps));
        }
    }
}
