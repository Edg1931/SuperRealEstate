using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using SuperRealEstate.Overlays;
using SuperRealEstate.Construction;
using SuperRealEstate.PropertyData;

namespace SuperRealEstate.Tests
{
    public class OverlaysTests
    {
        private static SystemElement Run(string id, BuildingSystem sys, string spec, params Vector3[] pts)
            => new SystemElement { Id = id, System = sys, Kind = ElementKind.Run, Spec = spec, Points = new List<Vector3>(pts) };

        private static SystemElement Fixture(string id, BuildingSystem sys, Vector3 at)
            => new SystemElement { Id = id, System = sys, Kind = ElementKind.Fixture, Points = new List<Vector3> { at } };

        [Test]
        public void SystemsOverlay_OnePolylinePerRun_WithPointsAndColor()
        {
            var model = new SystemsModel
            {
                Elements =
                {
                    Run("r1", BuildingSystem.Hvac, "6in duct", new Vector3(0, 0, 0), new Vector3(3, 0, 0), new Vector3(3, 0, 4)),
                    // Degenerate run (one point) must be skipped.
                    Run("r2", BuildingSystem.Plumbing, "1/2in PEX", new Vector3(1, 1, 1))
                }
            };

            var overlay = SystemsOverlayBuilder.Build(model);

            Assert.AreEqual(1, overlay.Polylines.Count, "degenerate run should be skipped");
            var line = overlay.Polylines[0];
            Assert.AreEqual(3, line.Points.Count);
            Assert.AreEqual(SystemPalette.ColorFor(BuildingSystem.Hvac), line.Color);
            StringAssert.Contains("Hvac", line.Label);
            StringAssert.Contains("6in duct", line.Label);
        }

        [Test]
        public void SystemsOverlay_OneMarkerPerFixture()
        {
            var model = new SystemsModel
            {
                Elements =
                {
                    Fixture("o1", BuildingSystem.Electrical, new Vector3(2, 1, 0)),
                    Fixture("o2", BuildingSystem.Electrical, new Vector3(3, 1, 0))
                }
            };

            var overlay = SystemsOverlayBuilder.Build(model);

            Assert.AreEqual(2, overlay.Markers.Count);
            Assert.AreEqual(new Vector3(2, 1, 0), overlay.Markers[0].Position);
            Assert.AreEqual(SystemPalette.ColorFor(BuildingSystem.Electrical), overlay.Markers[0].Color);
        }

        [Test]
        public void ParcelOverlay_ClosedBoundary_WithCorrectPointCountAndPerimeter()
        {
            // 10 x 6 m rectangle in plan (X/Z).
            var parcel = new Parcel
            {
                Boundary =
                {
                    new Vector2(0, 0),
                    new Vector2(10, 0),
                    new Vector2(10, 6),
                    new Vector2(0, 6)
                }
            };

            var line = ParcelOverlayBuilder.Build(parcel, floorY: 0.5f);

            Assert.IsNotNull(line);
            Assert.AreEqual(5, line.Points.Count, "4 vertices + closing point");
            Assert.AreEqual(line.Points[0], line.Points[line.Points.Count - 1], "ring must close");
            Assert.AreEqual(0.5f, line.Points[1].y, 0.0001f, "plan Y maps to floorY");
            Assert.AreEqual(0f, line.Points[1].z, 0.0001f, "plan (10,0) -> world (10, y, 0)");

            // Perimeter = 10 + 6 + 10 + 6 = 32 m.
            Assert.AreEqual(32f, ParcelOverlayBuilder.PerimeterM(parcel), 0.001f);
        }

        [Test]
        public void ParcelOverlay_TooFewPoints_ReturnsNull()
        {
            var parcel = new Parcel { Boundary = { new Vector2(0, 0), new Vector2(1, 0) } };
            Assert.IsNull(ParcelOverlayBuilder.Build(parcel));
            Assert.AreEqual(0f, ParcelOverlayBuilder.PerimeterM(parcel));
        }

        [Test]
        public void CompsOverlay_FormatsPriceAndPerSqFt()
        {
            var comps = new List<Comp>
            {
                new Comp { Price = 525000f, SqFt = 1683f } // 525000 / 1683 ≈ 312/ft²
            };

            var tags = CompsOverlayBuilder.Build(comps);

            Assert.AreEqual(1, tags.Count);
            StringAssert.Contains("$525,000", tags[0].Text);
            StringAssert.Contains("$312/ft²", tags[0].Text);
        }

        [Test]
        public void CompsOverlay_DivideByZeroSafe_OmitsPerSqFt()
        {
            var comps = new List<Comp>
            {
                new Comp { Price = 400000f, SqFt = 0f }   // missing sqft
            };

            var tags = CompsOverlayBuilder.Build(comps);

            Assert.AreEqual(1, tags.Count);
            StringAssert.Contains("$400,000", tags[0].Text);
            StringAssert.DoesNotContain("/ft²", tags[0].Text);
        }

        [Test]
        public void CompsOverlay_PrefersProviderPricePerSqFt()
        {
            var comps = new List<Comp>
            {
                new Comp { Price = 525000f, SqFt = 1683f, PricePerSqFt = 300f }
            };

            var tags = CompsOverlayBuilder.Build(comps);

            StringAssert.Contains("$300/ft²", tags[0].Text);
        }
    }
}
