using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using SuperRealEstate.Renovation;

namespace SuperRealEstate.Acquisition
{
    /// <summary>
    /// Imports a building from a CubiCasa floor-plan export into an editable
    /// <see cref="BuildingModel"/>. CubiCasa turns a scan/photo set into a
    /// vector floor plan with real walls and rooms (see COMPETITIVE-LANDSCAPE.md),
    /// so this is a primary editable-import path: we get semantic walls/openings,
    /// not just a baked mesh.
    ///
    /// The CubiCasa API key never touches the client. This calls the Supabase
    /// `cubicasa-import` Edge Function (supabase/functions/cubicasa-import), which
    /// holds the key as a secret, fetches the export, and returns the normalized
    /// BuildingModel JSON that <see cref="BuildingModelParser"/> parses.
    ///
    /// Call from the main thread (UnityWebRequest requirement); awaiting is fine.
    /// </summary>
    public sealed class CubiCasaImporter : IBuildingModelImporter
    {
        private readonly string _functionUrl;
        private readonly string _anonKey;

        public CubiCasaImporter(string supabaseUrl, string anonKey)
        {
            if (string.IsNullOrEmpty(supabaseUrl)) throw new ArgumentNullException(nameof(supabaseUrl));
            _functionUrl = $"{supabaseUrl.TrimEnd('/')}/functions/v1/cubicasa-import";
            _anonKey = anonKey;
        }

        public CaptureSource Source => CaptureSource.CubiCasa;

        public async Task<BuildingModel> ImportAsync(CaptureReference reference, CancellationToken ct = default)
        {
            if (reference == null) throw new ArgumentNullException(nameof(reference));

            // reference.Uri carries the CubiCasa job id (or a direct export URL).
            // The edge function accepts either { jobId } or { exportUrl }.
            string payload = BuildRequestJson(reference.Uri);
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
                throw new Exception($"cubicasa-import request failed: {www.error}");

            return BuildingModelParser.Parse(www.downloadHandler.text);
        }

        private static string BuildRequestJson(string uri)
        {
            var dto = new RequestDto();
            // Heuristic: an http(s) value is a direct export URL; otherwise a job id.
            if (!string.IsNullOrEmpty(uri) &&
                (uri.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                 uri.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
            {
                dto.exportUrl = uri;
            }
            else
            {
                dto.jobId = uri;
            }
            return JsonUtility.ToJson(dto);
        }

        [Serializable]
        private sealed class RequestDto
        {
            public string jobId;
            public string exportUrl;
        }
    }
}
