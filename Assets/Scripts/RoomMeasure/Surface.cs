using System;
using UnityEngine;

namespace SuperRealEstate.RoomMeasure
{
    public enum SurfaceKind { Floor, Wall, Ceiling }

    /// <summary>
    /// The unifying primitive for the walk-in pipeline: a single detected
    /// surface that everything attaches to —
    ///   • measurement   (Area),
    ///   • current finish recognized by AI (RecognizedFinishId),
    ///   • a proposed new finish for renovation (ProposedFinishId / cost),
    ///   • staging anchoring (a floor surface is where furniture drops).
    /// Keeping floor/walls/ceiling as one addressable type is what lets
    /// measure → recognize → re-finish → stage operate on the same object
    /// instead of three disconnected features.
    /// </summary>
    [Serializable]
    public sealed class Surface
    {
        public string Id;
        public SurfaceKind Kind;
        public float AreaSqM;

        /// <summary>Center and outward normal in world space (for re-finishing + labels).</summary>
        public Vector3 Center;
        public Vector3 Normal;

        /// <summary>Catalog/material id of the AI-recognized current finish (if identified).</summary>
        public string RecognizedFinishId;

        /// <summary>Catalog/material id of a proposed renovation finish (if the user changed it).</summary>
        public string ProposedFinishId;

        public float AreaSqFt => AreaSqM / 0.092903f;
    }
}
