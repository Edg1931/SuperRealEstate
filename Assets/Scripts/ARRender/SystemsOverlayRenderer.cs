using System.Collections.Generic;
using UnityEngine;
using SuperRealEstate.Overlays;

namespace SuperRealEstate.ARRender
{
    /// <summary>
    /// In-scene renderer layer: the concrete, visual implementation of
    /// <see cref="ISystemsOverlayRenderer"/> (the "show me where the mechanicals
    /// are" view). Each <see cref="OverlayPolyline"/> becomes a
    /// <see cref="LineRenderer"/> (wire/duct/pipe run) and each
    /// <see cref="OverlayMarker"/> a small colored sphere (outlet/register/valve/
    /// panel). Everything is parented under a single child root so it can be
    /// torn down in one shot.
    /// </summary>
    public sealed class SystemsOverlayRenderer : MonoBehaviour, ISystemsOverlayRenderer
    {
        private const string RootName = "SystemsOverlayRoot";

        /// <summary>Diameter (m) of a fixture marker sphere.</summary>
        [SerializeField] private float markerDiameterM = 0.05f;

        private Transform _root;

        /// <summary>Draws the given run polylines (wire/duct/pipe).</summary>
        public void RenderPolylines(IReadOnlyList<OverlayPolyline> polylines)
        {
            EnsureRoot();
            if (polylines == null) return;

            foreach (var poly in polylines)
            {
                if (poly == null || poly.Points == null || poly.Points.Count < 2) continue;
                DrawPolyline(poly);
            }
        }

        /// <summary>Draws the given fixture markers (outlet/register/valve/panel).</summary>
        public void RenderMarkers(IReadOnlyList<OverlayMarker> markers)
        {
            EnsureRoot();
            if (markers == null) return;

            foreach (var marker in markers)
            {
                if (marker == null) continue;
                DrawMarker(marker);
            }
        }

        /// <summary>Removes everything this renderer has drawn.</summary>
        public void Clear()
        {
            if (_root == null) return;
            ClearChildren(_root);
        }

        // --- Drawing ------------------------------------------------------

        private void DrawPolyline(OverlayPolyline poly)
        {
            var go = new GameObject(string.IsNullOrEmpty(poly.Label) ? "Run" : poly.Label);
            go.transform.SetParent(_root, false);

            var lr = go.AddComponent<LineRenderer>();
            ConfigureLine(lr, poly.Points, poly.Color, poly.Width, loop: false);
        }

        private void DrawMarker(OverlayMarker marker)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = string.IsNullOrEmpty(marker.Label) ? "Fixture" : marker.Label;
            go.transform.SetParent(_root, false);
            go.transform.localPosition = marker.Position;
            go.transform.localScale = Vector3.one * Mathf.Max(0.001f, markerDiameterM);

            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null) mr.material.color = marker.Color;
        }

        // Shared LineRenderer setup used here and by other overlay renderers.
        internal static void ConfigureLine(LineRenderer lr, IList<Vector3> points, Color color, float width, bool loop)
        {
            lr.useWorldSpace = false;
            lr.loop = loop;
            lr.widthMultiplier = Mathf.Max(0.0005f, width);
            lr.numCornerVertices = 2;
            lr.numCapVertices = 2;
            lr.startColor = color;
            lr.endColor = color;

            // Unlit, vertex-colored material so the trade color reads on passthrough.
            var shader = Shader.Find("Sprites/Default");
            if (shader != null) lr.material = new Material(shader);

            lr.positionCount = points.Count;
            for (int i = 0; i < points.Count; i++)
                lr.SetPosition(i, points[i]);
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
