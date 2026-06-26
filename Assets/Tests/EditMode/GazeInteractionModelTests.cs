using NUnit.Framework;
using SuperRealEstate.UI;

namespace SuperRealEstate.Tests
{
    public class GazeInteractionModelTests
    {
        [Test]
        public void Commit_WithNoGaze_DoesNothing()
        {
            var m = new GazeInteractionModel();
            Assert.IsFalse(m.Commit());
            Assert.AreEqual(GazeState.Idle, m.State);
            Assert.IsNull(m.SelectedId);
        }

        [Test]
        public void Gaze_ThenCommit_Selects()
        {
            var m = new GazeInteractionModel();
            m.GazeEnter("card-1");
            Assert.AreEqual(GazeState.Hovered, m.State);

            Assert.IsTrue(m.Commit());
            Assert.AreEqual(GazeState.Selected, m.State);
            Assert.AreEqual("card-1", m.SelectedId);
        }

        [Test]
        public void GazeExit_ClearsHover()
        {
            var m = new GazeInteractionModel();
            m.GazeEnter("card-1");
            m.GazeExit();
            Assert.IsNull(m.HoveredId);
            Assert.AreEqual(GazeState.Idle, m.State);
        }

        [Test]
        public void HoverEvent_Fires_OnEnter()
        {
            var m = new GazeInteractionModel();
            string hovered = null;
            m.Hovered += id => hovered = id;
            m.GazeEnter("wall-tag");
            Assert.AreEqual("wall-tag", hovered);
        }

        [Test]
        public void Deselect_ReturnsToHoverOrIdle()
        {
            var m = new GazeInteractionModel();
            m.GazeEnter("a");
            m.Commit();
            m.Deselect();
            Assert.AreEqual(GazeState.Hovered, m.State); // still gazing at "a"
            m.GazeExit();
            m.Deselect();
            Assert.AreEqual(GazeState.Idle, m.State);
        }
    }
}
