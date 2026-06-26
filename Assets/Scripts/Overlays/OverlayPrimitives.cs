using System;
using System.Collections.Generic;
using UnityEngine;
using SuperRealEstate.Construction;

namespace SuperRealEstate.Overlays
{
    /// <summary>
    /// A world-anchored polyline the AR layer draws (e.g. a duct/pipe/wire run, or
    /// a property line). Points are in plan-anchored meters; the renderer maps them
    /// to a LineRenderer or equivalent.
    /// </summary>
    [Serializable]
    public sealed class OverlayPolyline
    {
        /// <summary>Ordered vertices of the line, in anchored world meters.</summary>
        public List<Vector3> Points = new List<Vector3>();

        /// <summary>Stroke colour (trade convention for systems; accent for parcels).</summary>
        public Color Color = Color.white;

        /// <summary>Short caption, e.g. "Hvac · 6in duct" or "Property line".</summary>
        public string Label;

        /// <summary>Stroke width in meters at 1 m depth.</summary>
        public float Width = 0.01f;
    }

    /// <summary>
    /// A world-anchored point marker the AR layer draws (e.g. an outlet, register,
    /// valve, or panel fixture).
    /// </summary>
    [Serializable]
    public sealed class OverlayMarker
    {
        /// <summary>Marker position in anchored world meters.</summary>
        public Vector3 Position;

        /// <summary>Marker colour (trade convention for the owning system).</summary>
        public Color Color = Color.white;

        /// <summary>Short caption, e.g. "Electrical · outlet".</summary>
        public string Label;
    }

    /// <summary>
    /// A piece of content text the AR layer positions in the world (e.g. a comp
    /// price tag floated over a neighbouring home). Carries no position itself —
    /// the AR layer decides where to anchor it.
    /// </summary>
    [Serializable]
    public sealed class OverlayTag
    {
        /// <summary>Display text, e.g. "$525,000 · $312/ft²".</summary>
        public string Text;

        /// <summary>Tag colour.</summary>
        public Color Color = Color.white;
    }

    /// <summary>
    /// Maps each building system to a colour following common trade conventions, so
    /// "show me where the mechanicals are" reads at a glance. Colours avoid pure
    /// white (which blooms on passthrough). Pure + deterministic.
    /// </summary>
    public static class SystemPalette
    {
        // Trade-convention colours (electrical = yellow, plumbing = blue, etc.).
        private static readonly Color Electrical  = new Color(0.95f, 0.83f, 0.20f, 1f); // yellow
        private static readonly Color Plumbing    = new Color(0.22f, 0.49f, 0.86f, 1f); // blue
        private static readonly Color Hvac        = new Color(0.30f, 0.74f, 0.45f, 1f); // green
        private static readonly Color Gas         = new Color(0.95f, 0.62f, 0.16f, 1f); // amber
        private static readonly Color Sewer       = new Color(0.45f, 0.31f, 0.18f, 1f); // brown
        private static readonly Color LowVoltage  = new Color(0.27f, 0.80f, 0.82f, 1f); // cyan
        private static readonly Color Structural  = new Color(0.62f, 0.64f, 0.67f, 1f); // gray

        /// <summary>Returns the trade-convention colour for <paramref name="system"/>.</summary>
        public static Color ColorFor(BuildingSystem system)
        {
            switch (system)
            {
                case BuildingSystem.Electrical: return Electrical;
                case BuildingSystem.Plumbing:   return Plumbing;
                case BuildingSystem.Hvac:       return Hvac;
                case BuildingSystem.Gas:        return Gas;
                case BuildingSystem.Sewer:      return Sewer;
                case BuildingSystem.LowVoltage: return LowVoltage;
                case BuildingSystem.Structural: return Structural;
                default:                        return Structural;
            }
        }
    }
}
