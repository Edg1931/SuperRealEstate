using System.Collections.Generic;

namespace SuperRealEstate.Overlays
{
    /// <summary>
    /// Contract the AR layer implements to draw building-system overlays (the
    /// "show me where the mechanicals are" view). The builders in this module emit
    /// pure primitives; concrete implementations live in the AR layer (e.g.
    /// LineRenderer for polylines, world-space billboards/labels for markers).
    /// </summary>
    public interface ISystemsOverlayRenderer
    {
        /// <summary>Draws the given run polylines (wire/duct/pipe).</summary>
        void RenderPolylines(IReadOnlyList<OverlayPolyline> polylines);

        /// <summary>Draws the given fixture markers (outlet/register/valve/panel).</summary>
        void RenderMarkers(IReadOnlyList<OverlayMarker> markers);

        /// <summary>Removes everything this renderer has drawn.</summary>
        void Clear();
    }

    /// <summary>
    /// Contract the AR layer implements to draw property overlays: the parcel
    /// boundary ("show the property lines") and comp tags ("show the comps").
    /// Concrete implementations live in the AR layer (ground-anchored LineRenderer
    /// for the boundary, world-space labels for the comp tags).
    /// </summary>
    public interface IPropertyOverlayRenderer
    {
        /// <summary>Draws the closed parcel boundary on the ground.</summary>
        void RenderParcel(OverlayPolyline parcel);

        /// <summary>Positions and draws the comp content tags over neighbouring homes.</summary>
        void RenderCompTags(IReadOnlyList<OverlayTag> tags);

        /// <summary>Removes everything this renderer has drawn.</summary>
        void Clear();
    }
}
