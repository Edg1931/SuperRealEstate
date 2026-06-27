using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using SuperRealEstate.ARRender;
using SuperRealEstate.CollaborationBackend;
using SuperRealEstate.Insights;
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

        [Header("Voice / tool command handling (optional)")]
        [Tooltip("Scene-layer IAppActions: measure, plant ID, finish recognition, estimates.")]
        [SerializeField] private SceneAppActions sceneActions;
        [Tooltip("Supplies JPEG camera frames for plant / finish recognition.")]
        [SerializeField] private ArCameraFrameProvider cameraFrameProvider;
        [Tooltip("Renders a removed wall as a portal into the pre-scanned space.")]
        [SerializeField] private WallPortalRenderer wallPortalRenderer;

        [Header("Shared sessions (optional)")]
        [Tooltip("Polls + broadcasts shared-session edits to every participant's device.")]
        [SerializeField] private SharedSessionSync sharedSessionSync;

        /// <summary>Catalog reads + room/estimate writes.</summary>
        public SupabaseBackendClient Backend { get; private set; }

        /// <summary>Loads a saved project's model/plan/design.</summary>
        public IProjectStore ProjectStore { get; private set; }

        /// <summary>Shared multi-device session backbone (create/join/sync).</summary>
        public SupabaseSharedSessionService Sessions { get; private set; }

        /// <summary>Optional Realtime channel for low-latency placements + presence.</summary>
        public SupabaseRealtimeChannel RealtimeChannel { get; private set; }

        public bool IsConfigured => ProjectStore != null && stagingController != null && stagedSceneRenderer != null;

        private void Awake()
        {
            if (!string.IsNullOrEmpty(supabaseUrl))
            {
                Backend = new SupabaseBackendClient(supabaseUrl, supabaseAnonKey);
                ProjectStore = new SupabaseProjectStore(supabaseUrl, supabaseAnonKey);
                Sessions = new SupabaseSharedSessionService(supabaseUrl, supabaseAnonKey);
                RealtimeChannel = new SupabaseRealtimeChannel(supabaseUrl, supabaseAnonKey);
            }

            if (stagingController != null && ProjectStore != null && stagedSceneRenderer != null)
                stagingController.Configure(ProjectStore, stagedSceneRenderer);

            if (sharedSessionSync != null && Sessions != null)
            {
                sharedSessionSync.Configure(Sessions);
                if (RealtimeChannel != null) sharedSessionSync.ConfigureRealtime(RealtimeChannel);
            }

            ConfigureSceneActions();
        }

        /// <summary>
        /// Wire the voice/tool command handler to the cloud analysis services and
        /// the AR camera. Plant ID and finish recognition need the Supabase config
        /// (for the Edge Functions) and a frame source; when either is missing the
        /// corresponding action degrades to a friendly "not available" message.
        /// </summary>
        private void ConfigureSceneActions()
        {
            if (sceneActions == null) return;

            EdgeFunctionSceneAnalyzer analyzer = null;
            EdgeFunctionPlantIdentifier plantId = null;
            if (!string.IsNullOrEmpty(supabaseUrl))
            {
                analyzer = new EdgeFunctionSceneAnalyzer(supabaseUrl, supabaseAnonKey);
                plantId = new EdgeFunctionPlantIdentifier(supabaseUrl, supabaseAnonKey);
            }

            System.Func<byte[]> frameProvider =
                cameraFrameProvider != null ? cameraFrameProvider.CaptureJpeg : (System.Func<byte[]>)null;

            sceneActions.Configure(analyzer, plantId, frameProvider);

            if (wallPortalRenderer != null)
                sceneActions.ConfigureRenovation(wallPortalRenderer);
        }

        /// <summary>After the user signs in, propagate the token so writes pass RLS.</summary>
        public void SetAccessToken(string token)
        {
            Backend?.SetAccessToken(token);
            Sessions?.SetAccessToken(token);
            RealtimeChannel?.SetAccessToken(token);
        }

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
