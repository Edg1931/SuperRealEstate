using System;
using System.Collections.Generic;

namespace SuperRealEstate.Capture
{
    /// <summary>
    /// Orchestrates one furniture capture: collects photos + their viewing angles,
    /// tracks coverage, exposes live guidance, and transitions through the capture
    /// lifecycle. The AR layer feeds it photos (JPEG bytes) + each shot's view
    /// angle (from the AR camera pose relative to the object) + the AR-measured
    /// metric bounds; when coverage is sufficient it flips to ReadyToProcess and
    /// can build the <see cref="CaptureSubmission"/> for reconstruction. Pure +
    /// unit-tested.
    /// </summary>
    public sealed class CaptureSession
    {
        private readonly List<byte[]> _photos = new List<byte[]>();
        private readonly int _minPhotos;
        private readonly float _minCoverage;

        public string Name { get; set; }
        public CaptureCoverage Coverage { get; }
        public CaptureStatus Status { get; private set; } = CaptureStatus.Idle;
        public CaptureBounds? Bounds { get; private set; }
        public FurnitureCaptureResult Result { get; private set; }
        public string Error { get; private set; }

        public CaptureSession(string name = "Furniture", int azimuthSectors = 12, int elevationBands = 2,
            int minPhotos = 20, float minCoverage = 0.8f)
        {
            Name = name;
            Coverage = new CaptureCoverage(azimuthSectors, elevationBands);
            _minPhotos = minPhotos;
            _minCoverage = minCoverage;
        }

        public int PhotoCount => _photos.Count;
        public float Coverage01 => Coverage.Coverage01;

        /// <summary>Begin capturing (Idle/failed → Capturing).</summary>
        public void Begin()
        {
            _photos.Clear();
            Bounds = null;
            Result = null;
            Error = null;
            Status = CaptureStatus.Capturing;
        }

        /// <summary>
        /// Add a captured frame + its view angle. Accepted only while Capturing.
        /// Auto-advances to ReadyToProcess once coverage + count thresholds clear.
        /// Returns false if not capturing or the frame is empty.
        /// </summary>
        public bool AddPhoto(CaptureView view, byte[] jpeg)
        {
            if (Status != CaptureStatus.Capturing) return false;
            if (jpeg == null || jpeg.Length == 0) return false;

            _photos.Add(jpeg);
            Coverage.Add(view);

            if (Coverage.IsReady(_minPhotos, _minCoverage))
                Status = CaptureStatus.ReadyToProcess;
            return true;
        }

        /// <summary>Set the AR-measured metric bounds (drives true-scale placement + fit).</summary>
        public void SetBounds(CaptureBounds bounds) => Bounds = bounds;

        /// <summary>Coaching line for the next move (empty when ready).</summary>
        public string Guidance(float userHeadingDeg = 0f) => Coverage.NextGuidance(userHeadingDeg);

        /// <summary>Whether there's enough to attempt reconstruction.</summary>
        public bool CanProcess =>
            (Status == CaptureStatus.ReadyToProcess || Status == CaptureStatus.Capturing)
            && Coverage.IsReady(_minPhotos, _minCoverage);

        /// <summary>
        /// Build the submission and move to Processing. Throws if not ready, so the
        /// UI should gate on <see cref="CanProcess"/>.
        /// </summary>
        public CaptureSubmission BuildSubmission(string ownerId, CaptureMethod preferredMethod)
        {
            if (!CanProcess)
                throw new InvalidOperationException("Capture not ready — keep covering angles.");

            Status = CaptureStatus.Processing;
            return new CaptureSubmission
            {
                Name = Name,
                PreferredMethod = preferredMethod,
                Photos = new List<byte[]>(_photos),
                MetricBounds = Bounds,
                OwnerId = ownerId,
            };
        }

        public void MarkReady(FurnitureCaptureResult result)
        {
            Result = result;
            if (result != null && (!Bounds.HasValue || !Bounds.Value.IsValid) && result.Bounds.IsValid)
                Bounds = result.Bounds; // adopt reconstructed scale if AR didn't supply it
            Status = CaptureStatus.Ready;
        }

        public void MarkFailed(string error)
        {
            Error = error;
            Status = CaptureStatus.Failed;
        }

        /// <summary>Discard everything and return to Idle.</summary>
        public void Reset()
        {
            _photos.Clear();
            Bounds = null;
            Result = null;
            Error = null;
            Status = CaptureStatus.Idle;
        }
    }
}
