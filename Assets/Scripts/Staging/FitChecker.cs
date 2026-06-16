using System;
using System.Collections.Generic;
using UnityEngine;

namespace SuperRealEstate.Staging
{
    /// <summary>Result of checking whether furniture fits in a room.</summary>
    public readonly struct FitResult
    {
        public readonly bool Fits;
        public readonly bool InsideRoom;
        public readonly float MinWallGapM;   // smallest distance from footprint to any wall
        public readonly string Reason;

        public FitResult(bool fits, bool insideRoom, float minWallGapM, string reason)
        {
            Fits = fits;
            InsideRoom = insideRoom;
            MinWallGapM = minWallGapM;
            Reason = reason;
        }
    }

    /// <summary>
    /// Pure geometry that answers "will my furniture fit?" — through a doorway
    /// and within the room footprint with walkway clearance. No AR/scene
    /// dependency, so it is fully unit-testable. All math is in the XZ floor
    /// plane and in meters.
    /// </summary>
    public static class FitChecker
    {
        /// <summary>
        /// Conservative doorway pass-through: the item's two smallest dimensions
        /// must fit within the door opening (no diagonal tilting credited).
        /// </summary>
        public static bool FitsThroughDoorway(Vector3 itemSize, float doorWidth, float doorHeight)
        {
            float[] dims = { Mathf.Abs(itemSize.x), Mathf.Abs(itemSize.y), Mathf.Abs(itemSize.z) };
            Array.Sort(dims); // ascending: [smallest, middle, largest]

            float openMin = Mathf.Min(doorWidth, doorHeight);
            float openMax = Mathf.Max(doorWidth, doorHeight);

            // Present the smallest face to the opening.
            return dims[0] <= openMin && dims[1] <= openMax;
        }

        /// <summary>
        /// The four world-space footprint corners of a placement, accounting for
        /// position, yaw, and scale. Y is taken from the placement position.
        /// </summary>
        public static Vector3[] FootprintCorners(Placement placement, FurnitureAsset asset)
        {
            if (placement == null) throw new ArgumentNullException(nameof(placement));
            if (asset == null) throw new ArgumentNullException(nameof(asset));

            float hw = Mathf.Abs(asset.Size.x) * placement.Scale * 0.5f;
            float hd = Mathf.Abs(asset.Size.z) * placement.Scale * 0.5f;

            Quaternion rot = Quaternion.Euler(0f, placement.YawDegrees, 0f);
            Vector3 p = placement.Position;

            var corners = new Vector3[4];
            corners[0] = p + rot * new Vector3(-hw, 0f, -hd);
            corners[1] = p + rot * new Vector3(hw, 0f, -hd);
            corners[2] = p + rot * new Vector3(hw, 0f, hd);
            corners[3] = p + rot * new Vector3(-hw, 0f, hd);
            return corners;
        }

        /// <summary>
        /// Does the placed furniture's footprint sit inside the room outline
        /// with at least <paramref name="wallClearanceM"/> from every wall?
        /// Ignores furniture-to-furniture collisions (handled elsewhere).
        /// </summary>
        public static FitResult FootprintFitsInRoom(
            IReadOnlyList<Vector3> roomOutline,
            Placement placement,
            FurnitureAsset asset,
            float wallClearanceM = 0f)
        {
            if (roomOutline == null || roomOutline.Count < 3)
                return new FitResult(false, false, 0f, "Room outline is invalid.");

            Vector3[] corners = FootprintCorners(placement, asset);

            bool allInside = true;
            float minGap = float.MaxValue;

            foreach (Vector3 c in corners)
            {
                if (!PointInPolygonXZ(roomOutline, c)) allInside = false;
                float gap = DistanceToOutlineXZ(roomOutline, c);
                if (gap < minGap) minGap = gap;
            }

            if (!allInside)
                return new FitResult(false, false, minGap, "Furniture extends past a wall.");

            if (minGap < wallClearanceM)
                return new FitResult(false, true, minGap,
                    $"Only {minGap:0.00} m to the nearest wall (need {wallClearanceM:0.00} m).");

            return new FitResult(true, true, minGap, "Fits.");
        }

        // --- geometry helpers (XZ plane) ---

        /// <summary>Ray-casting point-in-polygon test on the XZ plane.</summary>
        public static bool PointInPolygonXZ(IReadOnlyList<Vector3> polygon, Vector3 point)
        {
            bool inside = false;
            int n = polygon.Count;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                float xi = polygon[i].x, zi = polygon[i].z;
                float xj = polygon[j].x, zj = polygon[j].z;

                bool crosses = ((zi > point.z) != (zj > point.z)) &&
                               (point.x < (xj - xi) * (point.z - zi) / (zj - zi) + xi);
                if (crosses) inside = !inside;
            }
            return inside;
        }

        /// <summary>Shortest distance from a point to the polygon outline (XZ).</summary>
        public static float DistanceToOutlineXZ(IReadOnlyList<Vector3> polygon, Vector3 point)
        {
            float min = float.MaxValue;
            int n = polygon.Count;
            for (int i = 0; i < n; i++)
            {
                float d = DistancePointToSegmentXZ(point, polygon[i], polygon[(i + 1) % n]);
                if (d < min) min = d;
            }
            return min;
        }

        private static float DistancePointToSegmentXZ(Vector3 p, Vector3 a, Vector3 b)
        {
            Vector2 pp = new Vector2(p.x, p.z);
            Vector2 aa = new Vector2(a.x, a.z);
            Vector2 bb = new Vector2(b.x, b.z);

            Vector2 ab = bb - aa;
            float lenSq = ab.sqrMagnitude;
            if (lenSq < 1e-8f) return Vector2.Distance(pp, aa);

            float t = Mathf.Clamp01(Vector2.Dot(pp - aa, ab) / lenSq);
            Vector2 proj = aa + t * ab;
            return Vector2.Distance(pp, proj);
        }
    }
}
