using System;
using System.Collections.Generic;
using UnityEngine;

namespace SuperRealEstate.RoomMeasure
{
    /// <summary>
    /// A captured room described by its floor outline and ceiling height.
    /// This is a plain data container produced by the AR layer (from detected
    /// planes / scene mesh) and consumed by <see cref="MeasurementService"/>.
    /// It deliberately has no AR-Foundation dependency so the measurement math
    /// can be unit-tested headlessly.
    /// </summary>
    [Serializable]
    public sealed class RoomGeometry
    {
        /// <summary>
        /// Floor outline vertices in world space, ordered around the perimeter
        /// (clockwise or counter-clockwise — area is returned as an absolute
        /// value). Y is expected to sit on the floor plane.
        /// </summary>
        public readonly IReadOnlyList<Vector3> FloorOutline;

        /// <summary>Floor-to-ceiling height in meters.</summary>
        public readonly float CeilingHeight;

        public RoomGeometry(IReadOnlyList<Vector3> floorOutline, float ceilingHeight)
        {
            if (floorOutline == null) throw new ArgumentNullException(nameof(floorOutline));
            if (ceilingHeight < 0f) throw new ArgumentOutOfRangeException(nameof(ceilingHeight));

            FloorOutline = floorOutline;
            CeilingHeight = ceilingHeight;
        }

        public bool IsValid => FloorOutline != null && FloorOutline.Count >= 3;
    }
}
