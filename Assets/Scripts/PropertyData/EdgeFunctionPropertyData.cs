using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace SuperRealEstate.PropertyData
{
    /// <summary>
    /// <see cref="IPropertyDataProvider"/> backed by Supabase Edge Functions
    /// (see supabase/functions/comps, /parcels, /valuation). RentCast (comps +
    /// valuation) and Regrid (parcels) API keys live only as function secrets;
    /// the Unity client uses the public anon key to reach the functions.
    ///
    /// Mirrors <c>EdgeFunctionSceneAnalyzer</c>: a single POST per request, a
    /// <see cref="TaskCompletionSource{TResult}"/> bridged to UnityWebRequest's
    /// completion, <see cref="CancellationToken"/> aborting the request, and
    /// <see cref="JsonUtility"/> parsing (with the array-wrap trick for top-level
    /// JSON arrays, which JsonUtility cannot deserialize directly).
    ///
    /// Call from the main thread (UnityWebRequest requirement); awaiting is fine.
    /// Returned data is ADVISORY — see <see cref="IPropertyDataProvider"/>.
    /// </summary>
    public sealed class EdgeFunctionPropertyData : IPropertyDataProvider
    {
        private readonly string _baseUrl;
        private readonly string _anonKey;

        public EdgeFunctionPropertyData(string supabaseUrl, string anonKey)
        {
            if (string.IsNullOrEmpty(supabaseUrl)) throw new ArgumentNullException(nameof(supabaseUrl));
            _baseUrl = $"{supabaseUrl.TrimEnd('/')}/functions/v1";
            _anonKey = anonKey;
        }

        public async Task<IReadOnlyList<Comp>> GetCompsAsync(double lat, double lng, CancellationToken ct = default)
        {
            string json = await PostAsync("comps", lat, lng, ct);
            return ParseComps(json);
        }

        public async Task<ParcelInfo> GetParcelAsync(double lat, double lng, CancellationToken ct = default)
        {
            string json = await PostAsync("parcels", lat, lng, ct);
            return ParseParcel(json);
        }

        public async Task<PropertyValuation> GetValuationAsync(double lat, double lng, CancellationToken ct = default)
        {
            string json = await PostAsync("valuation", lat, lng, ct);
            return ParseValuation(json);
        }

        // --- transport ---

        private async Task<string> PostAsync(string function, double lat, double lng, CancellationToken ct)
        {
            string url = $"{_baseUrl}/{function}";
            string payload = JsonUtility.ToJson(new LatLngDto { lat = lat, lng = lng });
            byte[] body = System.Text.Encoding.UTF8.GetBytes(payload);

            using var www = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);
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
                throw new Exception($"{function} request failed: {www.error}");

            return www.downloadHandler.text;
        }

        // --- parsing ---

        private static IReadOnlyList<Comp> ParseComps(string json)
        {
            var result = new List<Comp>();
            if (string.IsNullOrEmpty(json)) return result;

            // The function returns { "comps": [ ... ] }; map snake_case DTOs to models.
            var response = JsonUtility.FromJson<CompsResponse>(json);
            if (response?.comps == null) return result;

            foreach (var dto in response.comps)
            {
                if (dto == null) continue;
                float ppsf = dto.price_per_sqft;
                if (ppsf <= 0f && dto.sqft > 0f) ppsf = dto.price / dto.sqft;

                result.Add(new Comp
                {
                    Address = dto.address,
                    Price = dto.price,
                    Beds = dto.beds,
                    Baths = dto.baths,
                    SqFt = dto.sqft,
                    DistanceMiles = dto.distance_miles,
                    SoldDate = dto.sold_date,
                    PricePerSqFt = ppsf,
                });
            }

            return result;
        }

        private static ParcelInfo ParseParcel(string json)
        {
            var parcel = new ParcelInfo();
            if (string.IsNullOrEmpty(json)) return parcel;

            var dto = JsonUtility.FromJson<ParcelDto>(json);
            if (dto == null) return parcel;

            parcel.Apn = dto.apn;
            parcel.LotSizeAcres = dto.lot_size_acres;
            parcel.Zoning = dto.zoning;

            if (dto.boundary != null)
            {
                foreach (var p in dto.boundary)
                {
                    if (p == null) continue;
                    parcel.Boundary.Add(new Vector2(p.x, p.y));
                }
            }

            return parcel;
        }

        private static PropertyValuation ParseValuation(string json)
        {
            if (string.IsNullOrEmpty(json)) return new PropertyValuation();

            var dto = JsonUtility.FromJson<ValuationDto>(json);
            if (dto == null) return new PropertyValuation();

            return new PropertyValuation
            {
                Estimate = dto.estimate,
                Low = dto.low,
                High = dto.high,
                RentEstimate = dto.rent_estimate,
            };
        }

        // --- DTOs (snake_case to match the Edge Function REST responses) ---

        [Serializable]
        private sealed class LatLngDto
        {
            public double lat;
            public double lng;
        }

        // Wrapper so JsonUtility can read the top-level array (it cannot parse a
        // bare JSON array — the function nests it under "comps").
        [Serializable]
        private sealed class CompsResponse
        {
            public CompDto[] comps;
        }

        [Serializable]
        private sealed class CompDto
        {
            public string address;
            public float price;
            public int beds;
            public float baths;
            public float sqft;
            public float distance_miles;
            public string sold_date;
            public float price_per_sqft;
        }

        [Serializable]
        private sealed class ParcelDto
        {
            public string apn;
            public float lot_size_acres;
            public string zoning;
            public PointDto[] boundary;
        }

        [Serializable]
        private sealed class PointDto
        {
            public float x;
            public float y;
        }

        [Serializable]
        private sealed class ValuationDto
        {
            public float estimate;
            public float low;
            public float high;
            public float rent_estimate;
        }
    }
}
