using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using SuperRealEstate.MaterialCost;
using SuperRealEstate.Staging;

namespace SuperRealEstate.Services
{
    /// <summary>
    /// Supabase-backed <see cref="IBackendClient"/> over PostgREST
    /// (<c>{url}/rest/v1/...</c>). Reads the shared catalog and persists rooms +
    /// estimates the web companion (on Vercel) then displays. Pass a user access
    /// token once signed in so row-level security applies; the anon key alone
    /// covers public catalog reads. Call from the main thread (UnityWebRequest).
    /// </summary>
    public sealed class SupabaseBackendClient : IBackendClient
    {
        private readonly string _restUrl;
        private readonly string _anonKey;
        private string _accessToken;

        public SupabaseBackendClient(string supabaseUrl, string anonKey, string accessToken = null)
        {
            if (string.IsNullOrEmpty(supabaseUrl)) throw new ArgumentNullException(nameof(supabaseUrl));
            _restUrl = $"{supabaseUrl.TrimEnd('/')}/rest/v1";
            _anonKey = anonKey;
            _accessToken = accessToken;
        }

        /// <summary>Set after the user signs in so writes pass RLS.</summary>
        public void SetAccessToken(string token) => _accessToken = token;

        public async Task<IReadOnlyList<Material>> GetMaterialCatalogAsync(CancellationToken ct = default)
        {
            string json = await Get("/materials?select=id,name,category,unit,price_per_unit,brand,product_code,color_hex,buy_url", ct);
            var rows = JsonUtility.FromJson<MaterialRowList>(Wrap(json));
            var list = new List<Material>();
            if (rows?.items != null)
                foreach (var r in rows.items)
                    list.Add(new Material
                    {
                        Id = r.id, Name = r.name, Category = r.category,
                        Unit = ParseUnit(r.unit), PricePerUnit = r.price_per_unit,
                        Brand = r.brand, ProductCode = r.product_code,
                        ColorHex = r.color_hex, BuyUrl = r.buy_url,
                    });
            return list;
        }

        public async Task<IReadOnlyList<Vendor>> GetVendorsAsync(CancellationToken ct = default)
        {
            string json = await Get("/vendors?select=id,name,website,logo_url", ct);
            var rows = JsonUtility.FromJson<VendorRowList>(Wrap(json));
            var list = new List<Vendor>();
            if (rows?.items != null)
                foreach (var r in rows.items)
                    list.Add(new Vendor { Id = r.id, Name = r.name, Website = r.website, LogoUrl = r.logo_url });
            return list;
        }

        public async Task<IReadOnlyList<CatalogItem>> GetVendorCatalogAsync(string vendorId, CancellationToken ct = default)
        {
            string q = $"/vendor_catalog_items?vendor_id=eq.{UnityWebRequest.EscapeURL(vendorId)}" +
                       "&select=id,vendor_id,sku,name,category,width_m,depth_m,height_m,model_url,thumbnail_url,price,currency";
            string json = await Get(q, ct);
            var rows = JsonUtility.FromJson<CatalogRowList>(Wrap(json));
            var list = new List<CatalogItem>();
            if (rows?.items != null)
                foreach (var r in rows.items)
                    list.Add(new CatalogItem
                    {
                        Id = r.id, VendorId = r.vendor_id, Sku = r.sku, Name = r.name, Category = r.category,
                        Size = new Vector3(r.width_m, r.height_m, r.depth_m),
                        ModelUrl = r.model_url, ThumbnailUrl = r.thumbnail_url, Price = r.price, Currency = r.currency,
                    });
            return list;
        }

        public async Task<string> SaveRoomAsync(RoomRecord room, CancellationToken ct = default)
        {
            if (room == null) throw new ArgumentNullException(nameof(room));
            var insert = new RoomInsert
            {
                property_id = room.PropertyId, name = room.Name,
                floor_area_sqm = room.FloorAreaSqM, wall_area_sqm = room.WallAreaSqM,
                perimeter_m = room.PerimeterM, ceiling_height_m = room.CeilingHeightM,
                volume_cubic_m = room.VolumeCubicM,
            };
            string json = await Post("/rooms", JsonUtility.ToJson(insert), returnRepresentation: true, ct);
            var rows = JsonUtility.FromJson<RoomIdList>(Wrap(json));
            return rows?.items != null && rows.items.Length > 0 ? rows.items[0].id : null;
        }

