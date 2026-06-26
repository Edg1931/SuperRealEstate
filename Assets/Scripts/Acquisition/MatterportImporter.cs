using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using SuperRealEstate.Renovation;

namespace SuperRealEstate.Acquisition
{
    /// <summary>
    /// Imports a building from a Matterport model into an editable
    /// <see cref="BuildingModel"/> — by SEEDING, not by lifting walls.
    ///
    /// Source reality (COMPETITIVE-LANDSCAPE.md): Matterport hands us a baked
    /// single mesh + point cloud and READ-ONLY room dimensions via the Enterprise
    /// Property Intelligence API. It does NOT expose editable, parametric walls.
    /// So instead of importing walls, the `matterport-import` Edge Function reads
    /// each room's dimensional estimate and emits a rectangular
    /// <see cref="RoomDef"/> seeded from those dimensions (floor outline +
    /// ceiling height). The result is an approximate, editable starting point the
    /// agent refines — a scaffold, not a survey. For true editable walls, prefer
    /// <see cref="CubiCasaImporter"/> or RoomPlan.
    ///
    /// The Matterport token never touches the client; the Supabase
    /// `matterport-import` Edge Function (supabase/functions/matterport-import)
    /// holds it as a secret and returns the normalized BuildingModel JSON that
    /// <see cref="BuildingModelParser"/> parses.
    ///
    /// Call from the main thread (UnityWebRequest requirement); awaiting is fine.
    /// </summary>
    public sealed class MatterportImporter : IBuildingModelImporter
    {
        private readonly string _functionUrl;
        private readonly string _anonKey;

        public MatterportImporter(string supabaseUrl, string anonKey)
        {
            if (string.IsNullOrEmpty(supabaseUrl)) throw new ArgumentNullException(nameof(supabaseUrl));
            _functionUrl = $"{supabaseUrl.TrimEnd('/')}/functions/v1/matterport-import";
            _anonKey = anonKey;
        }

        public CaptureSource Source => CaptureSource.Matterport;

        public async Task<BuildingModel> ImportAsync(CaptureReference reference, CancellationToken ct = default)
        {
            if (reference == null) throw new ArgumentNullException(nameof(reference));

            // reference.Uri carries the Matterport model id (e.g. "SxQL3iGyoDo").
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
                throw new Exception($"matterport-import request failed: {www.error}");

            // Rooms come back seeded from dimensions (typically zero walls). The
            // shared parser handles the rest; downstream tooling treats this model
            // as an editable scaffold, not authoritative geometry.
            return BuildingModelParser.Parse(www.downloadHandler.text);
        }

        private static string BuildRequestJson(string modelId)
        {
            var dto = new RequestDto { modelId = modelId };
            return JsonUtility.ToJson(dto);
        }

        [Serializable]
        private sealed class RequestDto
        {
            public string modelId;
        }
    }
}
