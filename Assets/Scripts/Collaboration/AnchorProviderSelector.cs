using System;
using System.Collections.Generic;

namespace SuperRealEstate.Collaboration
{
    /// <summary>One concrete way a given device can try to resolve a shared anchor.</summary>
    public readonly struct AnchorCandidate
    {
        public readonly AnchorProvider Provider;
        public readonly string AnchorId;

        public AnchorCandidate(AnchorProvider provider, string anchorId)
        {
            Provider = provider;
            AnchorId = anchorId;
        }
    }

    /// <summary>
    /// Decides, for a specific device, which advertised anchor provider(s) it can
    /// actually resolve — the cross-platform "make everyone agree on the same
    /// real-world origin" rule. ARKit and ARCore anchors don't interoperate, so a
    /// host advertises a primary (and optional fallback) provider in its
    /// <see cref="SharedAnchorPayload"/>, and each joining device picks the first
    /// one it supports. Pure + unit-tested; no AR/Unity dependency. See
    /// docs/Co-Location.md.
    ///
    /// Resolvability matrix (conservative — only paths with a real cross-device SDK):
    ///  • <see cref="AnchorProvider.ArcoreCloud"/>  — Android, Android XR, and iOS
    ///    (ARCore Cloud Anchors ship an iOS resolver). Not Vision Pro, not Web.
    ///  • <see cref="AnchorProvider.ArkitWorld"/>   — Apple only: Vision Pro + iOS
    ///    devices (incl. iPads).
    ///  • <see cref="AnchorProvider.OpenXrPersistent"/> — OpenXR runtimes: Android XR.
    ///  • <see cref="AnchorProvider.NianticVps"/>   — visual positioning fallback on
    ///    Android / iOS / Android XR (large-area, no shared local tracking needed).
    /// </summary>
    public static class AnchorProviderSelector
    {
        /// <summary>Can <paramref name="device"/> resolve an anchor from <paramref name="provider"/>?</summary>
        public static bool CanResolve(DeviceKind device, AnchorProvider provider)
        {
            switch (provider)
            {
                case AnchorProvider.ArcoreCloud:
                    return device == DeviceKind.AndroidXR
                        || device == DeviceKind.AndroidPhone
                        || device == DeviceKind.IosPhone
                        || device == DeviceKind.Tablet;

                case AnchorProvider.ArkitWorld:
                    return device == DeviceKind.VisionPro
                        || device == DeviceKind.IosPhone
                        || device == DeviceKind.Tablet;

                case AnchorProvider.OpenXrPersistent:
                    return device == DeviceKind.AndroidXR;

                case AnchorProvider.NianticVps:
                    return device == DeviceKind.AndroidXR
                        || device == DeviceKind.AndroidPhone
                        || device == DeviceKind.IosPhone
                        || device == DeviceKind.Tablet;

                default:
                    return false;
            }
        }

        /// <summary>
        /// The ordered list of (provider, anchorId) this device should attempt for
        /// the given payload — primary first, then the fallback — keeping only the
        /// providers this device can resolve and entries that carry an anchor id.
        /// Empty means this device can't co-locate from this payload (e.g. a Vision
        /// Pro handed only an ARCore Cloud anchor).
        /// </summary>
        public static IReadOnlyList<AnchorCandidate> Candidates(DeviceKind device, SharedAnchorPayload payload)
        {
            var result = new List<AnchorCandidate>(2);
            if (payload == null) return result;

            Add(result, device, payload.Provider, payload.AnchorId);

            if (payload.HasFallback && TryParseProvider(payload.AltProvider, out AnchorProvider alt))
                Add(result, device, alt, payload.AltAnchorId);

            return result;
        }

        /// <summary>True when this device can resolve at least one candidate.</summary>
        public static bool CanCoLocate(DeviceKind device, SharedAnchorPayload payload)
            => Candidates(device, payload).Count > 0;

        private static void Add(List<AnchorCandidate> list, DeviceKind device, AnchorProvider provider, string anchorId)
        {
            if (string.IsNullOrEmpty(anchorId)) return;
            if (!CanResolve(device, provider)) return;
            // Avoid duplicate (provider, id) pairs.
            foreach (AnchorCandidate c in list)
                if (c.Provider == provider && c.AnchorId == anchorId) return;
            list.Add(new AnchorCandidate(provider, anchorId));
        }

        private static bool TryParseProvider(string name, out AnchorProvider provider)
            => Enum.TryParse(name, ignoreCase: true, out provider);
    }
}
