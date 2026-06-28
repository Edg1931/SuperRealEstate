using System.Collections.Generic;

namespace SuperRealEstate.Onboarding
{
    /// <summary>The ordered first-run steps.</summary>
    public enum OnboardingStep
    {
        Welcome,        // what the app does
        Consent,        // grant camera / scan / etc.
        SignIn,         // optional account (needed for cloud sync + sessions)
        ReadyToScan,    // hand-off into the first room scan / measure
        Done
    }

    /// <summary>
    /// Pure state machine for first-run onboarding: tracks progress, decides the
    /// next step, and knows when onboarding can complete. Drives the UI; the AR
    /// layer persists <see cref="IsComplete"/> so returning users skip it. No
    /// Unity dependency — fully unit-tested.
    ///
    /// Rules:
    ///  • Consent is the gating step: at least Camera OR SceneScan must be decided
    ///    (granted or explicitly declined) before moving past it — we never
    ///    silently skip the consent ask.
    ///  • Sign-in is optional (you can use the app locally), so it can be skipped.
    ///  • Onboarding completes when every required step is satisfied.
    /// </summary>
    public sealed class OnboardingFlow
    {
        private readonly HashSet<OnboardingStep> _completed = new HashSet<OnboardingStep>();

        public OnboardingStep Current { get; private set; } = OnboardingStep.Welcome;

        /// <summary>True once onboarding has reached <see cref="OnboardingStep.Done"/>.</summary>
        public bool IsComplete => Current == OnboardingStep.Done;

        /// <summary>Mark the current step satisfied and advance to the next required one.</summary>
        public OnboardingStep Advance(ConsentLedger consent)
        {
            _completed.Add(Current);
            Current = NextAfter(Current, consent);
            return Current;
        }

        /// <summary>Skip an optional step (only Sign-in is skippable).</summary>
        public OnboardingStep Skip(ConsentLedger consent)
        {
            if (Current == OnboardingStep.SignIn)
            {
                _completed.Add(OnboardingStep.SignIn);
                Current = NextAfter(OnboardingStep.SignIn, consent);
            }
            return Current;
        }

        /// <summary>Has a step been completed (or skipped)?</summary>
        public bool IsStepDone(OnboardingStep step) => _completed.Contains(step);

        /// <summary>
        /// Whether the user may leave the Consent step yet — at least one of the
        /// capture consents must be decided so the ask isn't silently bypassed.
        /// </summary>
        public static bool CanLeaveConsent(ConsentLedger consent)
            => consent != null
               && (consent.HasDecided(ConsentType.Camera) || consent.HasDecided(ConsentType.SceneScan));

        private static OnboardingStep NextAfter(OnboardingStep step, ConsentLedger consent)
        {
            switch (step)
            {
                case OnboardingStep.Welcome:
                    return OnboardingStep.Consent;
                case OnboardingStep.Consent:
                    // Stay on Consent until the user has decided a capture consent.
                    return CanLeaveConsent(consent) ? OnboardingStep.SignIn : OnboardingStep.Consent;
                case OnboardingStep.SignIn:
                    return OnboardingStep.ReadyToScan;
                case OnboardingStep.ReadyToScan:
                    return OnboardingStep.Done;
                default:
                    return OnboardingStep.Done;
            }
        }
    }
}
