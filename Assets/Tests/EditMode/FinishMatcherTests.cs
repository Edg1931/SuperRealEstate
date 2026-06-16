using System.Collections.Generic;
using NUnit.Framework;
using SuperRealEstate.Catalog;
using SuperRealEstate.Insights;
using SuperRealEstate.MaterialCost;

namespace SuperRealEstate.Tests
{
    public class FinishMatcherTests
    {
        private static ProductCandidate Paint(string id, string brand, string name, string code, string hex, float price)
            => ProductCandidate.FromMaterial(new Material
            {
                Id = id, Name = name, Category = "Paint", Unit = MaterialUnit.PerGallon,
                PricePerUnit = price, Brand = brand, ProductCode = code, ColorHex = hex,
            });

        private static List<ProductCandidate> Catalog() => new List<ProductCandidate>
        {
            Paint("sw7029", "Sherwin-Williams", "Agreeable Gray", "SW 7029", "#D1CBC1", 45f),
            Paint("sw7015", "Sherwin-Williams", "Repose Gray",    "SW 7015", "#CCC9C0", 45f),
            Paint("behr12", "Behr",             "Swiss Coffee",   "12",      "#EAE3D3", 38f),
            ProductCandidate.FromMaterial(new Material
            {
                Id = "lvp", Name = "Luxury Vinyl Plank", Category = "Flooring",
                Unit = MaterialUnit.PerSquareFoot, PricePerUnit = 3f,
            }),
        };

        [Test]
        public void Resolves_SherwinWilliams_AgreeableGray()
        {
            var finding = new SurfaceFinding
            {
                MaterialType = "Paint",
                Brand = "Sherwin-Williams",
                Product = "Agreeable Gray SW 7029",
                ColorHex = "#D1CBC1",
            };

            var match = FinishMatcher.Best(finding, Catalog());

            Assert.IsTrue(match.HasValue);
            Assert.AreEqual("sw7029", match.Value.Product.Id);
            Assert.Greater(match.Value.Confidence, 0.8f);
            Assert.AreEqual("sku", match.Value.Basis); // SW 7029 present in the recognized text
        }

        [Test]
        public void Sku_Drives_Match_OverColorAndBrandNoise()
        {
            // Recognized product text carries the code but a slightly off name.
            var finding = new SurfaceFinding
            {
                MaterialType = "Paint",
                Brand = "Sherwin Williams",
                Product = "SW 7015 gray",
                ColorHex = "#000000", // misleading color
            };

            var match = FinishMatcher.Best(finding, Catalog());
            Assert.AreEqual("sw7015", match.Value.Product.Id);
        }

        [Test]
        public void Prefers_Paint_Over_Flooring_ByCategory()
        {
            var finding = new SurfaceFinding { MaterialType = "Paint", Product = "some warm white" };
            var match = FinishMatcher.Best(finding, Catalog());
            Assert.AreEqual("Paint", match.Value.Product.Category);
        }

        [Test]
        public void EmptyCandidates_ReturnsNull()
        {
            var finding = new SurfaceFinding { Product = "Agreeable Gray" };
            Assert.IsNull(FinishMatcher.Best(finding, new List<ProductCandidate>()));
        }

        [Test]
        public void ColorProximity_IdenticalIsOne_DistantIsLow()
        {
            Assert.AreEqual(1f, FinishMatcher.ColorProximity("#D1CBC1", "#D1CBC1"), 0.001f);
            Assert.Less(FinishMatcher.ColorProximity("#000000", "#FFFFFF"), 0.01f);
        }

        [Test]
        public void TokenOverlap_PartialNames()
        {
            // "agreeable gray" vs "agreeable gray sw 7029": 2 shared of 4 union = 0.5
            Assert.AreEqual(0.5f, FinishMatcher.TokenOverlap("agreeable gray", "agreeable gray sw 7029"), 0.001f);
        }
    }
}
