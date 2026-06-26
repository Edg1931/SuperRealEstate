using System;
using System.Collections.Generic;
using UnityEngine;
using SuperRealEstate.Construction;

namespace SuperRealEstate.Overlays
{
    /// <summary>The renderable result of overlaying a building's MEP systems.</summary>
    [Serializable]
    public sealed class SystemsOverlay
    {
        /// <summary>One polyline per run (wire/duct/pipe), coloured by trade.</summary>
        public List<OverlayPolyline> Polylines = new List<OverlayPolyline>();

        /// <summary>One marker per fixture (outlet/register/valve/panel).</summary>
        public List<OverlayMarker> Markers = new List<OverlayMarker>();
    }

    /// <summary>
    /// Turns a <see cref="SystemsModel"/> into renderable primitives for the
    /// "show me where the mechanicals are" overlay: one <see cref="OverlayPolyline"/>
    /// per run and one <see cref="OverlayMarker"/> per fixture, coloured by trade.
    /// Pure + testable; the AR layer renders the result.
    /// </summary>
    public static class SystemsOverlayBuilder
    {
        /// <summary>Stroke width for run polylines, in meters at 1 m depth.</summary>
        public const float RunWidthM = 0.015f;

        /// <summary>
        /// Builds the overlay. Runs with fewer than two points and fixtures with no
        /// point are skipped (nothing to draw). Never returns null.
        /// </summary>
        public static SystemsOverlay Build(SystemsModel model)
        {
            var overlay = new SystemsOverlay();
            if (model?.Elements == null) return overlay;

            foreach (var element in model.Elements)
            {
                if (element == null) continue;

                if (element.Kind == ElementKind.Run)
                {
                    if (element.Points == null || element.Points.Count < 2) continue; // too few to draw
                    overlay.Polylines.Add(new OverlayPolyline
                    {
                        Points = new List<Vector3>(element.Points),
                        Color = SystemPalette.ColorFor(element.System),
                        Label = LabelFor(element),
                        Width = RunWidthM
                    });
                }
                else // Fixture
                {
                    if (element.Points == null || element.Points.Count < 1) continue; // nowhere to place
                    overlay.Markers.Add(new OverlayMarker
                    {
                        Position = element.Points[0],
                        Color = SystemPalette.ColorFor(element.System),
                        Label = LabelFor(element)
                    });
                }
            }

            return overlay;
        }

        /// <summary>Builds a "System · spec" caption, falling back gracefully.</summary>
        private static string LabelFor(SystemElement element)
        {
            string system = element.System.ToString();
            if (!string.IsNullOrEmpty(element.Spec)) return $"{system} · {element.Spec}";
            if (!string.IsNullOrEmpty(element.Label)) return $"{system} · {element.Label}";
            return system;
        }
    }
}
