using System.Collections.Generic;
using UnityEngine;

namespace SuperRealEstate.Construction
{
    public readonly struct Clash
    {
        public readonly string ElementA;
        public readonly string ElementB;
        public readonly BuildingSystem SystemA;
        public readonly BuildingSystem SystemB;
        public readonly Vector3 Point;
        public readonly float DistanceM;

        public Clash(string a, string b, BuildingSystem sa, BuildingSystem sb, Vector3 point, float distance)
        {
            ElementA = a; ElementB = b; SystemA = sa; SystemB = sb; Point = point; DistanceM = distance;
        }
    }

    /// <summary>
    /// Finds where runs from different systems come too close — the duct through
    /// the joist, the pipe crossing the electrical. Catching these in AR before
    /// the wall closes is exactly the value a construction team wants. Pure 3D
    /// segment-segment geometry; unit-tested.
    /// </summary>
    public static class ClashDetector
    {
        public static List<Clash> Find(SystemsModel model, float clearanceM)
        {
            var clashes = new List<Clash>();
            var runs = new List<SystemElement>();
            foreach (var e in model.Elements)
                if (e.Kind == ElementKind.Run && e.Points != null && e.Points.Count >= 2) runs.Add(e);

            for (int i = 0; i < runs.Count; i++)
            for (int j = i + 1; j < runs.Count; j++)
            {
                if (runs[i].System == runs[j].System) continue; // same trade isn't a clash
                if (TryClash(runs[i], runs[j], clearanceM, out var clash)) clashes.Add(clash);
            }
            return clashes;
        }

        private static bool TryClash(SystemElement a, SystemElement b, float clearanceM, out Clash clash)
        {
            float best = float.MaxValue;
            Vector3 at = Vector3.zero;
            for (int i = 1; i < a.Points.Count; i++)
            for (int j = 1; j < b.Points.Count; j++)
            {
                float d = SegmentDistance(a.Points[i - 1], a.Points[i], b.Points[j - 1], b.Points[j], out var mid);
                if (d < best) { best = d; at = mid; }
            }
            if (best <= clearanceM)
            {
                clash = new Clash(a.Id, b.Id, a.System, b.System, at, best);
                return true;
            }
            clash = default;
            return false;
        }

        /// <summary>Shortest distance between two 3D segments (clamped). Returns the midpoint of the closest approach.</summary>
        public static float SegmentDistance(Vector3 p1, Vector3 q1, Vector3 p2, Vector3 q2, out Vector3 midpoint)
        {
            Vector3 d1 = q1 - p1, d2 = q2 - p2, r = p1 - p2;
            float a = Vector3.Dot(d1, d1), e = Vector3.Dot(d2, d2), f = Vector3.Dot(d2, r);
            const float EPS = 1e-8f;
            float s, t;

            if (a <= EPS && e <= EPS) { midpoint = (p1 + p2) * 0.5f; return r.magnitude; }
            if (a <= EPS) { s = 0f; t = Mathf.Clamp01(f / e); }
            else
            {
                float c = Vector3.Dot(d1, r);
                if (e <= EPS) { t = 0f; s = Mathf.Clamp01(-c / a); }
                else
                {
                    float b = Vector3.Dot(d1, d2);
                    float denom = a * e - b * b;
                    s = denom > EPS ? Mathf.Clamp01((b * f - c * e) / denom) : 0f;
                    t = (b * s + f) / e;
                    if (t < 0f) { t = 0f; s = Mathf.Clamp01(-c / a); }
                    else if (t > 1f) { t = 1f; s = Mathf.Clamp01((b - c) / a); }
                }
            }

            Vector3 c1 = p1 + d1 * s, c2 = p2 + d2 * t;
            midpoint = (c1 + c2) * 0.5f;
            return (c1 - c2).magnitude;
        }
    }
}
