using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SuperRealEstate.MaterialCost;
using SuperRealEstate.Staging;

namespace SuperRealEstate.Services
{
    /// <summary>
    /// Records that mirror the Supabase schema (see supabase/migrations).
    /// Kept as plain serializable types so they can be (de)serialized from the
    /// REST API and reused by the UI and tests.
    /// </summary>
    [Serializable]
    public sealed class RoomRecord
    {
        public string Id;
        public string PropertyId;
        public string Name;
        public float FloorAreaSqM;
        public float WallAreaSqM;
        public float PerimeterM;
        public float CeilingHeightM;
        public float VolumeCubicM;
    }

    [Serializable]
    public sealed class EstimateItemRecord
    {
        public string RoomId;
        public SurfaceType Surface;
        public string MaterialId;
        public float Quantity;
        public float WasteFactor;
        public float Subtotal;
    }

    /// <summary>
    /// Backend boundary for the app. The concrete implementation talks to
    /// Supabase (PostgREST + Auth) over HTTPS; third-party APIs (Pl@ntNet,
    /// RentCast, Regrid) are reached through Supabase Edge Functions so their
    /// keys never ship in the client build.
    ///
    /// Implementation is deferred to Phase 2 — this interface defines the seam
    /// so the rest of the app can be built and tested against it.
    /// </summary>
    public interface IBackendClient
    {
        Task<IReadOnlyList<Material>> GetMaterialCatalogAsync(CancellationToken ct = default);

        /// <summary>Browse the shared vendor marketplace.</summary>
        Task<IReadOnlyList<Vendor>> GetVendorsAsync(CancellationToken ct = default);

        /// <summary>Fetch a vendor's importable catalog items (3D assets + price).</summary>
        Task<IReadOnlyList<CatalogItem>> GetVendorCatalogAsync(string vendorId, CancellationToken ct = default);

        Task<string> SaveRoomAsync(RoomRecord room, CancellationToken ct = default);

        Task SaveEstimateItemsAsync(
            string roomId,
            IReadOnlyList<EstimateItemRecord> items,
            CancellationToken ct = default);
    }
}
