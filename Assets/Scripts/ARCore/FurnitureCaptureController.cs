using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;
using SuperRealEstate.Capture;

namespace SuperRealEstate.ARCore
{
    /// <summary>
    /// In-app furniture capture (phone AR): the user places a box around a piece of
    /// their furniture, orbits it taking photos, and we reconstruct a to-scale 3D
    /// model. This MonoBehaviour drives the tested <see cref="CaptureSession"/> from
    /// the AR camera — each shot's JPEG (via <see cref="ArCameraFrameProvider"/>)
    /// and view angle (from the camera pose vs the object center) — surfaces the
    /// live coverage coach, and on "ready" hands the photos + AR-measured metric
    /// bounds to an <see cref="IFurnitureCaptureService"/> (Apple Object Capture
    /// on-device, or a cloud service) to build the model.
    ///
    /// Setup: place the object center (tap a reticle on the floor at the furniture)
    /// and size the bounds; then Begin and orbit. Auto-capture fires a shot each
    /// time the user rotates into a new sector, so they just walk around it.
    /// </summary>
    public sealed class FurnitureCaptureController : MonoBehaviour
    {
        [SerializeField] private ArCameraFrameProvider frameProvider;
        [Tooltip("AR camera transform (defaults to Camera.main).")]
        [SerializeField] private Transform arCamera;

        [Header("Capture")]
        [SerializeField] private string captureName = "Client furniture";
        [Tooltip("Auto-snap a photo when the user orbits into a new angle.")]
        [SerializeField] private bool autoCapture = true;
        [Tooltip("Degrees of orbit between auto-snaps.")]
        [SerializeField] private float autoCaptureStepDeg = 15f;

        [Serializable] public sealed class FloatEvent : UnityEvent<float> { }
        [Serializable] public sealed class StringEvent : UnityEvent<string> { }
        [Serializable] public sealed class ResultEvent : UnityEvent<FurnitureCaptureResult> { }

        [Tooltip("Coverage 0..1 — drive a capture progress ring.")]
        public FloatEvent OnCoverage = new FloatEvent();
        [Tooltip("Next-move coaching text (empty when ready).")]
        public StringEvent OnGuidance = new StringEvent();
        [Tooltip("Status / errors for the HUD.")]
        public StringEvent OnInfo = new StringEvent();
        [Tooltip("Raised when reconstruction finishes with the stored asset.")]
        public ResultEvent OnCaptured = new ResultEvent();

        private CaptureSession _session;
        private IFurnitureCaptureService _service;
        private SupabaseStorageUploader _uploader;
        private string _bucket = "furniture";
        private Vector3 _objectCenter;
        private bool _hasCenter;
        private float _lastShotAzimuth;
        private bool _hasLastShot;

        /// <summary>The user/account id used for storage attribution (set after sign-in).</summary>
        public string OwnerId { get; set; }

        private void Awake()
        {
            if (arCamera == null && Camera.main != null) arCamera = Camera.main.transform;
            _session = new CaptureSession(captureName);
        }

        /// <summary>Inject the reconstruction backend (Apple Object Capture / cloud / local).</summary>
        public void Configure(IFurnitureCaptureService service) => _service = service;

        /// <summary>Inject a storage uploader so photos are uploaded for the reconstruction worker.</summary>
        public void SetStorage(SupabaseStorageUploader uploader, string bucket = "furniture")
        {
            _uploader = uploader;
            if (!string.IsNullOrEmpty(bucket)) _bucket = bucket;
        }

        /// <summary>Place the object center (e.g. a reticle hit on the floor at the furniture).</summary>
        public void SetObjectCenter(Vector3 worldCenter) { _objectCenter = worldCenter; _hasCenter = true; }

        /// <summary>Set the AR-measured bounding box (meters) — drives true scale + fit.</summary>
        public void SetBounds(float widthM, float depthM, float heightM)
            => _session.SetBounds(new CaptureBounds(widthM, depthM, heightM));

