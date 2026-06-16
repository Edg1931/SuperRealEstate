using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using SuperRealEstate.RoomMeasure;

namespace SuperRealEstate.Insights
{
    /// <summary>
    /// Inputs for a scene analysis pass. Whatever signals the device can provide
    /// are filled in; the analyzer uses what's present. The captured frame is
    /// JPEG/PNG bytes from the headset/phone camera.
    /// </summary>
    public sealed class SceneAnalysisRequest
    {
        /// <summary>Captured camera frame (JPEG/PNG). Optional but key for vision.</summary>
        public byte[] FrameImage;

        /// <summary>Current room measurements, if a room has been captured.</summary>
        public RoomMeasurements? Measurements;

        /// <summary>Device/world pose when the frame was taken (for anchoring insights).</summary>
        public Pose CameraPose;

        /// <summary>Approximate location, for orientation/sun/comp context.</summary>
        public double? Latitude;
        public double? Longitude;
        public float? HeadingDegrees;

        /// <summary>This buyer's stated wishlist (e.g. "home office", "natural light").</summary>
        public IReadOnlyList<string> BuyerPreferences;

        /// <summary>Limit analysis to these categories (null = all enabled).</summary>
        public IReadOnlyList<InsightCategory> CategoryFilter;
    }

    /// <summary>
    /// Turns a captured scene into ready-to-relay insights. The concrete
    /// implementation calls a multimodal model (e.g. Claude) and/or specialist
    /// APIs (plant ID, comps, parcels) through a Supabase Edge Function so keys
    /// stay server-side. Returns advisory items flagged via
    /// <see cref="SceneInsight.IsAdvisory"/> with disclaimers attached.
    /// </summary>
    public interface ISceneAnalyzer
    {
        Task<IReadOnlyList<SceneInsight>> AnalyzeAsync(
            SceneAnalysisRequest request,
            CancellationToken ct = default);
    }
}
