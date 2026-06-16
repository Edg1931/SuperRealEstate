using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using SuperRealEstate.Staging;

namespace SuperRealEstate.Collaboration
{
    /// <summary>
    /// Co-locates devices to a common real-world reference. On a Vision Pro this
    /// maps to ARKit world/shared anchors; on Galaxy XR / Android to ARCore
    /// persistent (cloud) anchors; phones/tablets resolve the same anchor.
    /// Resolving the host's anchor is what makes a chair appear in the same spot
    /// for everyone. (Azure Spatial Anchors is retired — do not depend on it.)
    /// </summary>
    public interface ISpatialAnchorService
    {
        /// <summary>Create/persist an anchor at a pose; returns its shareable id.</summary>
        Task<string> CreateAnchorAsync(Pose pose, CancellationToken ct = default);

        /// <summary>Resolve a previously shared anchor to a local pose.</summary>
        Task<Pose> ResolveAnchorAsync(string anchorId, CancellationToken ct = default);
    }

    /// <summary>
    /// Real-time shared-session backbone: presence, roster, and live edits to
    /// the staged layout, broadcast to every participant's device. The concrete
    /// implementation rides on Supabase Realtime (or another transport);
    /// placements/anchors persist via the backend so latecomers sync up.
    /// </summary>
    public interface ISharedSessionService
    {
        SharedSession Current { get; }

        Task<SharedSession> CreateSessionAsync(string propertyId, DeviceKind device, CancellationToken ct = default);
        Task<SharedSession> JoinSessionAsync(string sessionId, DeviceKind device, ParticipantRole role, CancellationToken ct = default);
        Task LeaveSessionAsync(CancellationToken ct = default);

        /// <summary>Add/update/remove a placement; change is broadcast to all.</summary>
        Task UpsertPlacementAsync(Placement placement, CancellationToken ct = default);
        Task RemovePlacementAsync(string placementId, CancellationToken ct = default);

        /// <summary>Publish this device's head/pointer pose for avatars/cursors.</summary>
        Task PublishPresenceAsync(PresenceUpdate presence, CancellationToken ct = default);

        // --- inbound events (raised on the main thread by the implementation) ---
        event Action<SessionParticipant> ParticipantJoined;
        event Action<string> ParticipantLeft;          // userId
        event Action<Placement> PlacementChanged;
        event Action<string> PlacementRemoved;          // placementId
        event Action<PresenceUpdate> PresenceUpdated;
    }
}
