using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace SuperRealEstate.Insights
{
    /// <summary>
    /// <see cref="IPlantIdentifier"/> backed by the Supabase `plant-id` Edge
    /// Function (see supabase/functions/plant-id). Posts a captured frame and
    /// parses structured <see cref="PlantIdentification"/> results. The provider
    /// key stays server-side; only the public anon key is used here. Call from
    /// the main thread (UnityWebRequest).
    /// </summary>
    public sealed class EdgeFunctionPlantIdentifier : IPlantIdentifier
    {
        private readonly string _functionUrl;
        private readonly string _anonKey;

        public EdgeFunctionPlantIdentifier(string supabaseUrl, string anonKey)
        {
            if (string.IsNullOrEmpty(supabaseUrl)) throw new ArgumentNullException(nameof(supabaseUrl));
            _functionUrl = $"{supabaseUrl.TrimEnd('/')}/functions/v1/plant-id";
            _anonKey = anonKey;
        }

        public async Task<IReadOnlyList<PlantIdentification>> IdentifyAsync(byte[] frameImage, CancellationToken ct = default)
        {
            if (frameImage == null || frameImage.Length == 0) throw new ArgumentException("frameImage required", nameof(frameImage));

            var req = new RequestDto { imageBase64 = Convert.ToBase64String(frameImage), mediaType = "image/jpeg" };
            byte[] body = System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(req));

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
                throw new Exception($"plant-id request failed: {www.error}");

            return Parse(www.downloadHandler.text);
        }

        private static List<PlantIdentification> Parse(string json)
        {
            var result = new List<PlantIdentification>();
            if (string.IsNullOrEmpty(json)) return result;

            var response = JsonUtility.FromJson<PlantsResponse>(json);
            if (response?.plants == null) return result;

            foreach (var p in response.plants)
            {
                result.Add(new PlantIdentification
                {
                    CommonName = p.commonName,
                    ScientificName = p.scientificName,
                    Type = p.type,
                    CareLevel = p.careLevel,
                    Water = p.water,
                    Sun = p.sun,
                    MatureSize = p.matureSize,
                    ToxicToPetsOrKids = p.toxicToPetsOrKids,
                    Invasive = p.invasive,
                    PollenAllergy = p.pollenAllergy,
                    ReplacementCost = p.replacementCost,
                    Confidence = Mathf.Clamp01(p.confidence),
                    Note = p.note,
                });
            }
            return result;
        }

        [Serializable]
        private sealed class RequestDto
        {
            public string imageBase64;
            public string mediaType;
        }

        [Serializable]
        private sealed class PlantsResponse
        {
            public PlantDto[] plants;
        }

        [Serializable]
        private sealed class PlantDto
        {
            public string commonName;
            public string scientificName;
            public string type;
            public string careLevel;
            public string water;
            public string sun;
            public string matureSize;
            public bool toxicToPetsOrKids;
            public bool invasive;
            public string pollenAllergy;
            public float replacementCost;
            public float confidence;
            public string note;
        }
    }
}
