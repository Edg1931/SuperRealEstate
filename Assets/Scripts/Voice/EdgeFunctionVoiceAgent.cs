using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace SuperRealEstate.Voice
{
    /// <summary>
    /// <see cref="IVoiceAgent"/> backed by the Supabase `voice-agent` Edge
    /// Function (Claude). The device handles speech I/O; this turns the
    /// transcript + context into a spoken reply + an app action. Anthropic key
    /// stays server-side. Call from the main thread (UnityWebRequest).
    /// </summary>
    public sealed class EdgeFunctionVoiceAgent : IVoiceAgent
    {
        private readonly string _functionUrl;
        private readonly string _anonKey;
        private string _accessToken;

        public EdgeFunctionVoiceAgent(string supabaseUrl, string anonKey)
        {
            if (string.IsNullOrEmpty(supabaseUrl)) throw new ArgumentNullException(nameof(supabaseUrl));
            _functionUrl = $"{supabaseUrl.TrimEnd('/')}/functions/v1/voice-agent";
            _anonKey = anonKey;
        }

        /// <summary>Set the signed-in user's token so AI calls are per-user (RLS + rate-limit attribution).</summary>
        public void SetAccessToken(string token) => _accessToken = token;

        public async Task<VoiceResponse> AskAsync(VoiceContext context, CancellationToken ct = default)
        {
            if (context == null || string.IsNullOrEmpty(context.Transcript))
                throw new ArgumentException("transcript required", nameof(context));

            var req = new RequestDto { transcript = context.Transcript };
            if (context.Measurements.HasValue)
            {
                var m = context.Measurements.Value;
                req.measurements = new MeasurementsDto
                {
                    floorAreaSqM = m.FloorAreaSqM, wallAreaSqM = m.WallAreaSqM,
                    perimeterM = m.PerimeterM, ceilingHeightM = m.CeilingHeightM,
                };
            }
            if (context.Latitude.HasValue) req.latitude = (float)context.Latitude.Value;
            if (context.Longitude.HasValue) req.longitude = (float)context.Longitude.Value;
            if (context.FrameImage != null && context.FrameImage.Length > 0)
                req.imageBase64 = Convert.ToBase64String(context.FrameImage);

            byte[] body = System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(req));

            using var www = new UnityWebRequest(_functionUrl, UnityWebRequest.kHttpVerbPOST)
            {
                uploadHandler = new UploadHandlerRaw(body),
                downloadHandler = new DownloadHandlerBuffer(),
            };
            www.SetRequestHeader("Content-Type", "application/json");
            if (!string.IsNullOrEmpty(_anonKey))
            {
                www.SetRequestHeader("Authorization", $"Bearer {(_accessToken ?? _anonKey)}");
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
                throw new Exception($"voice-agent request failed: {www.error}");

            return VoiceResponseParser.Parse(www.downloadHandler.text);
        }

        [Serializable] private sealed class RequestDto {
            public string transcript; public MeasurementsDto measurements;
            public float latitude; public float longitude; public string imageBase64; }
        [Serializable] private sealed class MeasurementsDto {
            public float floorAreaSqM, wallAreaSqM, perimeterM, ceilingHeightM; }
    }
}
