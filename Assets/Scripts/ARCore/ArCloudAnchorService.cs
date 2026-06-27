using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using SuperRealEstate.Collaboration;

namespace SuperRealEstate.ARCore
{
    /// <summary>
    /// AR Foundation implementation of <see cref="ISpatialAnchorService"/>, the
    /// hosting/resolving seam behind <c>SessionCoLocator</c>. The portable
    /// cross-platform path is **ARCore Cloud Anchors** (host on one device,
    /// resolve on iOS/Android/Android XR), keeping a Vision Pro, a Galaxy XR, and
    /// a phone on the same origin — see docs/Co-Location.md.
    ///
    /// What this does today (AF 6.0.x):
    ///  • Creates a **real tracked anchor** via
    ///    <see cref="ARAnchorManager.TryAddAnchorAsync"/> when a manager is
    ///    supplied, so the origin stays locked to the world as tracking refines
    ///    (not a frozen pose). Resolve returns the live anchor pose.
    ///  • Keeps a local id→pose/anchor map so same-device resolve and the
    ///    co-location flow (and EditMode tests) run end to end without a manager.
    ///
    /// What still needs an SDK (the cross-device "cloud" step): hosting a Cloud
    /// Anchor and getting back a globally-shareable id, then resolving that id on
    /// a *different* device. ARCore Cloud Anchors live in Google's "ARCore
    /// Extensions for AR Foundation" package (not in this manifest yet); Android
    /// XR ↔ Android XR can alternatively use OpenXR persistent anchors. Those plug
    /// in at <see cref="HostCloudAsync"/> / <see cref="ResolveCloudAsync"/>.
    /// </summary>
    public sealed class ArCloudAnchorService : ISpatialAnchorService
    {
        private readonly ARAnchorManager _anchorManager; // real anchoring backend (optional)
        private readonly Dictionary<string, ARAnchor> _anchors = new Dictionary<string, ARAnchor>();
        private readonly Dictionary<string, Pose> _poses = new Dictionary<string, Pose>();

        public ArCloudAnchorService(ARAnchorManager anchorManager = null)
        {
            _anchorManager = anchorManager;
        }

        public async Task<string> CreateAnchorAsync(Pose pose, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();

            string id = Guid.NewGuid().ToString("N");

            // Create a real tracked anchor when a manager is available; fall back
            // to a pose handle otherwise (tests / no-AR contexts).
            ARAnchor anchor = await TryCreateTrackedAnchorAsync(pose);
            if (anchor != null)
                _anchors[id] = anchor;

            _poses[id] = pose;

            // Cross-device hand-off needs the cloud id (see HostCloudAsync); until
            // that SDK lands the local id co-locates same-device + shared-tracking.
            return id;
        }

        public Task<Pose> ResolveAnchorAsync(string anchorId, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            if (string.IsNullOrEmpty(anchorId)) throw new ArgumentNullException(nameof(anchorId));

            // Prefer the live tracked-anchor pose (updates as tracking refines).
            if (_anchors.TryGetValue(anchorId, out ARAnchor anchor) && anchor != null)
            {
                Transform t = anchor.transform;
                return Task.FromResult(new Pose(t.position, t.rotation));
            }

            if (_poses.TryGetValue(anchorId, out Pose pose))
                return Task.FromResult(pose);

            // TODO: resolve a Cloud Anchor by its shareable id on this device.
            throw new InvalidOperationException(
                $"Cloud anchor '{anchorId}' not resolvable on this device yet (needs the Cloud Anchors SDK).");
        }

        /// <summary>
        /// Create a real AR Foundation tracked anchor at <paramref name="pose"/>.
        /// Returns null when no manager is wired or the platform/subsystem can't
        /// add it; callers fall back to the local pose map. Isolated here because
        /// it's the one AR-Foundation-version-specific call (AF 6.0.x async
        /// anchor API).
        /// </summary>
        private async Task<ARAnchor> TryCreateTrackedAnchorAsync(Pose pose)
        {
            if (_anchorManager == null) return null;

            try
            {
                Result<ARAnchor> result = await _anchorManager.TryAddAnchorAsync(pose);
                return result.status.IsSuccess() ? result.value : null;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ArCloudAnchorService] TryAddAnchorAsync failed: {e.Message}");
                return null;
            }
        }

        // --- Cross-device cloud seam (SDK integration point) ------------------

        /// <summary>
        /// Host a globally-shareable Cloud Anchor from a local anchor and return
        /// its cloud id. Implement with ARCore Extensions
        /// (<c>HostCloudAnchorAsync</c>) or OpenXR persistent-anchor export when
        /// the package is added. Until then this is not wired.
        /// </summary>
        public Task<string> HostCloudAsync(string localAnchorId, CancellationToken ct = default)
            => throw new NotSupportedException("Cloud Anchor hosting needs the ARCore Extensions / OpenXR persistence SDK.");

        /// <summary>Resolve a Cloud Anchor by its shareable cloud id on this device.</summary>
        public Task<Pose> ResolveCloudAsync(string cloudAnchorId, CancellationToken ct = default)
            => throw new NotSupportedException("Cloud Anchor resolve needs the ARCore Extensions / OpenXR persistence SDK.");
    }
}
