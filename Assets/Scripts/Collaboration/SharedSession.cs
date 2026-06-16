using System;
using System.Collections.Generic;
using UnityEngine;

namespace SuperRealEstate.Collaboration
{
    /// <summary>The kind of device a participant is using. Sessions are mixed.</summary>
    public enum DeviceKind
    {
        VisionPro,
        AndroidXR,
        IosPhone,
        AndroidPhone,
        Tablet,
        Web
    }

    public enum ParticipantRole
    {
        Host,
        Agent,
        Buyer,
        Guest
    }

    /// <summary>A person in a shared session. Mirrors `session_participants`.</summary>
    [Serializable]
    public sealed class SessionParticipant
    {
        public string UserId;
        public string DisplayName;
        public ParticipantRole Role;
        public DeviceKind Device;
        public bool IsRemote;            // joined from elsewhere vs. co-located
    }

    /// <summary>
    /// Live pose of a participant, used to render avatars / pointers so everyone
    /// can see where others are looking or pointing. Headsets supply head pose;
    /// phones/tablets supply camera pose. Pointer ray is optional.
    /// </summary>
    [Serializable]
    public struct PresenceUpdate
    {
        public string UserId;
        public Pose HeadPose;
        public bool HasPointer;
        public Ray Pointer;
    }

    /// <summary>
    /// A shared walkthrough: a host, participants on any mix of devices, an
    /// optional staged layout, and a spatial anchor that co-locates everyone to
    /// the same real-world reference so virtual furniture lines up across
    /// headsets, phones, and tablets. Mirrors `sessions`.
    /// </summary>
    [Serializable]
    public sealed class SharedSession
    {
        public string Id;
        public string HostId;
        public string PropertyId;

        /// <summary>Shared spatial anchor id used to align all devices.</summary>
        public string SpatialAnchorId;

        /// <summary>The arrangement everyone sees and can edit (by id).</summary>
        public string LayoutId;

        public List<SessionParticipant> Participants = new List<SessionParticipant>();
    }
}
