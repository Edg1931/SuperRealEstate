using System;
using System.Collections.Generic;
using UnityEngine;
using SuperRealEstate.Platform;

namespace SuperRealEstate.ARCore
{
    /// <summary>
    /// Drop this on a root object in the AR scene. It resolves the device's
    /// <see cref="XrCapabilities"/> and switches feature GameObjects on/off via
    /// <see cref="FeatureAvailability"/> — so the SAME scene runs on Galaxy XR,
    /// Vision Pro, and a phone, lighting up only what each device supports
    /// (e.g. wall-removal portals stay off on optical glasses automatically).
    /// </summary>
    public sealed class XrSessionBootstrap : MonoBehaviour
    {
        [Serializable]
        public sealed class FeatureBinding
        {
            public AppFeature Feature;
            public GameObject[] Objects;
        }

        [Tooltip("Auto-detect from the runtime platform, or set explicitly (headsets are ambiguous at runtime).")]
        [SerializeField] private bool autoDetect = true;
        [SerializeField] private XrPlatform platform = XrPlatform.AndroidXrHeadset;

        [Tooltip("Objects to enable only when their feature is supported on this device.")]
        [SerializeField] private List<FeatureBinding> featureBindings = new List<FeatureBinding>();

        public XrCapabilities Capabilities { get; private set; }

        private void Awake()
        {
            XrPlatform resolved = autoDetect ? Detect(platform) : platform;
            Capabilities = XrCapabilityProfiles.For(resolved);
            ApplyFeatureGates();
        }

        private void ApplyFeatureGates()
        {
            foreach (var binding in featureBindings)
            {
                bool available = FeatureAvailability.IsAvailable(Capabilities, binding.Feature);
                if (binding.Objects == null) continue;
                foreach (var go in binding.Objects)
                    if (go != null) go.SetActive(available);
            }
        }

        /// <summary>
        /// Best-effort runtime detection. Phones/visionOS are reliable; the two
        /// Android XR form factors (headset vs. optical glasses) aren't
        /// distinguishable here — set <see cref="platform"/> explicitly for those.
        /// </summary>
        private static XrPlatform Detect(XrPlatform fallback)
        {
            switch (Application.platform)
            {
                case RuntimePlatform.VisionOS: return XrPlatform.VisionOS;
                case RuntimePlatform.IPhonePlayer: return XrPlatform.IosPhone;
                case RuntimePlatform.Android:
                    // Could be a phone or an Android XR device — keep the
                    // explicit choice for XR builds.
                    return fallback == XrPlatform.AndroidXrGlasses ? XrPlatform.AndroidXrGlasses
                         : fallback == XrPlatform.AndroidXrHeadset ? XrPlatform.AndroidXrHeadset
                         : XrPlatform.AndroidPhone;
                default: return fallback;
            }
        }
    }
}
