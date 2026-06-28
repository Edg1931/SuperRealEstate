using NUnit.Framework;
using SuperRealEstate.Onboarding;

namespace SuperRealEstate.Tests
{
    public class OnboardingFlowTests
    {
        [Test]
        public void StartsAtWelcome_NotComplete()
        {
            var flow = new OnboardingFlow();
            Assert.AreEqual(OnboardingStep.Welcome, flow.Current);
            Assert.IsFalse(flow.IsComplete);
        }

        [Test]
        public void Consent_BlocksUntilACaptureConsentIsDecided()
        {
            var flow = new OnboardingFlow();
            var consent = new ConsentLedger();

            flow.Advance(consent);                       // Welcome -> Consent
            Assert.AreEqual(OnboardingStep.Consent, flow.Current);

            flow.Advance(consent);                       // still Consent (nothing decided)
            Assert.AreEqual(OnboardingStep.Consent, flow.Current);

            consent.Grant(ConsentType.Camera);
            flow.Advance(consent);                       // Consent -> SignIn
            Assert.AreEqual(OnboardingStep.SignIn, flow.Current);
        }

        [Test]
        public void DenyingCaptureConsent_StillCountsAsDecided_AndProceeds()
        {
            var flow = new OnboardingFlow();
            var consent = new ConsentLedger();
            consent.Deny(ConsentType.SceneScan); // user said no — but they decided

            flow.Advance(consent); // Welcome -> Consent
            flow.Advance(consent); // Consent -> SignIn (decided, even if denied)
            Assert.AreEqual(OnboardingStep.SignIn, flow.Current);
        }

        [Test]
        public void SignIn_IsSkippable_AndFlowCompletes()
        {
            var flow = new OnboardingFlow();
            var consent = new ConsentLedger();
            consent.Grant(ConsentType.Camera);

            flow.Advance(consent); // Welcome -> Consent
            flow.Advance(consent); // Consent -> SignIn
            flow.Skip(consent);    // SignIn -> ReadyToScan
            Assert.AreEqual(OnboardingStep.ReadyToScan, flow.Current);
            Assert.IsTrue(flow.IsStepDone(OnboardingStep.SignIn));

            flow.Advance(consent); // ReadyToScan -> Done
            Assert.AreEqual(OnboardingStep.Done, flow.Current);
            Assert.IsTrue(flow.IsComplete);
        }

        [Test]
        public void Skip_OnlyAffectsSignIn()
        {
            var flow = new OnboardingFlow();
            var consent = new ConsentLedger();
            // At Welcome, Skip should do nothing.
            flow.Skip(consent);
            Assert.AreEqual(OnboardingStep.Welcome, flow.Current);
        }

        [Test]
        public void CanLeaveConsent_ReflectsDecision()
        {
            var consent = new ConsentLedger();
            Assert.IsFalse(OnboardingFlow.CanLeaveConsent(consent));
            consent.Grant(ConsentType.SceneScan);
            Assert.IsTrue(OnboardingFlow.CanLeaveConsent(consent));
            Assert.IsFalse(OnboardingFlow.CanLeaveConsent(null));
        }
    }
}
