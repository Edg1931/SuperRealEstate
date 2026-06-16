using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace SuperRealEstate.Insights
{
    /// <summary>
    /// <see cref="ISceneAnalyzer"/> backed by the Supabase `scene-insights`
    /// Edge Function (see supabase/functions/scene-insights). Posts the captured
    /// frame + context and parses the structured <see cref="SceneInsight"/> list
    /// the function returns. The Anthropic key never touches the client — only
    /// the public anon key is used to reach the function.
    ///
    /// Call from the main thread (UnityWebRequest requirement); awaiting is fine.
    /// </summary>
    public sealed class EdgeFunctionSceneAnalyzer : ISceneAnalyzer
    {
        private readonly string _functionUrl;
        private readonly string _anonKey;

        public EdgeFunctionSceneAnalyzer(string supabaseUrl, string anonKey)
        {
            if (string.IsNullOrEmpty(supabaseUrl)) throw new ArgumentNullException(nameof(supabaseUrl));
            _functionUrl = $"{supabaseUrl.TrimEnd('/')}/functions/v1/scene-insights";
            _anonKey = anonKey;
        }

        public async Task<IReadOnlyList<SceneInsight>> AnalyzeAsync(
            SceneAnalysisRequest request,
            CancellationToken ct = default)
            => (await AnalyzeSceneAsync(request, ct)).Insights;

        /// <summary>Full analysis: insight cards + recognized surface finishes.</summary>
        public async Task<SceneAnalysis> AnalyzeSceneAsync(
            SceneAnalysisRequest request,
            CancellationToken ct = default)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            string payload = BuildRequestJson(request);
            byte[] body = System.Text.Encoding.UTF8.GetBytes(payload);

            using var www = new UnityWebRequest(_functionUrl, UnityWebRequest.kHttpVerbPOST);
            www.uploadHandler = new UploadHandlerRaw(body);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");
            if (!string.IsNullOrEmpty(_anonKey))
            {
                www.SetRequestHeader("Authorization", $"Bearer {_anonKey}");
                www.SetRequestHeader("apikey", _anonKey);
            }

            var tcs = new TaskCompletionSource<bool>();
            using (ct.Register(() => www.Abort()))
            {
                www.SendWebRequest().completed += _ => tcs.TrySetResult(true);
                await tcs.Task;
            }

            ct.ThrowIfCancellationRequested();
            if (www.result != UnityWebRequest.Result.Success)
                throw new Exception($"scene-insights request failed: {www.error}");

            return Parse(www.downloadHandler.text);
        }

        // --- request / response (de)serialization ---

        private static string BuildRequestJson(SceneAnalysisRequest request)
        {
            var dto = new RequestDto
            {
                imageBase64 = request.FrameImage != null ? Convert.ToBase64String(request.FrameImage) : null,
                mediaType = "image/jpeg",
                buyerPreferences = request.BuyerPreferences != null
                    ? new List<string>(request.BuyerPreferences).ToArray()
                    : Array.Empty<string>(),
            };

            if (request.Measurements.HasValue)
            {
                var m = request.Measurements.Value;
                dto.measurements = new MeasurementsDto
                {
                    floorAreaSqM = m.FloorAreaSqM,
                    wallAreaSqM = m.WallAreaSqM,
                    perimeterM = m.PerimeterM,
                    ceilingHeightM = m.CeilingHeightM,
                    volumeCubicM = m.VolumeCubicM,
                };
            }

            if (request.Latitude.HasValue) dto.latitude = (float)request.Latitude.Value;
            if (request.Longitude.HasValue) dto.longitude = (float)request.Longitude.Value;
            if (request.HeadingDegrees.HasValue) dto.headingDegrees = request.HeadingDegrees.Value;

            return JsonUtility.ToJson(dto);
        }

        private static SceneAnalysis Parse(string json)
        {
            var insights = new List<SceneInsight>();
            var surfaces = new List<SurfaceFinding>();
            if (string.IsNullOrEmpty(json)) return new SceneAnalysis(insights, surfaces);

            var response = JsonUtility.FromJson<AnalysisResponse>(json);

            if (response?.insights != null)
            {
                foreach (var dto in response.insights)
                {
                    insights.Add(new SceneInsight
                    {
                        Category = ParseEnum(dto.category, InsightCategory.TalkingPoint),
                        Severity = ParseEnum(dto.severity, InsightSeverity.Info),
                        Title = dto.title,
                        Detail = dto.detail,
                        SuggestedTalkingPoint = dto.suggestedTalkingPoint,
                        Confidence = Mathf.Clamp01(dto.confidence),
                        IsAdvisory = dto.isAdvisory,
                        Disclaimer = dto.disclaimer,
                        Source = "scene-insights (claude)",
                    });
                }
            }

            if (response?.surfaces != null)
            {
                foreach (var s in response.surfaces)
                {
                    surfaces.Add(new SurfaceFinding
                    {
                        SurfaceKind = s.surfaceKind,
                        MaterialType = s.materialType,
                        Brand = s.brand,
                        Product = s.product,
                        ColorHex = s.colorHex,
                        EstimatedUnitCost = s.estimatedUnitCost,
                        Unit = s.unit,
                        Confidence = Mathf.Clamp01(s.confidence),
                        IsAdvisory = s.isAdvisory,
                        Disclaimer = s.disclaimer,
                        Note = s.note,
                    });
                }
            }

            return new SceneAnalysis(insights, surfaces);
        }

        private static TEnum ParseEnum<TEnum>(string value, TEnum fallback) where TEnum : struct
        {
            return Enum.TryParse(value, ignoreCase: true, out TEnum parsed) ? parsed : fallback;
        }

        [Serializable]
        private sealed class RequestDto
        {
            public string imageBase64;
            public string mediaType;
            public MeasurementsDto measurements;
            public float latitude;
            public float longitude;
            public float headingDegrees;
            public string[] buyerPreferences;
        }

        [Serializable]
        private sealed class MeasurementsDto
        {
            public float floorAreaSqM;
            public float wallAreaSqM;
            public float perimeterM;
            public float ceilingHeightM;
            public float volumeCubicM;
        }

        [Serializable]
        private sealed class AnalysisResponse
        {
            public InsightDto[] insights;
            public SurfaceDto[] surfaces;
        }

        [Serializable]
        private sealed class SurfaceDto
        {
            public string surfaceKind;
            public string materialType;
            public string brand;
            public string product;
            public string colorHex;
            public float estimatedUnitCost;
            public string unit;
            public float confidence;
            public bool isAdvisory;
            public string disclaimer;
            public string note;
        }

        [Serializable]
        private sealed class InsightDto
        {
            public string category;
            public string severity;
            public string title;
            public string detail;
            public string suggestedTalkingPoint;
            public float confidence;
            public bool isAdvisory;
            public string disclaimer;
        }
    }
}
