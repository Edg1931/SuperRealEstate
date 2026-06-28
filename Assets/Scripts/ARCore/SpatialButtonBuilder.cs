using System;
using UnityEngine;
using SuperRealEstate.ARRender;
using SuperRealEstate.UI;

namespace SuperRealEstate.ARCore
{
    /// <summary>
    /// Builds an interactive world-space button on the design-system panel: a
    /// <see cref="SpatialPanelBuilder"/> surface + a <see cref="BoxCollider"/> +
    /// a <see cref="GazeTarget"/> wired to a callback. Because it uses the same
    /// <c>GazeTarget</c> the input layer raycasts to, it works on every platform
    /// (Android XR eye-gaze + pinch / visionOS pointer + tap / phone touch) with
    /// no per-platform code. Text is optional (font-gated) so it never breaks a
    /// build without a font assigned; assign one to label the buttons.
    /// </summary>
    public static class SpatialButtonBuilder
    {
        /// <summary>
        /// Create a button <paramref name="widthM"/> × <paramref name="heightM"/>
        /// meters with a leading accent bar and an optional text label. Calls
        /// <paramref name="onSelect"/> when the user commits (pinch/tap) while gazing.
        /// </summary>
        public static GameObject Build(
            string id, string label, float widthM, float heightM,
            Action onSelect, Font font = null, Color? accent = null)
        {
            GameObject root = SpatialPanelBuilder.Build(widthM, heightM, $"Button_{id}");

            // Leading accent bar for affordance.
            GameObject bar = SpatialPanelBuilder.AddAccentBar(root.transform, heightM, accent ?? DesignTokens.Accent);
            bar.transform.localPosition = new Vector3(-widthM * 0.5f + DesignTokens.SpaceS, 0f, -0.001f);

            // Collider must exist before GazeTarget (it RequireComponents a Collider).
            var box = root.AddComponent<BoxCollider>();
            box.size = new Vector3(widthM, heightM, 0.01f);

            var target = root.AddComponent<GazeTarget>();
            target.Id = string.IsNullOrEmpty(id) ? $"btn-{root.GetInstanceID()}" : id;
            if (onSelect != null)
                target.OnSelect.AddListener(() => onSelect());

            if (font != null && !string.IsNullOrEmpty(label))
                AddLabel(root.transform, label, font, widthM, heightM);

            return root;
        }

        private static void AddLabel(Transform parent, string text, Font font, float widthM, float heightM)
        {
            var go = new GameObject("Label");
            go.transform.SetParent(parent, worldPositionStays: false);
            go.transform.localPosition = new Vector3(DesignTokens.SpaceS, 0f, -0.003f);

            var tm = go.AddComponent<TextMesh>();
            tm.text = text;
            tm.font = font;
            tm.fontSize = 64;                 // high res; scaled down by characterSize
            tm.characterSize = heightM * 0.018f; // tune in-editor for the panel height
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = DesignTokens.OnSurface;

            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null && font.material != null) mr.sharedMaterial = font.material;
        }
    }
}
