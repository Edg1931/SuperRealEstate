using System;
using System.Collections.Generic;

namespace SuperRealEstate.Insights
{
    /// <summary>
    /// A recognized CURRENT finish on a surface — what the AI sees on that wall
    /// or floor as you walk in. E.g. "Sherwin-Williams Agreeable Gray SW 7029,
    /// ~$45/gal". Used to (a) tell the buyer what's there and what it cost, and
    /// (b) seed a one-tap re-finish / "shop this look" with a real product.
    ///
    /// Brand/product/price are best-effort recognition and are ADVISORY — the
    /// UI should let the user confirm or correct, and never assert a brand as
    /// fact for pricing/contractual purposes.
    /// </summary>
    [Serializable]
    public sealed class SurfaceFinding
    {
        public string SurfaceKind;   // floor | wall | ceiling | cabinet | countertop
        public string MaterialType;  // paint | hardwood | tile | carpet | laminate | quartz ...
        public string Brand;         // e.g. "Sherwin-Williams"
        public string Product;       // e.g. "Agreeable Gray SW 7029"
        public string ColorHex;      // e.g. "#D1CBC1"
        public float EstimatedUnitCost;
        public string Unit;          // per_gallon | per_sqft | per_sqm
        public float Confidence;     // 0..1
        public bool IsAdvisory = true;
        public string Disclaimer;
        public string Note;
    }

    /// <summary>Full result of a scene analysis pass: insight cards + recognized surfaces.</summary>
    public sealed class SceneAnalysis
    {
        public IReadOnlyList<SceneInsight> Insights;
        public IReadOnlyList<SurfaceFinding> Surfaces;

        public SceneAnalysis(IReadOnlyList<SceneInsight> insights, IReadOnlyList<SurfaceFinding> surfaces)
        {
            Insights = insights ?? Array.Empty<SceneInsight>();
            Surfaces = surfaces ?? Array.Empty<SurfaceFinding>();
        }
    }
}
