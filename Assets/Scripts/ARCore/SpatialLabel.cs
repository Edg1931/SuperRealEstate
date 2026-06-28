using TMPro;
using UnityEngine;
using SuperRealEstate.UI;

namespace SuperRealEstate.ARCore
{
    /// <summary>
    /// Builds crisp world-space text with <b>TextMeshPro</b> (SDF — anti-aliased at
    /// any depth, proper transparency over passthrough), sized from the angular
    /// type scale so a cap-height token subtends the right visual angle at the
    /// panel's depth. Replaces the legacy aliased <c>TextMesh</c> placeholder.
    ///
    /// Font: pass a <see cref="TMP_FontAsset"/>; when null, TMP uses the project's
    /// default (import "TMP Essential Resources" once via Window → TextMeshPro).
    /// The cap-to-meters calibration is a sensible default — fine-tune
    /// <see cref="CapUnitsAtBaseFont"/> in-editor for your chosen font if needed.
    /// </summary>
    public static class SpatialLabel
    {
        private const float BaseFontSize = 10f;
        // Approx. local-unit cap height of this font at BaseFontSize (TMP world text).
        // Tune per font; the object is scaled so this maps to the target meters.
        private const float CapUnitsAtBaseFont = 7f;

        public static TextMeshPro Build(
            Transform parent, string text, TMP_FontAsset font,
            float capHeightDeg, float depthM, Color color,
            TextAlignmentOptions alignment = TextAlignmentOptions.Center)
        {
            var go = new GameObject("Label");
            go.transform.SetParent(parent, worldPositionStays: false);

            var tmp = go.AddComponent<TextMeshPro>();
            if (font != null) tmp.font = font;
            tmp.text = text;
            tmp.color = color;
            tmp.alignment = alignment;
            tmp.fontSize = BaseFontSize;

            var rt = tmp.rectTransform;
            rt.sizeDelta = new Vector2(48f, 6f); // generous rect in TMP units; centered

            // Scale the object so the cap height matches the angular token in meters.
            float capM = Mathf.Max(0.0001f, SpatialComfort.AngularToMeters(capHeightDeg, depthM));
            float scale = capM / CapUnitsAtBaseFont;
            go.transform.localScale = new Vector3(scale, scale, scale);

            return tmp;
        }
    }
}
