using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace SuperRealEstate.Staging
{
    /// <summary>
    /// Client for the `stage-director` Edge Function (Claude): sends the room
    /// outline + style brief + stageable items, returns the AI's proposed plan
    /// already parsed AND geometry-validated (only placements that truly fit
    /// the room survive; near-misses are rescued by StagingDirector's nudge).
    /// Anthropic key stays server-side. Call from the main thread
    /// (UnityWebRequest).
    /// </summary>
    public sealed class EdgeFunctionStagingDirector
    {
        private readonly string _functionUrl;
        private readonly string _anonKey;
        private string _accessToken;

        public EdgeFunctionStagingDirector(string supabaseUrl, string anonKey)
        {
            if (string.IsNullOrEmpty(supabaseUrl)) throw new ArgumentNullException(nameof(supabaseUrl));
            _functionUrl = $"{supabaseUrl.TrimEnd('/')}/functions/v1/stage-director";
            _anonKey = anonKey;
        }

        /// <summary>Set the signed-in user's token so AI calls are per-user (rate-limit attribution).</summary>
        public void SetAccessToken(string token) => _accessToken = token;

        /// <summary>
        /// Ask the director to stage the room, then validate every proposal
        /// against the outline. The result's Accepted placements are safe to
        /// render; Rejected carries human-readable reasons for anything dropped.
        /// </summary>
        public async Task<StagingDirector.ValidatedStaging> StageAsync(
            string style,
            IReadOnlyList<Vector3> roomOutline,
            IReadOnlyList<StagingDirector.DirectorItem> items,
            CancellationToken ct = default)
        {
            string body = StagingDirector.BuildRequestJson(style, roomOutline, items);
            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(body);

            using var www = new UnityWebRequest(_functionUrl, UnityWebRequest.kHttpVerbPOST)
            {
                uploadHandler = new UploadHandlerRaw(bytes),
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
                throw new Exception($"stage-director request failed: {www.error}");

            StagingDirector.DirectorPlan plan = StagingDirector.ParsePlan(www.downloadHandler.text);
            return StagingDirector.Validate(roomOutline, plan, items);
        }
    }
}
