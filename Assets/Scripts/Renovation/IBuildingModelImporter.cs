using System.Threading;
using System.Threading.Tasks;

namespace SuperRealEstate.Renovation
{
    /// <summary>Where a captured model came from. Determines how editable it is.</summary>
    public enum CaptureSource
    {
        RoomPlan,   // Apple RoomPlan — parametric walls/doors/windows (best for editing)
        CubiCasa,   // floor-plan-from-scan — vector/DXF walls
        Polycam,    // raw mesh/splat + (2026) editable floor-plan layer / DXF
        Matterport, // visual twin + read-only room dimensions; NOT editable walls
        ArScan,     // our own AR Foundation scan
        Manual      // built in the desktop authoring tool
    }

    public sealed class CaptureReference
    {
        public CaptureSource Source;
        public string Uri;     // file path, storage URL, or vendor model id
        public string Format;  // usdz | dxf | svg | glb | json
    }

    /// <summary>
    /// Turns a capture into an editable <see cref="BuildingModel"/>.
    ///
    /// Source reality (per research):
    ///  • RoomPlan / CubiCasa / Polycam floor-plan → semantic walls/openings we
    ///    can edit directly. These are the primary import paths.
    ///  • Matterport → a baked mesh + read-only room dimensions (Enterprise
    ///    Property Intelligence). Use it as a visual twin and to *seed* a
    ///    parametric model; it does not hand us editable walls.
    ///  • None of these capture finishes/materials — the AI surface-finish
    ///    recognition layer (Insights) supplies those.
    ///
    /// Concrete parsers are deferred; this interface defines the seam.
    /// </summary>
    public interface IBuildingModelImporter
    {
        CaptureSource Source { get; }
        Task<BuildingModel> ImportAsync(CaptureReference reference, CancellationToken ct = default);
    }
}
