using System;
using System.Collections.Generic;
using UnityEngine;

namespace SuperRealEstate.RoomMeasure
{
    /// <summary>
    /// Pure geometry math that turns a captured <see cref="RoomGeometry"/> into
    /// floor area, perimeter, wall area, and volume. No AR or scene
    /// dependencies, so it is fully unit-testable.
    /// </summary>
    public static class MeasurementService
    {
        /// <summary>
        /// Computes derived measurements for a room. The floor outline is
        /// projected onto the horizontal (XZ) plane; floor area uses the
        /// shoelace formula and is always returned as a positive value
        /// regardless of winding order.
        /// </summary>
        public static RoomMeasurements Compute(RoomGeometry room)
        {
            if (room == null) throw new ArgumentNullException(nameof(room));
            if (!room.IsValid)
                throw new ArgumentException("Room needs at least 3 floor outline points.", nameof(room));

            float floorArea = FloorArea(room.FloorOutline);
            float perimeter = Perimeter(room.FloorOutline);
            float height = room.CeilingHeight;
            float wallArea = perimeter * height;
            float volume = floorArea * height;

            return new RoomMeasurements(floorArea, wallArea, perimeter, height, volume);
        }

        /// <summary>Shoelace area of the outline projected onto the XZ plane (m²).</summary>
        public static float FloorArea(IReadOnlyList<Vector3> outline)
        {
            if (outline == null) throw new ArgumentNullException(nameof(outline));
            int n = outline.Count;
            if (n < 3) return 0f;

            float sum = 0f;
            for (int i = 0; i < n; i++)
            {
                Vector3 a = outline[i];
                Vector3 b = outline[(i + 1) % n];
                sum += (a.x * b.z) - (b.x * a.z);
            }

            return Mathf.Abs(sum) * 0.5f;
        }

        /// <summary>Perimeter length of the closed outline in the XZ plane (m).</summary>
        public static float Perimeter(IReadOnlyList<Vector3> outline)
        {
            if (outline == null) throw new ArgumentNullException(nameof(outline));
            int n = outline.Count;
            if (n < 2) return 0f;

            float total = 0f;
            for (int i = 0; i < n; i++)
            {
                Vector3 a = outline[i];
                Vector3 b = outline[(i + 1) % n];
                float dx = b.x - a.x;
                float dz = b.z - a.z;
                total += Mathf.Sqrt((dx * dx) + (dz * dz));
            }

            return total;
        }
    }
}
