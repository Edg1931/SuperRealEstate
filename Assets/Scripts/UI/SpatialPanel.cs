using UnityEngine;

namespace SuperRealEstate.UI
{
    /// <summary>
    /// A placed spatial UI surface — the base for cards, HUDs, menus. Holds its
    /// anchoring + intended depth, and resolves comfortable physical sizing from
    /// the angular design tokens (so a card reads the same at any distance).
    /// </summary>
    [System.Serializable]
    public sealed class SpatialPanel
    {
        public string Id;
        public AnchorMode Anchor = AnchorMode.SurfaceLocked;
        public float DepthM = SpatialComfort.DepthSweetM;

        /// <summary>Subject this panel describes (world point), for world/surface anchoring.</summary>
        public Vector3 SubjectPosition;
        public Vector3 SubjectNormal = Vector3.forward;

        /// <summary>Clamp the panel's depth into the comfortable vergence range.</summary>
        public float ComfortableDepth() => SpatialComfort.ClampDepth(DepthM);

        /// <summary>Physical height (m) for a token cap-height at this panel's depth.</summary>
        public float TextHeightMeters(float capHeightDeg)
            => SpatialComfort.AngularToMeters(capHeightDeg, ComfortableDepth());

        /// <summary>Body text physical height at this depth (from DesignTokens).</summary>
        public float BodyTextMeters() => TextHeightMeters(DesignTokens.TypeBodyDeg);
    }
}
