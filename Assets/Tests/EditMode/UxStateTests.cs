using System.Collections.Generic;
using NUnit.Framework;
using SuperRealEstate.UI;

namespace SuperRealEstate.Tests
{
    public class LoadStateTests
    {
        [Test]
        public void Loading_HasLoadingPhase_NoValue()
        {
            var s = LoadState<List<int>>.Loading();
            Assert.AreEqual(LoadPhase.Loading, s.Phase);
            Assert.IsTrue(s.IsLoading);
            Assert.IsFalse(s.HasValue);
            Assert.IsNull(s.Message);
        }

        [Test]
        public void Loaded_WithContent_IsLoaded()
        {
            var s = LoadState<List<int>>.Loaded(new List<int> { 1, 2, 3 });
            Assert.AreEqual(LoadPhase.Loaded, s.Phase);
            Assert.IsTrue(s.IsLoaded);
            Assert.IsTrue(s.HasValue);
            Assert.AreEqual(3, s.Value.Count);
        }

        [Test]
        public void Loaded_WithEmptyCollection_CollapsesToEmpty()
        {
            var s = LoadState<List<int>>.Loaded(new List<int>());
            Assert.AreEqual(LoadPhase.Empty, s.Phase);
            Assert.IsTrue(s.IsEmpty);
            Assert.IsFalse(s.HasValue);
            Assert.IsNotNull(s.Value); // empty list retained for the view
        }

        [Test]
        public void Loaded_WithNull_CollapsesToEmpty()
        {
            var s = LoadState<List<int>>.Loaded(null);
            Assert.AreEqual(LoadPhase.Empty, s.Phase);
            Assert.IsTrue(s.IsEmpty);
        }

        [Test]
        public void Loaded_WithEmptyString_IsEmpty()
        {
            var s = LoadState<string>.Loaded("");
            Assert.AreEqual(LoadPhase.Empty, s.Phase);
        }

        [Test]
        public void Loaded_WithCustomIsEmptyPredicate_Honored()
        {
            // A value type that the default test can't see as empty (0 == "no rooms").
            var s = LoadState<int>.Loaded(0, isEmpty: count => count == 0);
            Assert.AreEqual(LoadPhase.Empty, s.Phase);

            var s2 = LoadState<int>.Loaded(5, isEmpty: count => count == 0);
            Assert.AreEqual(LoadPhase.Loaded, s2.Phase);
            Assert.AreEqual(5, s2.Value);
        }

        [Test]
        public void Error_CarriesMessage()
        {
            var s = LoadState<List<int>>.Error("You're offline.");
            Assert.AreEqual(LoadPhase.Error, s.Phase);
            Assert.IsTrue(s.IsError);
            Assert.AreEqual("You're offline.", s.Message);
            Assert.IsFalse(s.HasValue);
        }

        [Test]
        public void Error_WithBlankMessage_GetsCalmFallback()
        {
            var s = LoadState<int>.Error(null);
            Assert.AreEqual(LoadPhase.Error, s.Phase);
            Assert.IsFalse(string.IsNullOrEmpty(s.Message));
        }

        [Test]
        public void Idle_IsDefaultUnstartedState()
        {
            var s = LoadState<int>.Idle();
            Assert.AreEqual(LoadPhase.Idle, s.Phase);
            Assert.IsTrue(s.IsIdle);
        }
    }

    public class StatePresetTests
    {
        [Test]
        public void ErrorPresets_AreFlaggedAsError_WithRetry()
        {
            Assert.IsTrue(StatePresets.OfflineError.IsError);
            Assert.IsTrue(StatePresets.AlignFailed.IsError);
            Assert.IsNotNull(StatePresets.OfflineError.ActionLabel);
        }

        [Test]
        public void EmptyPresets_AreNotErrors()
        {
            Assert.IsFalse(StatePresets.NoCatalog.IsError);
            Assert.IsFalse(StatePresets.NoRoomsYet.IsError);
        }

        [Test]
        public void FeatureUnavailable_UsesReason_AndIsNotError()
        {
            var c = StatePresets.FeatureUnavailable("Wall removal needs LiDAR.");
            Assert.IsFalse(c.IsError);
            StringAssert.Contains("LiDAR", c.Message);
        }

