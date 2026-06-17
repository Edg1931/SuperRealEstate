using NUnit.Framework;
using SuperRealEstate.Insights;
using SuperRealEstate.Presentation;
using SuperRealEstate.RoomMeasure;

namespace SuperRealEstate.Tests
{
    public class PresentationTests
    {
        [Test]
        public void InsightCard_Advisory_TakesAdvisoryRole()
        {
            var vm = InsightCardVM.From(new SceneInsight
            {
                Title = "Possible water stain", Severity = InsightSeverity.Caution, IsAdvisory = true,
                Disclaimer = "Confirm with an inspector",
            });
            Assert.AreEqual(UiRole.Advisory, vm.Role);
            Assert.IsTrue(vm.IsAdvisory);
        }

        [Test]
        public void InsightCard_Suggestion_IsAccent()
        {
            var vm = InsightCardVM.From(new SceneInsight { Title = "Bright room", Severity = InsightSeverity.Suggestion });
            Assert.AreEqual(UiRole.Accent, vm.Role);
        }

        [Test]
        public void PlantCard_BuildsFactsAndWarnings()
        {
            var vm = PlantCardVM.From(new PlantIdentification
            {
                CommonName = "Oleander", ScientificName = "Nerium oleander",
                CareLevel = "easy", Water = "low", Sun = "full", MatureSize = "8-12 ft",
                ToxicToPetsOrKids = true, Invasive = false, PollenAllergy = "low", ReplacementCost = 35f,
            });

            Assert.AreEqual("Oleander", vm.Title);
            Assert.Contains("Care: easy", vm.Facts);
            Assert.Contains("~$35", vm.Facts);
            Assert.Contains("Toxic to pets/kids", vm.Warnings);
            Assert.IsFalse(vm.Warnings.Contains("Invasive"));
        }

        [Test]
        public void MeasurementReadout_ShowsMetricAndImperial()
        {
            var m = new RoomMeasurements(12f, 35f, 14f, 2.5f, 30f);
            var vm = MeasurementReadoutVM.From(m);
            StringAssert.Contains("12.0 m²", vm.FloorArea);
            StringAssert.Contains("129 ft²", vm.FloorArea); // 12 m² ≈ 129 ft²
        }
    }
}
