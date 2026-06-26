using System;

namespace SuperRealEstate.Projects
{
    /// <summary>What the client is doing with the space.</summary>
    public enum ProjectKind
    {
        EmptyStaging,  // stage an empty room with furniture
        Renovation,    // re-finish / remove walls / reconfigure
        Extension,     // add to the existing structure
        NewBuild       // build from a plan on a bare lot
    }

    /// <summary>Where the blueprint/measurements came from — any source, one pipeline.</summary>
    public enum BlueprintOrigin
    {
        PhoneScan,       // CubiCasa-style room scan on the phone
        CubiCasaUpload,  // an uploaded CubiCasa floor plan
        CubiCasaApi,     // CubiCasa integration
        MatterportApi,   // Matterport model (seeds rooms from dimensions)
        RoomPlan,        // Apple RoomPlan capture
        ManualDesktop    // drawn in the desktop authoring tool
    }

    public enum ProjectStatus { Draft, Designed, ReadyForAr, Archived }

    /// <summary>
    /// A renovation/staging project for a client — the single record that ties a
    /// blueprint (from any source) to its 3D model, the design (staging layout +
    /// renovation plan), and its AR-ready status. Design it on phone/desktop;
    /// when the client opens the AR app on Galaxy XR or Vision Pro, the space is
    /// already staged because the project carries the model + measurements.
    /// Mirrors `renovation_projects`.
    /// </summary>
    [Serializable]
    public sealed class RenovationProject
    {
        public string Id;
        public string Name = "Project";
        public string PropertyId;
        public ProjectKind Kind = ProjectKind.Renovation;
        public BlueprintOrigin Origin = BlueprintOrigin.PhoneScan;
        public ProjectStatus Status = ProjectStatus.Draft;

        // The pieces it bundles (by id; loaded on demand):
        public string BlueprintId;
        public string BuildingModelId;
        public string StagingLayoutId;
        public string RenovationPlanId;
    }
}