        [Test]
        public void FeatureUnavailable_WithNoReason_StillHasMessage()
        {
            var c = StatePresets.FeatureUnavailable(null);
            Assert.IsFalse(string.IsNullOrEmpty(c.Message));
        }
    }

    public class OnboardingFlowTests
    {
        [Test]
        public void StartsAtFirstStep_NotComplete()
        {
            var flow = new OnboardingFlow();
            Assert.AreEqual(0, flow.CurrentIndex);
            Assert.IsFalse(flow.IsComplete);
            Assert.IsFalse(flow.CanGoBack);
            Assert.That(flow.StepCount, Is.GreaterThanOrEqualTo(5)); // the "60 second" arc
        }

        [Test]
        public void Advance_MovesForward()
        {
            var flow = new OnboardingFlow();
            Assert.IsTrue(flow.Advance());
            Assert.AreEqual(1, flow.CurrentIndex);
            Assert.IsTrue(flow.CanGoBack);
        }

        [Test]
        public void Back_MovesBackward_StopsAtStart()
        {
            var flow = new OnboardingFlow();
            flow.Advance();
            flow.Advance();
            Assert.IsTrue(flow.Back());
            Assert.AreEqual(1, flow.CurrentIndex);

            flow.Back();
            Assert.AreEqual(0, flow.CurrentIndex);
            Assert.IsFalse(flow.Back()); // no-op at the first step
            Assert.AreEqual(0, flow.CurrentIndex);
        }

        [Test]
        public void AdvancePastLastStep_Completes()
        {
            var flow = new OnboardingFlow();
            for (int i = 0; i < flow.StepCount - 1; i++)
            {
                Assert.IsFalse(flow.IsComplete);
                flow.Advance();
            }
            Assert.IsFalse(flow.IsComplete); // on the last step
            Assert.IsTrue(flow.Advance());   // step past it
            Assert.IsTrue(flow.IsComplete);

            Assert.IsFalse(flow.Advance()); // no-op once complete
            Assert.IsFalse(flow.Back());    // completion is one-way
        }

        [Test]
        public void Progress_GoesFromZeroToOne()
        {
            var flow = new OnboardingFlow();
            Assert.AreEqual(0f, flow.Progress);

            flow.Advance();
            Assert.That(flow.Progress, Is.GreaterThan(0f).And.LessThan(1f));

            while (!flow.IsComplete) flow.Advance();
            Assert.AreEqual(1f, flow.Progress);
        }

        [Test]
        public void Skip_CompletesImmediately()
        {
            var flow = new OnboardingFlow();
            flow.Skip();
            Assert.IsTrue(flow.IsComplete);
            Assert.AreEqual(1f, flow.Progress);
        }

        [Test]
        public void Reset_ReturnsToStart()
        {
            var flow = new OnboardingFlow();
            flow.Skip();
            flow.Reset();
            Assert.IsFalse(flow.IsComplete);
            Assert.AreEqual(0, flow.CurrentIndex);
        }

        [Test]
        public void CustomSteps_AreUsed()
        {
            var steps = new List<OnboardingStep>
            {
                new OnboardingStep("A", "first"),
                new OnboardingStep("B", "second"),
            };
            var flow = new OnboardingFlow(steps);
            Assert.AreEqual(2, flow.StepCount);
            Assert.AreEqual("A", flow.Current.Title);
            flow.Advance();
            Assert.AreEqual("B", flow.Current.Title);
        }

        [Test]
        public void NullOrEmptySteps_FallBackToDefault()
        {
            var flow = new OnboardingFlow(new List<OnboardingStep>());
            Assert.That(flow.StepCount, Is.GreaterThanOrEqualTo(5));
        }

        [Test]
        public void ConsentStep_HasInputCoaching()
        {
            var flow = new OnboardingFlow();
            bool anyCoaching = false;
            foreach (var step in flow.Steps)
            {
                if (!string.IsNullOrEmpty(step.InputCoaching)) anyCoaching = true;
            }
            Assert.IsTrue(anyCoaching); // gaze+pinch vs touch coaching present
        }
    }
}
