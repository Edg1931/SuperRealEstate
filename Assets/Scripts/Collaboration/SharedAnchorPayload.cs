using System;

namespace SuperRealEstate.Collaboration
{
    /// <summary>Which backend persisted/shared the anchor.</summary>
    public enum AnchorProvider
    {
        ArkitWorld,        // Apple (visionOS / iOS) shared world anchor
        ArcoreCloud,       // Google Cloud Anchors (Android / Android XR / cross-platform)
        OpenXrPersistent,  // OpenXR spatial anchor persistence
        NianticVps         // visual positioning, large-area fallback
    }

    /// <summary>
    /// A serializable token that lets every device in a session resolve the SAME
    /// real-world origin, regardless of platform. Published by the host once,
    /// then resolved by each participant — this is what makes the agent's Vision
    /// Pro, the buyer's Galaxy XR, and a spouse's phone agree on where the
    /// virtual furniture / renovated walls sit.
    ///
    /// Cross-platform reality: ARKit and ARCore anchors don't interoperate
    /// directly. The portable path is **ARCore Cloud Anchors**, which resolve on
    /// iOS *and* Android (and Android XR via the same stack); OpenXR persistent
    /// anchors cover Android XR ↔ Android XR. The host advertises one or more
    /// providers and each device picks the one it can resolve.
    /// </summary>
    [Serializable]
    public sealed class SharedAnchorPayload
    {
        public string SessionId;
        public AnchorProvider Provider;
        public string AnchorId;       // provider-specific resolve handle
        public string AltProvider;    // optional fallback provider name
        public string AltAnchorId;    // optional fallback handle
        public long CreatedAtUnixMs;  // for staleness checks (stamp outside; clock-free core)

        public bool HasFallback => !string.IsNullOrEmpty(AltAnchorId);
    }
}
