using NUnit.Framework;
using SuperRealEstate.UI;

namespace SuperRealEstate.Tests
{
    public class SpatialComfortTests
    {
        [Test]
        public void Angular_Meters_RoundTrip()
        {
            float meters = SpatialComfort.AngularToMeters(2f, 1.3f);
            float deg = SpatialComfort.MetersToAngular(meters, 1.3f);
            Assert.AreEqual(2f, deg, 0.001f);
        }

        [Test]
        public void ClampDepth_BoundsToComfortRange()
        {
            Assert.AreEqual(SpatialComfort.DepthMaxM, SpatialComfort.ClampDepth(5f), 0.001f);
            Assert.AreEqual(SpatialComfort.DepthMinM, SpatialComfort.ClampDepth(0.2f), 0.001f);
            Assert.AreEqual(1.3f, SpatialComfort.ClampDepth(1.3f), 0.001f);
        }

        [Test]
        public void IsComfortableTargetAt_FlagsSmallTargets()
        {
            // ~5 cm button at 1.3 m ≈ 2.2° → comfortable.
            Assert.IsTrue(SpatialComfort.IsComfortableTargetAt(0.05f, 1.3f));
            // 1 cm at 1.3 m ≈ 0.44° → too small for gaze.
            Assert.IsFalse(SpatialComfort.IsComfortableTargetAt(0.01f, 1.3f));
        }

        [Test]
        public void Interaction_NoMidasTouch()
        {
            Assert.IsFalse(Interaction.CanCommit(GazeState.Hovered));
            Assert.IsFalse(Interaction.CanCommit(GazeState.Idle));
            Assert.IsTrue(Interaction.CanCommit(GazeState.Selected));
        }

        [Test]
        public void Interaction_PrimaryModality_PerDevice()
        {
            Assert.AreEqual(InputModality.GazePinch, Interaction.PrimaryModality(SpatialDevice.VisionPro));
            Assert.AreEqual(InputModality.TouchScreen, Interaction.PrimaryModality(SpatialDevice.Phone));
        }
    }
}
