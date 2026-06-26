using System.Collections.Generic;
using SuperRealEstate.Collaboration;
using SuperRealEstate.Renovation;
using SuperRealEstate.Staging;

namespace SuperRealEstate.Projects
{
    /// <summary>The world-ready result: the (renovated) model + furniture placed in world space.</summary>
    public sealed class StagedScene
    {
        public BuildingModel Model;
        public List<Placement> Placements = new List<Placement>();
    }

    /// <summary>
    /// The "open the AR app and the space is already staged" step. It takes a
    /// project's pieces (base model, renovation plan, the design authored in
    /// blueprint or anchor space) plus an on-site registration and produces a
    /// world-space scene to render: renovation edits applied, furniture
    /// converted to where it physically sits. Pure orchestration over the tested
    /// engine (RenovationEngine, BlueprintTransform, AnchorFrame); unit-tested.
    /// </summary>
    public static class AutoStager
    {
        /// <summary>
        /// Stage from a design authored on a blueprint, registered on site via two
        /// known points (<see cref="BlueprintTransform"/>). This is the Microsoft-
        /// Layout-style flow: walk the plan on the lot, see it furnished/renovated.
        /// </summary>
        public static StagedScene StageFromBlueprint(
            BuildingModel baseModel,
            RenovationPlan plan,
            IReadOnlyList<BlueprintPlacement> blueprintPlacements,
            BlueprintTransform transform)
        {
            var scene = new StagedScene { Model = ApplyPlan(baseModel, plan) };
            if (blueprintPlacements != null)
                foreach (var bp in blueprintPlacements)
                    if (bp != null) scene.Placements.Add(transform.ToWorldPlacement(bp));
            return scene;
        }

        /// <summary>
        /// Stage from a design stored anchor-relative (shared session), placed via
        /// the resolved <see cref="AnchorFrame"/> — so every device in the session
        /// sees the staged space in the same spot.
        /// </summary>
        public static StagedScene StageFromAnchor(
            BuildingModel baseModel,
            RenovationPlan plan,
            IReadOnlyList<Placement> anchorLocalPlacements,
            AnchorFrame frame)
        {
            var scene = new StagedScene { Model = ApplyPlan(baseModel, plan) };
            if (anchorLocalPlacements != null)
                foreach (var p in anchorLocalPlacements)
                    if (p != null) scene.Placements.Add(frame.ToWorldPlacement(p));
            return scene;
        }

        private static BuildingModel ApplyPlan(BuildingModel baseModel, RenovationPlan plan)
        {
            if (baseModel == null) return null;                  // empty-staging: no structure
            return plan != null ? RenovationEngine.Apply(baseModel, plan) : baseModel;
        }
    }
}
