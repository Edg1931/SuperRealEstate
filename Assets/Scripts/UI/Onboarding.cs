using System.Collections.Generic;

namespace SuperRealEstate.UI
{
    /// <summary>
    /// One screen of the first-run flow. Data-only and calm: a short title, a
    /// sentence of body, and optional per-device input coaching (gaze+pinch vs
    /// tap) — see docs/DESIGN-SYSTEM.md and docs/POLISH-REVIEW.md "Onboarding &
    /// permissions". The view renders these in a Spatial Glass panel.
    /// </summary>
    public readonly struct OnboardingStep
    {
        /// <summary>One short headline.</summary>
        public string Title { get; }

        /// <summary>A sentence or two of plain-language guidance.</summary>
        public string Body { get; }

        /// <summary>
        /// Optional device-specific coaching for how to interact (e.g. "Look at a
        /// wall and pinch" vs "Tap a wall"). Null when the step needs no input
        /// coaching. The view picks wording per <see cref="InputModality"/>.
        /// </summary>
        public string InputCoaching { get; }

        public OnboardingStep(string title, string body, string inputCoaching = null)
        {
            Title = title;
            Body = body;
            InputCoaching = inputCoaching;
        }
    }

    /// <summary>
    /// The "first room in ~60 seconds" first-run arc: welcome → consent → how to
    /// select → measure a room → see the cost. Pure and testable — it only tracks
    /// position in an ordered step list; the view drives <see cref="Advance"/> /
    /// <see cref="Back"/> and renders <see cref="Current"/>. Honors the design
    /// goal of getting someone to value fast, calmly, without hand-holding.
    /// </summary>
    public sealed class OnboardingFlow
    {
        private readonly IReadOnlyList<OnboardingStep> _steps;

        /// <summary>Index of the step currently shown (0-based).</summary>
        public int CurrentIndex { get; private set; }

        public OnboardingFlow() : this(BuildDefaultSteps()) { }

        /// <summary>Construct with custom steps (e.g. to skip consent if already granted).</summary>
        public OnboardingFlow(IReadOnlyList<OnboardingStep> steps)
        {
            _steps = (steps != null && steps.Count > 0) ? steps : BuildDefaultSteps();
            CurrentIndex = 0;
        }

        /// <summary>Total number of steps in the flow.</summary>
        public int StepCount => _steps.Count;

        /// <summary>The step currently shown.</summary>
        public OnboardingStep Current => _steps[CurrentIndex];

        /// <summary>Read-only access to the whole arc (e.g. for a progress dots row).</summary>
        public IReadOnlyList<OnboardingStep> Steps => _steps;

        /// <summary>True once the user has advanced past the final step.</summary>
        public bool IsComplete { get; private set; }

        /// <summary>True while a previous step exists to go back to.</summary>
        public bool CanGoBack => !IsComplete && CurrentIndex > 0;

        /// <summary>
        /// Completion fraction in 0..1: 0 at the first step, 1 when complete.
        /// Drives a calm progress affordance.
        /// </summary>
        public float Progress
        {
            get
            {
                if (IsComplete) return 1f;
                if (_steps.Count <= 1) return 0f;
                return (float)CurrentIndex / _steps.Count;
            }
        }

        /// <summary>
        /// Move to the next step. Advancing past the last step marks the flow
        /// <see cref="IsComplete"/>. Returns false (no-op) once already complete.
        /// </summary>
        public bool Advance()
        {
            if (IsComplete) return false;
            if (CurrentIndex >= _steps.Count - 1)
            {
                IsComplete = true;
                return true;
            }
            CurrentIndex++;
            return true;
        }

        /// <summary>
        /// Move to the previous step. Returns false (no-op) at the first step or
        /// once complete (completion is intentionally one-way).
        /// </summary>
        public bool Back()
        {
            if (!CanGoBack) return false;
            CurrentIndex--;
            return true;
        }

        /// <summary>Skip the rest of the flow (e.g. a "Skip" affordance).</summary>
        public void Skip() => IsComplete = true;

        /// <summary>Restart from the first step (e.g. re-run the tour from settings).</summary>
        public void Reset()
        {
            CurrentIndex = 0;
            IsComplete = false;
        }

        /// <summary>The default "first room in ~60 seconds" arc.</summary>
        public static IReadOnlyList<OnboardingStep> BuildDefaultSteps() => new List<OnboardingStep>
        {
            new OnboardingStep(
                "Your home is the canvas",
                "Point your space at a room and SuperRealEstate measures it, prices finishes, and shows what's possible — right where you're looking."),

            new OnboardingStep(
                "Let us see the room",
                "We use the camera to map walls and floors. Scans stay on your device until you choose to save. You can change this anytime.",
                inputCoaching: "Grant camera and scanning access to continue."),

            new OnboardingStep(
                "Selecting is simple",
                "Look at anything to preview it. A quick gesture confirms — nothing happens just by looking.",
                inputCoaching: "On a headset: look at it, then pinch. On a phone: tap it."),

            new OnboardingStep(
                "Measure your first room",
                "Slowly sweep the room so we can catch the corners. We'll lay down the floor plan and dimensions as you go.",
                inputCoaching: "Look at a wall and pinch to drop a point. On a phone, tap each corner."),

            new OnboardingStep(
                "See the cost",
                "Now the good part: a clear estimate for the space. Prices are advisory — flagged as estimates, never as final bids.")
        };
    }
}
