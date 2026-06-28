using System;
using UnityEngine;
using UnityEngine.Events;
using SuperRealEstate.Onboarding;

namespace SuperRealEstate.ARCore
{
    /// <summary>
    /// AR-layer wrapper around the pure <see cref="ConsentLedger"/>: persists the
    /// user's app-level consents (camera, scene scan, location, microphone, cloud,
    /// analytics) to <see cref="PlayerPrefs"/> and answers "is this capture action
    /// allowed?" for the rest of the app. The onboarding consent screen calls
    /// <see cref="Grant"/>/<see cref="Deny"/>; capture paths check
    /// <see cref="IsAllowed"/> before touching a sensor — so a camera frame, scan,
    /// or audio is never used without the user agreeing first (store-review +
    /// privacy requirement).
    /// </summary>
    public sealed class ConsentService : MonoBehaviour
    {
        private const string PrefKey = "sre.consent.v1";

        [Serializable] public sealed class ConsentEvent : UnityEvent<ConsentType, bool> { }

        [Tooltip("Raised when a consent is granted (true) or denied (false).")]
        public ConsentEvent OnConsentChanged = new ConsentEvent();

        private ConsentLedger _ledger;

        /// <summary>The underlying ledger (loaded from prefs on Awake).</summary>
        public ConsentLedger Ledger => _ledger ??= Load();

        private void Awake() { _ledger = Load(); }

        /// <summary>Grant a consent and persist.</summary>
        public void Grant(ConsentType type)
        {
            Ledger.Grant(type);
            Save();
            OnConsentChanged.Invoke(type, true);
        }

        /// <summary>Deny/revoke a consent and persist.</summary>
        public void Deny(ConsentType type)
        {
            Ledger.Deny(type);
            Save();
            OnConsentChanged.Invoke(type, false);
        }

        /// <summary>True only when the user has explicitly granted this consent.</summary>
        public bool IsGranted(ConsentType type) => Ledger.IsGranted(type);

        /// <summary>True when every consent a capture action needs has been granted.</summary>
        public bool IsAllowed(CaptureAction action) => ConsentGate.IsAllowed(action, Ledger);

        /// <summary>The first consent an action still needs, or null if all granted.</summary>
        public ConsentType? FirstMissing(CaptureAction action) => ConsentGate.FirstMissing(action, Ledger);

        private static ConsentLedger Load()
            => ConsentLedger.Deserialize(PlayerPrefs.GetString(PrefKey, string.Empty));

        private void Save()
        {
            PlayerPrefs.SetString(PrefKey, Ledger.Serialize());
            PlayerPrefs.Save();
        }
    }
}
