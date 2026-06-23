using NUnit.Framework;
using SuperRealEstate.Platform;

namespace SuperRealEstate.Tests
{
    public class XrCapabilitiesTests
    {
        [Test]
        public void GalaxyXr_AndVisionPro_BothDoRoomMeasurementAndPortals()
        {
            foreach (var p in new[] { XrPlatform.AndroidXrHeadset, XrPlatform.VisionOS })
            {
                var c = XrCapabilityProfiles.For(p);
                Assert.IsTrue(c.SupportsRoomMeasurement, $"{p} should measure rooms");
                Assert.IsTrue(c.SupportsGazeUi, $"{p} should support gaze UI");
                Assert.IsTrue(c.SupportsWallPortal, $"{p} should support wall portals (video passthrough)");
                Assert.IsTrue(c.SharedAnchors, $"{p} should co-locate");
            }
        }

        [Test]
        public void OpticalGlasses_CannotDoWallPortal()
        {
            // Optical see-through can't replace real light → portals don't apply.
            var aura = XrCapabilityProfiles.For(XrPlatform.AndroidXrGlasses);
            Assert.AreEqual(PassthroughKind.OpticalSeeThrough, aura.Passthrough);
            Assert.IsFalse(aura.SupportsWallPortal);
            Assert.IsTrue(aura.SupportsGazeUi);          // still has eye tracking
            Assert.IsFalse(aura.StandaloneCompute);      // tethered
        }

        [Test]
        public void Phones_HaveScreenPassthrough_NoEyeTracking()
        {
            var ios = XrCapabilityProfiles.For(XrPlatform.IosPhone);
            Assert.AreEqual(PassthroughKind.Screen, ios.Passthrough);
            Assert.IsTrue(ios.SupportsWallPortal);       // screen compositing works
            Assert.IsFalse(ios.SupportsGazeUi);          // touch, not gaze
        }
    }
}
