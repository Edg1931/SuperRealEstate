using System.Collections.Generic;
using UnityEngine;
using SuperRealEstate.Overlays;

namespace SuperRealEstate.ARRender
{
    /// <summary>
    /// In-scene renderer layer: the concrete, visual implementation of
    /// <see cref="IPropertyOverlayRenderer"/>. Draws the parcel boundary as a
    /// closed-loop <see cref="LineRenderer"/> on the ground ("show the property
    /// lines") and floats comp tags as world-space <see cref="TextMesh"/> labels
    /// ("show the comps"). Everything is parented under a single child root so it
    /// can be torn down in one shot.
    /// </summary>
    public sealed class PropertyOverlayRenderer : MonoBehaviour, IPropertyOverlayRenderer
    {
        private const string RootName = "PropertyOverlayRoot";

        /// <summary>Cap height (m) of a comp tag label.</summary>
        [SerializeField] private float tagHeightM = 0.15f;

        /// <summary>
        /// Where comp tags are placed when the contract carries no per-tag
        /// position. Tags are stacked upward from here so they don't overlap;
        /// the AR layer can reposition them once anchored to real homes.
        /// </summary>
        [SerializeField] private Vector3 tagAnchor = new Vector3(0f, 1.6f, 0f);

        /// <summary>Vertical gap (m) between stacked comp tags.</summary>
        [SerializeField] private float tagSpacingM = 0.25f;

        private Transform _root;

        /// <summary>Draws the closed parcel boundary on the ground.</summary>
        public void RenderParcel(OverlayPolyline parcel)
        {
            EnsureRoot();
            if (parcel == null || parcel.Points == null || parcel.Points.Count < 2) return;

            var go = new GameObject(string.IsNullOrEmpty(parcel.Label) ? "Parcel" : parcel.Label);
            go.transform.SetParent(_root, false);

            var lr = go.AddComponent<LineRenderer>();
            // Closed loop: a parcel boundary returns to its first vertex.
            SystemsOverlayRenderer.ConfigureLine(lr, parcel.Points, parcel.Color, parcel.Width, loop: true);
        }

        /// <summary>Positions and draws the comp content tags over neighbouring homes.</summary>
        public void RenderCompTags(IReadOnlyList<OverlayTag> tags)
        {
            EnsureRoot();
            if (tags == null) return;

            int row = 0;
            foreach (var tag in tags)
            {
                if (tag == null || string.IsNullOrEmpty(tag.Text)) continue;
                DrawTag(tag, row++);
            }
        }

        /// <summary>Removes everything this renderer has drawn.</summary>
        public void Clear()
        {
            if (_root == null) return;
            ClearChildren(_root);
        }

        // --- Drawing ------------------------------------------------------

        private void DrawTag(OverlayTag tag, int row)
        {
            var go = new GameObject("CompTag");
            go.transform.SetParent(_root, false);
            go.transform.localPosition = tagAnchor + Vector3.up * (row * tagSpacingM);

            var tm = go.AddComponent<TextMesh>();
            tm.text = tag.Text;
            tm.color = tag.Color;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;

            // TextMesh authors at a large font size then scales down so the glyphs
            // stay crisp; characterSize converts that to the desired world height.
            tm.fontSize = 64;
            tm.characterSize = Mathf.Max(0.001f, tagHeightM) / tm.fontSize * 10f;
        }

        // --- Helpers ------------------------------------------------------

        private void EnsureRoot()
        {
            if (_root != null) return;

            var existing = transform.Find(RootName);
            if (existing != null)
            {
                _root = existing;
                return;
            }

            var rootGo = new GameObject(RootName);
            rootGo.transform.SetParent(transform, false);
            _root = rootGo.transform;
        }

        private static void ClearChildren(Transform parent)
        {
            if (parent == null) return;
            for (int i = parent.childCount - 1; i >= 0; i--)
                DestroyImmediate(parent.GetChild(i).gameObject);
        }
    }
}