        public async Task SaveEstimateItemsAsync(string roomId, IReadOnlyList<EstimateItemRecord> items, CancellationToken ct = default)
        {
            if (items == null || items.Count == 0) return;
            var sb = new StringBuilder("[");
            for (int i = 0; i < items.Count; i++)
            {
                var it = items[i];
                var ins = new EstimateInsert
                {
                    room_id = roomId,
                    surface = it.Surface.ToString().ToLowerInvariant(),
                    material_id = it.MaterialId,
                    quantity = it.Quantity, waste_factor = it.WasteFactor, subtotal = it.Subtotal,
                };
                if (i > 0) sb.Append(',');
                sb.Append(JsonUtility.ToJson(ins));
            }
            sb.Append(']');
            await Post("/room_estimate_items", sb.ToString(), returnRepresentation: false, ct);
        }

        // --- HTTP helpers ---

        private Task<string> Get(string pathAndQuery, CancellationToken ct)
            => Send(UnityWebRequest.kHttpVerbGET, pathAndQuery, null, false, ct);

        private Task<string> Post(string path, string body, bool returnRepresentation, CancellationToken ct)
            => Send(UnityWebRequest.kHttpVerbPOST, path, body, returnRepresentation, ct);

        private async Task<string> Send(string verb, string pathAndQuery, string body, bool returnRepresentation, CancellationToken ct)
        {
            using var www = new UnityWebRequest(_restUrl + pathAndQuery, verb)
            {
                downloadHandler = new DownloadHandlerBuffer(),
            };
            if (body != null)
            {
                www.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
                www.SetRequestHeader("Content-Type", "application/json");
            }
            www.SetRequestHeader("apikey", _anonKey);
            www.SetRequestHeader("Authorization", $"Bearer {(_accessToken ?? _anonKey)}");
            if (returnRepresentation) www.SetRequestHeader("Prefer", "return=representation");

            var tcs = new TaskCompletionSource<bool>();
            using (ct.Register(() => www.Abort()))
            {
                www.SendWebRequest().completed += _ => tcs.TrySetResult(true);
                await tcs.Task;
            }

            ct.ThrowIfCancellationRequested();
            if (www.result != UnityWebRequest.Result.Success)
                throw new Exception($"Supabase {verb} {pathAndQuery} failed: {www.responseCode} {www.error}");
            return www.downloadHandler.text;
        }

        // JsonUtility can't parse a top-level array — wrap it as an object.
        private static string Wrap(string jsonArray)
            => "{\"items\":" + (string.IsNullOrEmpty(jsonArray) ? "[]" : jsonArray) + "}";

        private static MaterialUnit ParseUnit(string u) => u switch
        {
            "per_sqft" => MaterialUnit.PerSquareFoot,
            "per_sqm" => MaterialUnit.PerSquareMeter,
            "per_linft" => MaterialUnit.PerLinearFoot,
            "per_gallon" => MaterialUnit.PerGallon,
            _ => MaterialUnit.Each,
        };

        // --- row DTOs (snake_case to match PostgREST) ---
        [Serializable] private sealed class MaterialRowList { public MaterialRow[] items; }
        [Serializable] private sealed class MaterialRow {
            public string id, name, category, unit, brand, product_code, color_hex, buy_url; public float price_per_unit; }
        [Serializable] private sealed class VendorRowList { public VendorRow[] items; }
        [Serializable] private sealed class VendorRow { public string id, name, website, logo_url; }
        [Serializable] private sealed class CatalogRowList { public CatalogRow[] items; }
        [Serializable] private sealed class CatalogRow {
            public string id, vendor_id, sku, name, category, model_url, thumbnail_url, currency;
            public float width_m, depth_m, height_m, price; }
        [Serializable] private sealed class RoomIdList { public RoomId[] items; }
        [Serializable] private sealed class RoomId { public string id; }

        // --- insert DTOs ---
        [Serializable] private sealed class RoomInsert {
            public string property_id, name; public float floor_area_sqm, wall_area_sqm, perimeter_m, ceiling_height_m, volume_cubic_m; }
        [Serializable] private sealed class EstimateInsert {
            public string room_id, surface, material_id; public float quantity, waste_factor, subtotal; }
    }
}
