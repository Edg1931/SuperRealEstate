using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using SuperRealEstate.Collaboration;
using SuperRealEstate.Projects;
using SuperRealEstate.Staging;

namespace SuperRealEstate.ARCore
{
    /// <summary>
    /// The "open the AR app and the space is already staged" hook. Loads a saved
    /// project (model + renovation + design), registers it to the real space, runs
    /// <see cref="AutoStager"/>, and hands the world-space <see cref="StagedScene"/>
    /// to a renderer. Two registration paths:
    ///
    ///  • Blueprint (Microsoft-Layout style): the user taps two known points on
    ///    the ground that match two blueprint points → a 1:1 placement, even on a
    ///    bare slab.
    ///  • Shared anchor: the design is anchor-relative, placed via the session's
    ///    resolved <see cref="AnchorFrame"/> so every device sees it in the same
    ///    spot.
    ///
    /// The store and renderer are injected (their concrete impls are the Supabase
    /// backend and the AR prefab renderer); the orchestration math is tested via
    /// <c>AutoStager</c>.
    /// </summary>
    public sealed class ProjectStagingController : MonoBehaviour
    {
        private IProjectStore _store;
        private IStagedSceneRenderer _renderer;

        /// <summary>Wire up the backend store and the AR renderer (call once on setup).</summary>
        public void Configure(IProjectStore store, IStagedSceneRenderer renderer)
        {
            _store = store;
            _renderer = renderer;
        }

        public StagedScene LastScene { get; private set; }

        /// <summary>
        /// Register a blueprint-authored project on site from two correspondences
        /// (plan point ↔ tapped world point) and stage it.
        /// </summary>
        public async Task StageOnBlueprintAsync(
            string projectId,
            Vector2 planA, Vector2 planB,
            Vector3 worldA, Vector3 worldB,
            CancellationToken ct = default)
        {
            EnsureConfigured();
            var loaded = await _store.LoadAsync(projectId, ct);

            var transform = BlueprintTransform.Solve(
                planA, planB,
                new Vector2(worldA.x, worldA.z),
                new Vector2(worldB.x, worldB.z),
                floorY: worldA.y);

            var scene = AutoStager.StageFromBlueprint(loaded.BaseModel, loaded.Plan, loaded.BlueprintPlacements, transform);
            Present(scene);
        }

        /// <summary>Stage an anchor-relative project against a resolved shared anchor.</summary>
        public async Task StageOnAnchorAsync(string projectId, AnchorFrame frame, CancellationToken ct = default)
        {
            EnsureConfigured();
            var loaded = await _store.LoadAsync(projectId, ct);
            var scene = AutoStager.StageFromAnchor(loaded.BaseModel, loaded.Plan, loaded.AnchorPlacements, frame);
            Present(scene);
        }

        /// <summary>Remove the staged scene.</summary>
        public void Clear()
        {
            LastScene = null;
            _renderer?.Clear();
        }

        private void Present(StagedScene scene)
        {
            LastScene = scene;
            _renderer.Render(scene);
        }

        private void EnsureConfigured()
        {
            if (_store == null || _renderer == null)
                throw new System.InvalidOperationException("ProjectStagingController not configured — call Configure() first.");
        }
    }
}
