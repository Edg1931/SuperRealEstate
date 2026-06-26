using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SuperRealEstate.Renovation;
using SuperRealEstate.Staging;

namespace SuperRealEstate.Projects
{
    /// <summary>
    /// A project's pieces, materialized for staging: the base model, the
    /// renovation plan, and the design placements (authored in blueprint space
    /// and/or anchor-relative for a shared session).
    /// </summary>
    public sealed class LoadedProject
    {
        public RenovationProject Project;
        public BuildingModel BaseModel;
        public RenovationPlan Plan;
        public List<BlueprintPlacement> BlueprintPlacements = new List<BlueprintPlacement>();
        public List<Placement> AnchorPlacements = new List<Placement>();
    }

    /// <summary>
    /// Loads a project and its referenced pieces (model / plan / layout) from the
    /// backend. The concrete implementation fetches from Supabase; this contract
    /// keeps the staging controller backend-agnostic and testable.
    /// </summary>
    public interface IProjectStore
    {
        Task<LoadedProject> LoadAsync(string projectId, CancellationToken ct = default);
    }

    /// <summary>
    /// Renders a <see cref="StagedScene"/> in the AR scene — instantiate the
    /// (renovated) structure and place the furniture. Implemented by the AR layer
    /// (Unity prefabs / mesh); kept here as a pure seam so the controller and the
    /// auto-stager don't depend on the renderer.
    /// </summary>
    public interface IStagedSceneRenderer
    {
        void Render(StagedScene scene);
        void Clear();
    }
}
