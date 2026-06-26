using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace SuperRealEstate.Collaboration
{
    /// <summary>
    /// Establishes a shared real-world origin for a co-located session. The host
    /// publishes one anchor (a <see cref="SharedAnchorPayload"/> stored on the
    /// session); each participant resolves it into an <see cref="AnchorFrame"/>
    /// and renders all shared content anchor-relative. Built on the platform's
    /// <see cref="ISpatialAnchorService"/> (ARKit / ARCore Cloud / OpenXR) — the
    /// portable path is ARCore Cloud Anchors, which resolve on iOS and Android
    /// (incl. Android XR). See docs/Co-Location.md.
    /// </summary>
    public sealed class SessionCoLocator
    {
        private readonly ISpatialAnchorService _anchors;

        public SessionCoLocator(ISpatialAnchorService anchors)
        {
            _anchors = anchors ?? throw new ArgumentNullException(nameof(anchors));
        }

        /// <summary>Host: anchor the session origin and produce a shareable payload.</summary>
        public async Task<SharedAnchorPayload> PublishOriginAsync(
            string sessionId, Pose origin, AnchorProvider provider, CancellationToken ct = default)
        {
            string anchorId = await _anchors.CreateAnchorAsync(origin, ct);
            return new SharedAnchorPayload
            {
                SessionId = sessionId,
                Provider = provider,
                AnchorId = anchorId,
            };
        }

        /// <summary>Participant: resolve the shared anchor into a local frame.</summary>
        public async Task<AnchorFrame> ResolveAsync(SharedAnchorPayload payload, CancellationToken ct = default)
        {
            if (payload == null) throw new ArgumentNullException(nameof(payload));
            try
            {
                Pose pose = await _anchors.ResolveAnchorAsync(payload.AnchorId, ct);
                return new AnchorFrame(pose);
            }
            catch when (payload.HasFallback)
            {
                Pose pose = await _anchors.ResolveAnchorAsync(payload.AltAnchorId, ct);
                return new AnchorFrame(pose);
            }
        }
    }
}
