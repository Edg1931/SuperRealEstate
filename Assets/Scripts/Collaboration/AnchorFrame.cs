using UnityEngine;
using SuperRealEstate.Staging;

namespace SuperRealEstate.Collaboration
{
    /// <summary>
    /// A resolved shared anchor expressed as a local-world pose. Content is
    /// authored/stored **relative to the anchor**; each device resolves the same
    /// anchor to its own local pose and converts to world here. Because everyone
    /// references the same physical anchor, the furniture / renovated walls line
    /// up across a Vision Pro, a Galaxy XR, and a phone. Pure math; unit-tested.
    /// </summary>
    public readonly struct AnchorFrame
    {
        public readonly Pose AnchorPose; // the shared anchor, in this device's world

        public AnchorFrame(Pose anchorPose) { AnchorPose = anchorPose; }

        public Vector3 ToWorld(Vector3 anchorLocal)
            => AnchorPose.position + (AnchorPose.rotation * anchorLocal);

        public Vector3 ToAnchorLocal(Vector3 world)
            => Quaternion.Inverse(AnchorPose.rotation) * (world - AnchorPose.position);

        public Pose ToWorldPose(Pose anchorLocal)
            => new Pose(ToWorld(anchorLocal.position), AnchorPose.rotation * anchorLocal.rotation);

        /// <summary>
        /// Convert an anchor-relative placement (as shared in the session) into a
        /// world-space placement for this device. Item scale is unchanged; yaw
        /// composes with the anchor's yaw.
        /// </summary>
        public Placement ToWorldPlacement(Placement anchorLocal)
        {
            float anchorYaw = AnchorPose.rotation.eulerAngles.y;
            return new Placement
            {
                Id = anchorLocal.Id,
                FurnitureAssetId = anchorLocal.FurnitureAssetId,
                CatalogItemId = anchorLocal.CatalogItemId,
                Position = ToWorld(anchorLocal.Position),
                YawDegrees = anchorLocal.YawDegrees + anchorYaw,
                Scale = anchorLocal.Scale,
                AnchorId = anchorLocal.AnchorId,
            };
        }
    }
}
