using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using SuperRealEstate.Capture;

namespace SuperRealEstate.ARCore
{
    /// <summary>
    /// No-backend reconstruction: returns a to-scale result straight from the
    /// AR-measured bounds (a metric box placeholder — no mesh). Lets the capture
    /// loop run end to end in the Editor / offline, and is genuinely useful: the
    /// fit check only needs dimensions, so a correctly-sized box already answers
    /// "will it fit" while the real mesh is produced elsewhere.
    /// </summary>
    public sealed class LocalCaptureService : IFurnitureCaptureService
    {
        public CaptureMethod Method => CaptureMethod.Manual;

        public Task<FurnitureCaptureResult> ReconstructAsync(CaptureSubmission submission, CancellationToken ct = default)
        {
            CaptureBounds bounds = submission.MetricBounds ?? new CaptureBounds(1f, 1f, 1f);
            return Task.FromResult(new FurnitureCaptureResult
            {
                AssetId = Guid.NewGuid().ToString("N"),
                Name = submission.Name,
                Method = CaptureMethod.Manual,
                Bounds = bounds,
                ModelUrl = null, // placeholder box at true scale; real mesh swapped in later
            });
        }
    }

    /// <summary>
    /// Cloud reconstruction: submits the capture's metadata + AR-measured bounds to
    /// the <c>furniture-capture</c> Edge Function, which records the job and creates
    /// a to-scale entry in the user's furniture library immediately (correct
    /// dimensions → fit works now); a reconstruction worker fills in the real mesh
    /// asynchronously. Photos upload to Storage separately (they exceed the function
    /// body cap), so only metadata is sent here. Call from the main thread.
    /// </summary>
    public sealed class EdgeFunctionCaptureService : IFurnitureCaptureService
    {
        private readonly string _functionUrl;
        private readonly string _anonKey;
        private string _accessToken;

        public CaptureMethod Method => CaptureMethod.Photogrammetry;

        public EdgeFunctionCaptureService(string supabaseUrl, string anonKey)
        {
            if (string.IsNullOrEmpty(supabaseUrl)) throw new ArgumentNullException(nameof(supabaseUrl));
            _functionUrl = $"{supabaseUrl.TrimEnd('/')}/functions/v1/furniture-capture";
            _anonKey = anonKey;
        }

        public void SetAccessToken(string token) => _accessToken = token;

        public async Task<FurnitureCaptureResult> ReconstructAsync(CaptureSubmission submission, CancellationToken ct = default)
        {
            CaptureBounds b = submission.MetricBounds ?? new CaptureBounds(1f, 1f, 1f);
            var dto = new RequestDto
            {
                name = submission.Name,
                method = MethodString(submission.PreferredMethod),
                photoCount = submission.Photos?.Count ?? 0,
                storagePrefix = submission.StoragePrefix,
                widthM = b.WidthM, depthM = b.DepthM, heightM = b.HeightM,
            };
            string body = JsonUtility.ToJson(dto);

            using var www = new UnityWebRequest(_functionUrl, UnityWebRequest.kHttpVerbPOST)
            {
                uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body)),
                downloadHandler = new DownloadHandlerBuffer(),
            };
            www.SetRequestHeader("Content-Type", "application/json");
            www.SetRequestHeader("apikey", _anonKey);
            www.SetRequestHeader("Authorization", $"Bearer {(_accessToken ?? _anonKey)}");

            var tcs = new TaskCompletionSource<bool>();
            using (ct.Register(() => www.Abort()))
            {
                www.SendWebRequest().completed += _ => tcs.TrySetResult(true);
                await tcs.Task;
            }

            ct.ThrowIfCancellationRequested();
            if (www.result != UnityWebRequest.Result.Success)
                throw new Exception($"furniture-capture failed: {www.responseCode} {www.error}");

            var res = JsonUtility.FromJson<ResponseDto>(www.downloadHandler.text);
            return new FurnitureCaptureResult
            {
                AssetId = res?.assetId,
                Name = string.IsNullOrEmpty(res?.name) ? submission.Name : res.name,
                Method = submission.PreferredMethod,
                ModelUrl = res?.modelUrl,
                ThumbnailUrl = res?.thumbnailUrl,
                Bounds = res != null && res.widthM > 0f
                    ? new CaptureBounds(res.widthM, res.depthM, res.heightM)
                    : b,
            };
        }

        private static string MethodString(CaptureMethod m) => m switch
        {
            CaptureMethod.ObjectCapture => "object_capture",
            CaptureMethod.Lidar => "lidar",
            CaptureMethod.GaussianSplat => "gaussian_splat",
            CaptureMethod.Manual => "manual",
            _ => "photogrammetry",
        };

        [Serializable] private sealed class RequestDto {
            public string name, method, storagePrefix; public int photoCount; public float widthM, depthM, heightM; }
        [Serializable] private sealed class ResponseDto {
            public string assetId, captureId, name, modelUrl, thumbnailUrl, status; public float widthM, depthM, heightM; }
    }
}
