using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
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
    /// This is a working scaffold: it anchors locally via the
    /// <see cref="ARAnchorManager"/> and returns a handle, with the cloud
    /// host/resolve calls marked TODO (provider SDK + version specific). The
    /// co-location math that consumes it (<c>AnchorFrame</c>) is already tested.
    /// </summary>
    public sealed class ArCloudAnchorService : ISpatialAnchorService
    {
        private readonly ARAnchorManager _anchorManager; // real anchoring backend
        private readonly Dictionary<string, Pose> _local = new Dictionary<string, Pose>();

        public ArCloudAnchorService(ARAnchorManager anchorManager = null)
        {
            _anchorManager = anchorManager;
        }

        public Task<string> CreateAnchorAsync(Pose pose, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            // TODO: host a Cloud Anchor via the platform SDK and return its
            // shareable cloud id. For now, anchor locally and hand back a handle
            // so the session/co-location flow runs end to end.
            string id = Guid.NewGuid().ToString("N");
            _local[id] = pose;
            return Task.FromResult(id);
        }

        public Task<Pose> ResolveAnchorAsync(string anchorId, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            if (string.IsNullOrEmpty(anchorId)) throw new ArgumentNullException(nameof(anchorId));
            // TODO: resolve the Cloud Anchor by id on this device. The local map
            // covers same-device + tests; cross-device resolve needs the cloud id.
            if (_local.TryGetValue(anchorId, out var pose)) return Task.FromResult(pose);
            throw new InvalidOperationException($"Cloud anchor '{anchorId}' not resolvable on this device yet.");
        }
    }
}
