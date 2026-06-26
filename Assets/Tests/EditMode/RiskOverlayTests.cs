using System.Collections.Generic;
using NUnit.Framework;
using SuperRealEstate.Overlays;
using SuperRealEstate.PropertyData;

namespace SuperRealEstate.Tests
{
    public class RiskOverlayTests
    {
        [Test]
        public void RiskOverlay_FloodNoiseAndSchool_ProducesTagsWithFormattedText()
        {
            var profile = new PropertyRiskProfile
            {
                Layers =
                {
                    new RiskLayer
                    {
                        Kind = RiskKind.Flood,
                        Rating = RiskRating.Moderate,
                        Detail = "FEMA Zone AE",
                        Source = "FEMA NFHL"
                    },
                    new RiskLayer { Kind = RiskKind.Noise, Rating = RiskRating.Low }
                },
                Schools =
                {
                    new SchoolInfo { Name = "Lincoln Elementary", Level = "Elementary", Rating = 8, DistanceMiles = 0.4f }
                }
            };

            var tags = RiskOverlayBuilder.Build(profile);

            // 2 layers + 1 school = 3 tags.
            Assert.AreEqual(3, tags.Count);
            Assert.AreEqual("Flood: Moderate · FEMA Zone AE", tags[0].Text);
            Assert.AreEqual("Noise: Low", tags[1].Text);
            Assert.AreEqual("Lincoln Elementary · 8/10 · 0.4 mi", tags[2].Text);
        }

        [Test]
        public void RiskOverlay_RatingColor_HighDiffersFromLow()
        {
            Assert.AreNotEqual(
                RiskOverlayBuilder.RatingColor(RiskRating.High),
                RiskOverlayBuilder.RatingColor(RiskRating.Low),
                "high caution must read differently from a positive/low rating");

            // None and Low share the positive band; Moderate is its own caution band.
            Assert.AreEqual(
                RiskOverlayBuilder.RatingColor(RiskRating.None),
                RiskOverlayBuilder.RatingColor(RiskRating.Low));
            Assert.AreNotEqual(
                RiskOverlayBuilder.RatingColor(RiskRating.Moderate),
                RiskOverlayBuilder.RatingColor(RiskRating.High));
        }

        [Test]
        public void RiskOverlay_SchoolColor_HighRatingDiffersFromLowRating()
        {
            Assert.AreNotEqual(
                RiskOverlayBuilder.SchoolColor(9),
                RiskOverlayBuilder.SchoolColor(2));
        }

        [Test]
        public void RiskOverlay_EmptyProfile_IsSafeAndYieldsNoTags()
        {
            Assert.AreEqual(0, RiskOverlayBuilder.Build(new PropertyRiskProfile()).Count);
        }

        [Test]
        public void RiskOverlay_NullProfile_ReturnsEmptyList()
        {
            var tags = RiskOverlayBuilder.Build(null);
            Assert.IsNotNull(tags);
            Assert.AreEqual(0, tags.Count);
        }

        [Test]
        public void RiskOverlay_NullEntries_AreSkipped()
        {
            var profile = new PropertyRiskProfile
            {
                Layers = new List<RiskLayer> { null, new RiskLayer { Kind = RiskKind.Wildfire, Rating = RiskRating.High } },
                Schools = new List<SchoolInfo> { null }
            };

            var tags = RiskOverlayBuilder.Build(profile);

            Assert.AreEqual(1, tags.Count);
            Assert.AreEqual("Wildfire: High", tags[0].Text);
        }

        [Test]
        public void RiskOverlay_School_OmitsRatingAndDistanceWhenUnavailable()
        {
            var profile = new PropertyRiskProfile
            {
                Schools = { new SchoolInfo { Name = "Riverside High", Level = "High", Rating = 0, DistanceMiles = 0f } }
            };

            var tags = RiskOverlayBuilder.Build(profile);

            Assert.AreEqual(1, tags.Count);
            Assert.AreEqual("Riverside High", tags[0].Text);
        }
    }
}
