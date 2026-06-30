using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace SuperRealEstate.ARCore
{
    /// <summary>
    /// Uploads bytes (capture photos, thumbnails) to Supabase Storage over REST
    /// (<c>{url}/storage/v1/object/{bucket}/{path}</c>). Uses the signed-in user's
    /// token so RLS/storage policies attribute the object to them. Call from the
    /// main thread (UnityWebRequest). The bucket must exist (create a private
    /// <c>furniture</c> bucket in Supabase Storage with owner-scoped policies).
    /// </summary>
    public sealed class SupabaseStorageUploader
    {
        private readonly string _storageUrl;
        private readonly string _anonKey;
        private string _accessToken;

        public SupabaseStorageUploader(string supabaseUrl, string anonKey)
        {
            if (string.IsNullOrEmpty(supabaseUrl)) throw new ArgumentNullException(nameof(supabaseUrl));
            _storageUrl = $"{supabaseUrl.TrimEnd('/')}/storage/v1/object";
            _anonKey = anonKey;
        }

        public void SetAccessToken(string token) => _accessToken = token;

        /// <summary>Upload one object; returns the storage path (bucket/path) on success.</summary>
        public async Task<string> UploadAsync(string bucket, string path, byte[] bytes, string contentType = "image/jpeg", CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(bucket) || string.IsNullOrEmpty(path)) throw new ArgumentException("bucket + path required");
            if (bytes == null || bytes.Length == 0) throw new ArgumentException("empty payload");

            string url = $"{_storageUrl}/{bucket}/{path}";
            using var www = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST)
            {
                uploadHandler = new UploadHandlerRaw(bytes),
                downloadHandler = new DownloadHandlerBuffer(),
            };
            www.SetRequestHeader("Content-Type", contentType);
            www.SetRequestHeader("apikey", _anonKey);
            www.SetRequestHeader("Authorization", $"Bearer {(_accessToken ?? _anonKey)}");
            www.SetRequestHeader("x-upsert", "true");

            var tcs = new TaskCompletionSource<bool>();
            using (ct.Register(() => www.Abort()))
            {
                www.SendWebRequest().completed += _ => tcs.TrySetResult(true);
                await tcs.Task;
            }

            ct.ThrowIfCancellationRequested();
            if (www.result != UnityWebRequest.Result.Success)
                throw new Exception($"storage upload {path} failed: {www.responseCode} {www.error}");
            return $"{bucket}/{path}";
        }

        /// <summary>Upload a set of JPEG photos under a prefix; returns the prefix.</summary>
        public async Task<string> UploadPhotosAsync(string bucket, string prefix, IReadOnlyList<byte[]> photos, CancellationToken ct = default)
        {
            if (photos == null) return prefix;
            for (int i = 0; i < photos.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                if (photos[i] == null || photos[i].Length == 0) continue;
                await UploadAsync(bucket, $"{prefix}/photo_{i:000}.jpg", photos[i], "image/jpeg", ct);
            }
            return prefix;
        }
    }
}
