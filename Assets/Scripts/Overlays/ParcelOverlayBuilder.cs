using System.Collections.Generic;
using UnityEngine;
using SuperRealEstate.Construction;

namespace SuperRealEstate.Overlays
{
    /// <summary>
    /// Traces a <see cref="Parcel"/> boundary (plan-view meters in X/Z) into a
    /// closed, ground-anchored <see cref="OverlayPolyline"/> for the "show the
    /// property lines" overlay. Pure + testable; the AR layer renders the result.
    ///
    /// ADVISORY: parcel geometry comes from public GIS records and is approximate,
    /// not a survey. The UI should frame the overlay accordingly.
    /// </summary>
    public static class ParcelOverlayBuilder
    {
        /// <summary>Stroke width for the boundary line, in meters at 1 m depth.</summary>
        public const float BoundaryWidthM = 0.02f;

        /// <summary>
        /// Builds the closed boundary polyline at height <paramref name="floorY"/>.
        /// Each plan vertex (x, y) maps to world (x, floorY, y), and the first point
        /// is appended again at the end so the ring closes. Returns null when the
        /// parcel has fewer than three vertices (not a polygon).
        /// </summary>
        public static OverlayPolyline Build(Parcel parcel, float floorY = 0f)
        {
            if (parcel?.Boundary == null || parcel.Boundary.Count < 3) return null;

            var points = new List<Vector3>(parcel.Boundary.Count + 1);
            foreach (var v in parcel.Boundary)
                points.Add(new Vector3(v.x, floorY, v.y));
            points.Add(points[0]); // close the ring back to the first point

            return new OverlayPolyline
            {
                Points = points,
                Color = new Color(0.31f, 0.66f, 0.88f, 1f), // accent
                Label = "Property line",
                Width = BoundaryWidthM
            };
        }

        /// <summary>
        /// Total perimeter length of the closed boundary, in meters. Returns 0 when
        /// the parcel has fewer than three vertices.
        /// </summary>
        public static float PerimeterM(Parcel parcel)
        {
            if (parcel?.Boundary == null || parcel.Boundary.Count < 3) return 0f;

            float total = 0f;
            int n = parcel.Boundary.Count;
            for (int i = 0; i < n; i++)
                total += Vector2.Distance(parcel.Boundary[i], parcel.Boundary[(i + 1) % n]);
            return total;
        }
    }
}
