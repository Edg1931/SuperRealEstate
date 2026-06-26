using NUnit.Framework;
using SuperRealEstate.Platform;

namespace SuperRealEstate.Tests
{
    public class FeatureAvailabilityTests
    {
        [Test]
        public void GalaxyXr_HasFullSpatialFeatureSet()
        {
            var c = XrCapabilityProfiles.For(XrPlatform.AndroidXrHeadset);
            Assert.IsTrue(FeatureAvailability.IsAvailable(c, AppFeature.RoomMeasurement));
            Assert.IsTrue(FeatureAvailability.IsAvailable(c, AppFeature.WallRemovalPortal));
            Assert.IsTrue(FeatureAvailability.IsAvailable(c, AppFeature.GazeUi));
            Assert.IsTrue(FeatureAvailability.IsAvailable(c, AppFeature.SharedSession));
        }

        [Test]
        public void OpticalGlasses_GateOutPortalAndMeasurement_WithReasons()
        {
            var c = XrCapabilityProfiles.For(XrPlatform.AndroidXrGlasses);
            Assert.IsFalse(FeatureAvailability.IsAvailable(c, AppFeature.WallRemovalPortal));
            Assert.IsFalse(FeatureAvailability.IsAvailable(c, AppFeature.RoomMeasurement)); // no scene depth
            Assert.IsTrue(FeatureAvailability.IsAvailable(c, AppFeature.GazeUi));            // still eye-tracked
            StringAssert.Contains("optical", FeatureAvailability.Reason(c, AppFeature.WallRemovalPortal));
        }

        [Test]
        public void Phone_NoGazeUi_ButPortalViaScreen()
        {
            var c = XrCapabilityProfiles.For(XrPlatform.IosPhone);
            Assert.IsFalse(FeatureAvailability.IsAvailable(c, AppFeature.GazeUi));
            Assert.IsTrue(FeatureAvailability.IsAvailable(c, AppFeature.WallRemovalPortal));
            Assert.IsTrue(FeatureAvailability.IsAvailable(c, AppFeature.LandscapeCalculators));
        }

        [Test]
        public void Resolve_CoversEveryFeature()
        {
            var c = XrCapabilityProfiles.For(XrPlatform.VisionOS);
            var all = FeatureAvailability.Resolve(c);
            Assert.AreEqual(System.Enum.GetValues(typeof(AppFeature)).Length, all.Count);
        }
    }
}
