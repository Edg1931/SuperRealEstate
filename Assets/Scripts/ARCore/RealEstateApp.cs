using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using SuperRealEstate.ARRender;
using SuperRealEstate.Projects;
using SuperRealEstate.ProjectsBackend;
using SuperRealEstate.Services;

namespace SuperRealEstate.ARCore
{
    /// <summary>
    /// The app composition root (milestone M1, see docs/SHIPPING.md). Put one on
    /// a root object in the AR scene, set the Supabase config, and drag in the
    /// scene's controllers/renderers. On startup it builds the backend clients
    /// and wires them together — turning all the tested engine + scaffolds into a
    /// running app: open a saved project and stage it on site.
    ///
    /// What you still assemble in the Editor: the XR rig (XR Origin + AR Session +
    /// AR Plane Manager) and the per-platform input source feeding
    /// <see cref="SpatialPointerInput"/> (Android XR eye-gaze + pinch / visionOS
    /// pointer + tap / phone camera + touch). See docs/AndroidXR-Setup.md and
    /// docs/VisionOS-Setup.md.
    /// </summary>
    public sealed class RealEstateApp : MonoBehaviour
    {
        [Header("Backend (Supabase)")]
        [SerializeField] private string supabaseUrl;
        [SerializeField] private string supabaseAnonKey;

        [Header("Scene references")]
        [SerializeField] private ProjectStagingController stagingController;
        [SerializeField] private StagedSceneRenderer stagedSceneRenderer;

        /// <summary>Catalog reads + room/estimate writes.</summary>
        public SupabaseBackendClient Backend { get; private set; }

        /// <summary>Loads a saved project's model/plan/design.</summary>
        public IProjectStore ProjectStore { get; private set; }

        public bool IsConfigured => ProjectStore != null && stagingController != null && stagedSceneRenderer != null;

        private void Awake()
        {
            if (!string.IsNullOrEmpty(supabaseUrl))
            {
                Backend = new SupabaseBackendClient(supabaseUrl, supabaseAnonKey);
                ProjectStore = new SupabaseProjectStore(supabaseUrl, supabaseAnonKey);
            }

            if (stagingController != null && ProjectStore != null && stagedSceneRenderer != null)
                stagingController.Configure(ProjectStore, stagedSceneRenderer);
        }

        /// <summary>After the user signs in, propagate the token so writes pass RLS.</summary>
        public void SetAccessToken(string token) => Backend?.SetAccessToken(token);

        /// <summary>
        /// Stage a saved project on the real site from two ground correspondences
        /// (the Microsoft-Layout-style blueprint walk). Tap two known points whose
        /// plan coordinates you know; the design snaps in at 1:1.
        /// </summary>
        public Task StageProjectOnBlueprintAsync(
            string projectId, Vector2 planA, Vector2 planB, Vector3 worldA, Vector3 worldB,
            CancellationToken ct = default)
        {
            if (!IsConfigured)
                throw new System.InvalidOperationException("RealEstateApp not configured — set Supabase config + scene references.");
            return stagingController.StageOnBlueprintAsync(projectId, planA, planB, worldA, worldB, ct);
        }

        /// <summary>Clear the staged scene.</summary>
        public void ClearStaging() => stagingController?.Clear();
    }
}
