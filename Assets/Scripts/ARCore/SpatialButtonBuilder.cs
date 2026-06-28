using System;
using TMPro;
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
            Action onSelect, TMP_FontAsset font = null, Color? accent = null)
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

            // Hover/press feedback — brightens the accent bar + grows on hover.
            Color accentColor = accent ?? DesignTokens.Accent;
            var feedback = root.AddComponent<SpatialHoverFeedback>();
            var barRenderer = bar != null ? bar.GetComponentInChildren<Renderer>() : null;
            feedback.SetHighlight(barRenderer, accentColor, Color.Lerp(accentColor, DesignTokens.OnSurface, 0.4f));

            if (!string.IsNullOrEmpty(label))
            {
                TextMeshPro tmp = SpatialLabel.Build(
                    root.transform, label, font,
                    DesignTokens.TypeBodyDeg, SpatialComfort.DepthSweetM, AmbientLight.Palette.OnSurface);
                tmp.transform.localPosition = new Vector3(0f, 0f, -0.003f);
            }

            return root;
        }
    }
}
