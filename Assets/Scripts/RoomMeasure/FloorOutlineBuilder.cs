using System.Collections.Generic;
using UnityEngine;

namespace SuperRealEstate.RoomMeasure
{
    /// <summary>
    /// Bridges AR-detected plane data into a <see cref="RoomGeometry"/>.
    ///
    /// AR Foundation reports a plane's boundary as 2D points in the plane's
    /// local space (on its X/Z plane). This converts those points to world
    /// space using the plane's <see cref="Pose"/>, so the rest of the pipeline
    /// (<see cref="MeasurementService"/>) stays AR-agnostic and testable.
    /// </summary>
    public static class FloorOutlineBuilder
    {
        /// <summary>
        /// Convert plane-local boundary points (X = local x, Y = local z) into
        /// world-space outline vertices using the plane's pose.
        /// </summary>
        public static List<Vector3> FromPlaneBoundary(Pose planePose, IReadOnlyList<Vector2> boundary)
        {
            var outline = new List<Vector3>(boundary?.Count ?? 0);
            if (boundary == null) return outline;

            for (int i = 0; i < boundary.Count; i++)
            {
                Vector2 p = boundary[i];
                Vector3 local = new Vector3(p.x, 0f, p.y);
                outline.Add(planePose.position + (planePose.rotation * local));
            }

            return outline;
        }

        /// <summary>
        /// Build a room from a detected floor plane boundary and a ceiling
        /// height (e.g. measured from floor plane to a detected ceiling plane).
        /// </summary>
        public static RoomGeometry BuildRoom(Pose floorPose, IReadOnlyList<Vector2> floorBoundary, float ceilingHeight)
        {
            return new RoomGeometry(FromPlaneBoundary(floorPose, floorBoundary), ceilingHeight);
        }
    }
}
