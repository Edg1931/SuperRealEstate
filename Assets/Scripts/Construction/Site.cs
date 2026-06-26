using System;
using System.Collections.Generic;
using UnityEngine;

namespace SuperRealEstate.Construction
{
    /// <summary>A parcel/lot boundary (property lines) in plan meters (X/Z).</summary>
    [Serializable]
    public sealed class Parcel
    {
        public string Id;
        public List<Vector2> Boundary = new List<Vector2>();
    }

    /// <summary>A building footprint placed on the lot, with a facing heading.</summary>
    [Serializable]
    public sealed class SiteBuilding
    {
        public string Id;
        public List<Vector2> Footprint = new List<Vector2>();
        public float HeadingDegrees; // which way the front faces (0 = +Z / north)
    }

    public readonly struct SetbackResult
    {
        public readonly bool Compliant;
        public readonly float MinSetbackM;   // closest the building gets to a property line
        public readonly bool InsideLot;
        public readonly string Reason;

        public SetbackResult(bool compliant, float minSetbackM, bool insideLot, string reason)
        {
            Compliant = compliant; MinSetbackM = minSetbackM; InsideLot = insideLot; Reason = reason;
        }
    }

    /// <summary>
    /// Checks a building footprint against the property lines: is it inside the
    /// lot, and does it keep the required setback from every boundary? Advisory
    /// (confirm with the jurisdiction), but invaluable when walking a blueprint
    /// on the real lot. Pure 2D geometry; unit-tested.
    /// </summary>
    public static class SetbackChecker
    {
        public static SetbackResult Check(Parcel parcel, SiteBuilding building, float requiredSetbackM)
        {
            if (parcel?.Boundary == null || parcel.Boundary.Count < 3)
                return new SetbackResult(false, 0f, false, "invalid parcel");
            if (building?.Footprint == null || building.Footprint.Count < 3)
                return new SetbackResult(false, 0f, false, "invalid footprint");

            bool inside = true;
            float minGap = float.MaxValue;
            foreach (var v in building.Footprint)
            {
                if (!PointInPolygon(parcel.Boundary, v)) inside = false;
                float d = DistanceToBoundary(parcel.Boundary, v);
                if (d < minGap) minGap = d;
            }

            if (!inside)
                return new SetbackResult(false, minGap, false, "footprint extends past the property line");
            if (minGap < requiredSetbackM)
                return new SetbackResult(false, minGap, true,
                    $"only {minGap:0.0} m to a property line (needs {requiredSetbackM:0.0} m)");
            return new SetbackResult(true, minGap, true, "compliant");
        }

        public static bool PointInPolygon(IReadOnlyList<Vector2> poly, Vector2 p)
        {
            bool inside = false;
            int n = poly.Count;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                bool crosses = ((poly[i].y > p.y) != (poly[j].y > p.y)) &&
                               (p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x);
                if (crosses) inside = !inside;
            }
            return inside;
        }

        public static float DistanceToBoundary(IReadOnlyList<Vector2> poly, Vector2 p)
        {
            float min = float.MaxValue;
            int n = poly.Count;
            for (int i = 0; i < n; i++)
            {
                float d = DistancePointToSegment(p, poly[i], poly[(i + 1) % n]);
                if (d < min) min = d;
            }
            return min;
        }

        private static float DistancePointToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float len2 = ab.sqrMagnitude;
            if (len2 < 1e-8f) return Vector2.Distance(p, a);
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2);
            return Vector2.Distance(p, a + t * ab);
        }
    }
}
