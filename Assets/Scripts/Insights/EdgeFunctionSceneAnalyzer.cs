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

        private static List<SceneInsight> Parse(string json)
        {
            var result = new List<SceneInsight>();
            if (string.IsNullOrEmpty(json)) return result;

            var response = JsonUtility.FromJson<InsightsResponse>(json);
            if (response?.insights == null) return result;

            foreach (var dto in response.insights)
            {
                result.Add(new SceneInsight
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
            return result;
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
        private sealed class InsightsResponse
        {
            public InsightDto[] insights;
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
