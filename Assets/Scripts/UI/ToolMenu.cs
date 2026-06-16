using System.Collections.Generic;

namespace SuperRealEstate.UI
{
    /// <summary>The core tools, surfaced in the wrist/hand radial menu.</summary>
    public enum ToolId
    {
        Measure,     // room / area / volume
        Identify,    // gaze a thing → it explains itself (insights, finishes, plants)
        Finish,      // re-finish a surface / shop this look
        Stage,       // place furniture (own or vendor catalog)
        RemoveWall,  // wall-removal portal
        Notes,       // spatial annotations
        Landscape    // outdoor measure + plant ID + bulk-material calculators
    }

    [System.Serializable]
    public sealed class ToolMenuItem
    {
        public ToolId Tool;
        public string Label;
        public string Icon;     // glyph/asset id
        public ToolMenuItem(ToolId tool, string label, string icon) { Tool = tool; Label = label; Icon = icon; }
    }

    /// <summary>
    /// The radial tool palette — summoned at the hand/wrist (never head-locked),
    /// selected by gaze + pinch. Ordered for one-pinch reach.
    /// </summary>
    [System.Serializable]
    public sealed class RadialToolMenu
    {
        public AnchorMode Anchor = AnchorMode.WristLocked;

        public readonly List<ToolMenuItem> Items = new List<ToolMenuItem>
        {
            new ToolMenuItem(ToolId.Measure,    "Measure",      "ruler"),
            new ToolMenuItem(ToolId.Identify,   "Identify",     "scan"),
            new ToolMenuItem(ToolId.Finish,     "Finishes",     "swatch"),
            new ToolMenuItem(ToolId.Stage,      "Stage",        "sofa"),
            new ToolMenuItem(ToolId.RemoveWall, "Remove wall",  "wall"),
            new ToolMenuItem(ToolId.Notes,      "Notes",        "pin"),
            new ToolMenuItem(ToolId.Landscape,  "Landscape",    "leaf"),
        };
    }
}
