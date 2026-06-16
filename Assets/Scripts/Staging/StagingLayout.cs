using System;
using System.Collections.Generic;
using UnityEngine;

namespace SuperRealEstate.Staging
{
    /// <summary>
    /// A furniture asset positioned within a layout. Position is the footprint
    /// center on the floor; <see cref="YawDegrees"/> is rotation about the
    /// vertical axis. Mirrors a `staging_placements` row. <see cref="AnchorId"/>
    /// ties the placement to a shared spatial anchor so every device in a
    /// session renders it in the same real-world spot.
    /// </summary>
    [Serializable]
    public sealed class Placement
    {
        public string Id;
        public string FurnitureAssetId;
        public Vector3 Position;
        public float YawDegrees;
        public float Scale = 1f;
        public string AnchorId;

        public Placement() { }

        public Placement(string furnitureAssetId, Vector3 position, float yawDegrees = 0f, float scale = 1f)
        {
            FurnitureAssetId = furnitureAssetId;
            Position = position;
            YawDegrees = yawDegrees;
            Scale = scale;
        }
    }

    /// <summary>
    /// A named arrangement of furniture for a room. Mirrors `staging_layouts`
    /// plus its placements. Shared across all devices in a session.
    /// </summary>
    [Serializable]
    public sealed class StagingLayout
    {
        public string Id;
        public string RoomId;
        public string Name = "Layout";
        public List<Placement> Placements = new List<Placement>();
    }
}
