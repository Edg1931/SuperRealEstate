using UnityEngine;
using SuperRealEstate.UI;

namespace SuperRealEstate.ARRender
{
    /// <summary>
    /// Builds the design-system "Spatial Glass" panel at runtime from
    /// <see cref="DesignTokens"/> — the frosted, never-pure-white surface that HUD
    /// cards (insight, measurement, plant, tool menu) float on. This replaces
    /// "no UI / a bare cube" with the real styled surface until authored UI
    /// prefabs land; the geometry + colors come straight from the tokens, so it
    /// stays on-brand and updates if the tokens change.
    ///
    /// Sizes are in METERS (spatial UI is world-space). The panel faces +Z; place
    /// and orient the returned object, then parent your text/controls under it.
    /// </summary>
    public static class SpatialPanelBuilder
    {
        /// <summary>
        /// Create a panel <paramref name="widthM"/> × <paramref name="heightM"/>
        /// meters: a frosted <see cref="DesignTokens.Surface"/> face with a faint
        /// <see cref="DesignTokens.EdgeHalo"/> rim behind it to separate it from a
        /// busy passthrough background. Returns the root (named <paramref name="name"/>).
        /// </summary>
        public static GameObject Build(float widthM, float heightM, string name = "SpatialPanel")
        {
            var root = new GameObject(name);

            // Palette adapted to the current room luminance (denser in bright rooms).
            AdaptedPalette palette = AmbientLight.Palette;

            // Faint rim slightly larger and behind, for separation on passthrough.
            float rim = DesignTokens.SpaceXs;
            Quad("Edge", root.transform,
                widthM + rim * 2f, heightM + rim * 2f, 0.0005f,
                new Color(palette.OnSurfaceMuted.r, palette.OnSurfaceMuted.g,
                          palette.OnSurfaceMuted.b, DesignTokens.EdgeHalo));

            // Frosted surface face.
            GameObject face = Quad("Surface", root.transform, widthM, heightM, 0f, palette.Surface);
            // Nudge the face just in front of the rim.
            face.transform.localPosition = new Vector3(0f, 0f, -0.0006f);

            return root;
        }

        /// <summary>
        /// Add a left-aligned accent bar (e.g. a category/role color) down the
        /// panel's leading edge — a common card affordance. Parent under a panel.
        /// </summary>
        public static GameObject AddAccentBar(Transform panel, float heightM, Color color)
        {
            float w = DesignTokens.SpaceXs;
            GameObject bar = Quad("Accent", panel, w, heightM, -0.001f, color);
            // Pin to the left edge: panels are centered, so shift by half width is
            // the caller's job if needed; default sits at center-left of content.
            return bar;
        }

        private static GameObject Quad(string name, Transform parent, float widthM, float heightM, float zOffset, Color color)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = name;
            go.transform.SetParent(parent, worldPositionStays: false);
            go.transform.localScale = new Vector3(Mathf.Max(0.001f, widthM), Mathf.Max(0.001f, heightM), 1f);
            go.transform.localPosition = new Vector3(0f, 0f, zOffset);

            // Panels shouldn't intercept gaze ray-casts meant for content.
            Collider col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);

            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null) mr.sharedMaterial = TransparentMaterial(color);
            return go;
        }

        private static Material TransparentMaterial(Color color)
        {
            // Sprites/Default reliably supports vertex alpha across pipelines; fall
            // back to URP/Standard if a project strips it.
            Shader shader = Shader.Find("Sprites/Default")
                            ?? Shader.Find("Universal Render Pipeline/Unlit")
                            ?? Shader.Find("Unlit/Transparent")
                            ?? Shader.Find("Standard");
            return new Material(shader) { color = color };
        }
    }
}
