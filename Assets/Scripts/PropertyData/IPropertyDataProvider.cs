using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SuperRealEstate.PropertyData
{
    /// <summary>
    /// Fetches public/third-party property data near a coordinate: comparable
    /// sales, parcel geometry, and an automated valuation.
    ///
    /// The concrete implementation (<see cref="EdgeFunctionPropertyData"/>) calls
    /// RentCast (comps + valuation) and Regrid (parcels) through Supabase Edge
    /// Functions so the provider API keys stay server-side and never ship in the
    /// Unity client — only the public anon key is used to reach the functions.
    ///
    /// Everything returned is ADVISORY: comps, AVM estimates, and GIS-sourced
    /// parcel data are approximations from third parties, not appraisals or
    /// surveys. Callers must frame results accordingly (see VISION.md guardrails).
    /// </summary>
    public interface IPropertyDataProvider
    {
        /// <summary>Comparable recent sales near the given coordinate.</summary>
        Task<IReadOnlyList<Comp>> GetCompsAsync(double lat, double lng, CancellationToken ct = default);

        /// <summary>Parcel record (APN, lot size, zoning, boundary) for the lot at the coordinate.</summary>
        Task<ParcelInfo> GetParcelAsync(double lat, double lng, CancellationToken ct = default);

        /// <summary>Automated valuation (estimate, range, rent) for the property at the coordinate.</summary>
        Task<PropertyValuation> GetValuationAsync(double lat, double lng, CancellationToken ct = default);
    }
}
