using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SuperRealEstate.Capture
{
    /// <summary>How a 3D model of an object was produced.</summary>
    public enum CaptureMethod
    {
        ObjectCapture, // Apple RealityKit PhotogrammetrySession (on-device, iOS/visionOS)
        Photogrammetry, // cloud structure-from-motion (Android / fallback)
        Lidar,          // depth-sensor scan
        GaussianSplat,  // radiance-field / splat capture
        Manual          // imported / placeholder
    }

    /// <summary>Lifecycle of a capture job.</summary>
    public enum CaptureStatus { Idle, Capturing, ReadyToProcess, Processing, Ready, Failed }

    /// <summary>
    /// The real-world bounding box of a captured object — the part most
    /// photogrammetry pipelines throw away, and the part WE need: without metric
    /// scale you can't answer "will this couch fit in that room / through that
    /// door." We recover it from AR depth/anchors at capture time.
    /// </summary>
    public readonly struct CaptureBounds
    {
        public readonly float WidthM;
        public readonly float DepthM;
        public readonly float HeightM;

        public CaptureBounds(float widthM, float depthM, float heightM)
        {
            WidthM = Math.Max(0f, widthM);
            DepthM = Math.Max(0f, depthM);
            HeightM = Math.Max(0f, heightM);
        }

        public float FootprintAreaSqM => WidthM * DepthM;
        public float VolumeCubicM => WidthM * DepthM * HeightM;
        public float LongestEdgeM => Math.Max(WidthM, Math.Max(DepthM, HeightM));
        public bool IsValid => WidthM > 0f && DepthM > 0f && HeightM > 0f;

        /// <summary>
        /// Can this object pass through a rectangular opening (a doorway), allowing
        /// it to be turned/tilted? Standard simplification: orient it to lead with
        /// its smallest face — it fits if its two smallest dimensions are each ≤ the
        /// opening's two dimensions. The marquee "will the movers get it inside" check.
        /// </summary>
        public bool FitsThroughOpening(float openingWidthM, float openingHeightM)
        {
            // Object dims ascending.
            float d1 = WidthM, d2 = DepthM, d3 = HeightM;
            Sort3(ref d1, ref d2, ref d3);
            // Opening dims ascending.
            float o1 = Math.Min(openingWidthM, openingHeightM);
            float o2 = Math.Max(openingWidthM, openingHeightM);
            return d1 <= o1 && d2 <= o2;
        }

        private static void Sort3(ref float a, ref float b, ref float c)
        {
            if (a > b) (a, b) = (b, a);
            if (b > c) (b, c) = (c, b);
            if (a > b) (a, b) = (b, a);
        }
    }

    /// <summary>The result of a successful reconstruction.</summary>
    public sealed class FurnitureCaptureResult
    {
        public string AssetId;
        public string Name;
        public string ModelUrl;        // glTF/USDZ in storage
        public string ThumbnailUrl;
        public CaptureMethod Method;
        public CaptureBounds Bounds;   // metric — feeds the fit check + staging size
    }

    /// <summary>What a capture sends off to be reconstructed into a model.</summary>
    public sealed class CaptureSubmission
    {
        public string Name = "Furniture";
        public CaptureMethod PreferredMethod = CaptureMethod.Photogrammetry;
        public IReadOnlyList<byte[]> Photos = Array.Empty<byte[]>();
        /// <summary>Metric bounds measured from AR at capture time (drives true scale).</summary>
        public CaptureBounds? MetricBounds;
        /// <summary>Optional owner id for storage/RLS attribution.</summary>
        public string OwnerId;
    }

    /// <summary>
    /// The reconstruction seam: turns captured photos (+ AR-measured scale) into a
    /// stored 3D model. Implementations: Apple Object Capture on-device (iOS/visionOS),
    /// or a cloud photogrammetry service via a Supabase Edge Function (Android /
    /// fallback). The capture UX + scale handling above are platform-agnostic;
    /// only this step is platform-specific.
    /// </summary>
    public interface IFurnitureCaptureService
    {
        CaptureMethod Method { get; }
        Task<FurnitureCaptureResult> ReconstructAsync(CaptureSubmission submission, CancellationToken ct = default);
    }
}
