using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;
using SuperRealEstate.Onboarding;
using SuperRealEstate.Services;

namespace SuperRealEstate.ARCore
{
    /// <summary>
    /// Orchestrates first-run onboarding: drives the pure <see cref="OnboardingFlow"/>
    /// state machine through Welcome → Consent → Sign-in → Ready, recording consent
    /// via <see cref="ConsentService"/> and signing in via <see cref="RealEstateApp"/>.
    /// It owns the LOGIC + persistence and raises a <see cref="UnityEvent"/> per
    /// step so the view (procedural panels from <c>SpatialPanelBuilder</c>, or
    /// authored prefabs) just binds to <see cref="OnStep"/> / <see cref="OnStatus"/>
    /// / <see cref="OnCompleted"/>. Returning users skip onboarding (a PlayerPrefs
    /// flag), so this can sit on the boot object unconditionally.
    /// </summary>
    public sealed class OnboardingController : MonoBehaviour
    {
        private const string DonePref = "sre.onboarding.done.v1";

        [Tooltip("Records the user's consent choices.")]
        [SerializeField] private ConsentService consentService;
        [Tooltip("Used to sign in during the Sign-in step (optional).")]
        [SerializeField] private RealEstateApp app;
        [Tooltip("Run onboarding even if previously completed (for testing).")]
        [SerializeField] private bool forceShow;

        [Serializable] public sealed class StepEvent : UnityEvent<OnboardingStep> { }
        [Serializable] public sealed class StringEvent : UnityEvent<string> { }

        [Tooltip("Raised when the active onboarding step changes — bind your panels here.")]
        public StepEvent OnStep = new StepEvent();

        [Tooltip("Human-readable status / error (e.g. sign-in failures).")]
        public StringEvent OnStatus = new StringEvent();

        [Tooltip("Raised once onboarding is complete (or already was).")]
        public UnityEvent OnCompleted = new UnityEvent();

        private OnboardingFlow _flow;

        /// <summary>The current step.</summary>
        public OnboardingStep Step => _flow?.Current ?? OnboardingStep.Welcome;

        /// <summary>True once onboarding has been completed (this run or a prior one).</summary>
        public bool IsComplete => _flow != null && _flow.IsComplete;

        private void Start()
        {
            if (!forceShow && PlayerPrefs.GetInt(DonePref, 0) == 1)
            {
                OnCompleted.Invoke();
                return;
            }
            Begin();
        }

        /// <summary>(Re)start onboarding at the first step.</summary>
        public void Begin()
        {
            _flow = new OnboardingFlow();
            OnStep.Invoke(_flow.Current);
        }

        /// <summary>Advance from the current step (Welcome / Consent / Ready).</summary>
        public void Next()
        {
            if (_flow == null) Begin();
            OnboardingStep before = _flow.Current;
            OnboardingStep after = _flow.Advance(Ledger());

            if (before == OnboardingStep.Consent && after == OnboardingStep.Consent)
            {
                OnStatus.Invoke("choose camera or scanning access to continue");
                return;
            }
            EmitStep(after);
        }

        /// <summary>Skip the optional Sign-in step.</summary>
        public void SkipSignIn() { if (_flow != null) EmitStep(_flow.Skip(Ledger())); }

        /// <summary>Grant a consent (wire to the consent screen's allow buttons).</summary>
        public void Grant(ConsentType type) => consentService?.Grant(type);

        /// <summary>Deny a consent (wire to the consent screen's deny buttons).</summary>
        public void Deny(ConsentType type) => consentService?.Deny(type);

        /// <summary>Convenience wrappers for the common capture consents.</summary>
        public void GrantCamera() => Grant(ConsentType.Camera);
        public void GrantSceneScan() => Grant(ConsentType.SceneScan);
        public void GrantMicrophone() => Grant(ConsentType.Microphone);

        /// <summary>
        /// Sign in during the Sign-in step; on success, advance. Fire-and-forget
        /// wrapper for a UI button (errors surface via <see cref="OnStatus"/>).
        /// </summary>
        public void SignIn(string email, string password) => _ = SignInAsync(email, password);

        public async Task SignInAsync(string email, string password)
        {
            if (app == null) { OnStatus.Invoke("sign-in isn't available"); return; }
            try
            {
                AuthSession session = await app.SignInWithPasswordAsync(email, password);
                if (session.IsValid)
                {
                    OnStatus.Invoke("signed in");
                    if (_flow != null && _flow.Current == OnboardingStep.SignIn) EmitStep(_flow.Advance(Ledger()));
                }
                else
                {
                    OnStatus.Invoke("sign-in failed");
                }
            }
            catch (Exception e)
            {
                OnStatus.Invoke("sign-in failed");
                Debug.LogWarning($"[OnboardingController] sign-in failed: {e.Message}");
            }
        }

        private void EmitStep(OnboardingStep step)
        {
            OnStep.Invoke(step);
            if (step == OnboardingStep.Done)
            {
                PlayerPrefs.SetInt(DonePref, 1);
                PlayerPrefs.Save();
                OnCompleted.Invoke();
            }
        }

        private ConsentLedger Ledger() => consentService != null ? consentService.Ledger : new ConsentLedger();
    }
}