        /// <summary>Start a capture (clears any prior).</summary>
        public void Begin()
        {
            if (!_hasCenter) { OnInfo.Invoke("place the box around the furniture first"); return; }
            _session.Name = captureName;
            _session.Begin();
            _hasLastShot = false;
            Emit();
            OnInfo.Invoke("Walk around the furniture, keeping it centered.");
        }

        /// <summary>Snap one photo at the current pose.</summary>
        public void CapturePhoto()
        {
            if (_session.Status != CaptureStatus.Capturing) return;
            if (!_hasCenter || arCamera == null || frameProvider == null) return;

            byte[] jpeg = frameProvider.CaptureJpeg();
            if (jpeg == null || jpeg.Length == 0) { OnInfo.Invoke("hold steady — couldn't grab a frame"); return; }

            Vector3 p = arCamera.position;
            CaptureView view = CaptureMath.ViewFrom(_objectCenter.x, _objectCenter.y, _objectCenter.z, p.x, p.y, p.z);
            if (_session.AddPhoto(view, jpeg))
            {
                _lastShotAzimuth = view.AzimuthDeg;
                _hasLastShot = true;
                Emit();
            }
        }

        private void Update()
        {
            if (!autoCapture || _session.Status != CaptureStatus.Capturing || !_hasCenter || arCamera == null) return;

            Vector3 p = arCamera.position;
            CaptureView view = CaptureMath.ViewFrom(_objectCenter.x, _objectCenter.y, _objectCenter.z, p.x, p.y, p.z);
            if (!_hasLastShot || AngularDelta(view.AzimuthDeg, _lastShotAzimuth) >= autoCaptureStepDeg)
                CapturePhoto();
        }

        /// <summary>Send the captured set off for reconstruction. Call when coverage is ready.</summary>
        public async Task FinalizeAsync(CaptureMethod preferredMethod = CaptureMethod.Photogrammetry, CancellationToken ct = default)
        {
            if (_service == null) { OnInfo.Invoke("capture backend isn't set up"); return; }
            if (!_session.CanProcess) { OnInfo.Invoke("keep covering angles before finishing"); return; }

            OnInfo.Invoke("Building your 3D model…");
            try
            {
                CaptureSubmission submission = _session.BuildSubmission(OwnerId, preferredMethod);

                // Upload the photos so the reconstruction worker can fetch them.
                if (_uploader != null && submission.Photos.Count > 0)
                {
                    string prefix = $"{(string.IsNullOrEmpty(OwnerId) ? "anon" : OwnerId)}/{Guid.NewGuid():N}";
                    await _uploader.UploadPhotosAsync(_bucket, prefix, submission.Photos, ct);
                    submission.StoragePrefix = prefix;
                    OnInfo.Invoke("Photos uploaded — building your model…");
                }

                FurnitureCaptureResult result = await _service.ReconstructAsync(submission, ct);
                _session.MarkReady(result);
                OnCaptured.Invoke(result);
                OnInfo.Invoke(result != null ? $"Saved \"{result.Name ?? captureName}\" to your library." : "Capture finished.");
            }
            catch (OperationCanceledException) { _session.MarkFailed("cancelled"); }
            catch (Exception e)
            {
                _session.MarkFailed(e.Message);
                OnInfo.Invoke("couldn't build the model");
                Debug.LogWarning($"[FurnitureCapture] reconstruction failed: {e.Message}");
            }
        }

        /// <summary>Fire-and-forget wrapper for a UI button.</summary>
        public void Finalize() => _ = FinalizeAsync();

        private void Emit()
        {
            OnCoverage.Invoke(_session.Coverage01);
            float heading = arCamera != null ? arCamera.eulerAngles.y : 0f;
            OnGuidance.Invoke(_session.Guidance(heading));
        }

        private static float AngularDelta(float a, float b)
        {
            float d = Mathf.Abs(Mathf.DeltaAngle(a, b));
            return d;
        }
    }
}
