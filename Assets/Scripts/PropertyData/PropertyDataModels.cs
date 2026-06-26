using System;
using System.Collections.Generic;
using UnityEngine;

namespace SuperRealEstate.PropertyData
{
    /// <summary>
    /// One comparable sale ("comp") near the subject property, as returned by a
    /// data provider (e.g. RentCast). Used to ground valuation talking points.
    ///
    /// IMPORTANT: comps and anything derived from them are ADVISORY. Prices and
    /// dates come from third-party records that may be stale or incomplete; never
    /// present a comp-derived figure as an appraisal or determination of value.
    /// </summary>
    [Serializable]
    public sealed class Comp
    {
        /// <summary>Street address of the comparable sale, for display/labeling.</summary>
        public string Address;

        /// <summary>Recorded sale price in USD.</summary>
        public float Price;

        /// <summary>Bedroom count.</summary>
        public int Beds;

        /// <summary>Bathroom count (may be fractional, e.g. 2.5).</summary>
        public float Baths;

        /// <summary>Living area in square feet.</summary>
        public float SqFt;

        /// <summary>Straight-line distance from the subject property, in miles.</summary>
        public float DistanceMiles;

        /// <summary>Sale date as an ISO-8601 string (e.g. "2025-11-04"), provider-supplied.</summary>
        public string SoldDate;

        /// <summary>Price per square foot (USD/ft²); 0 when <see cref="SqFt"/> is unknown.</summary>
        public float PricePerSqFt;
    }

    /// <summary>
    /// Parcel (lot) record for the subject property, as returned by a parcel data
    /// provider (e.g. Regrid). Drives the on-the-ground boundary overlay.
    ///
    /// ADVISORY: zoning, lot size, and boundary geometry are sourced from public
    /// GIS records and are approximate — they are not a survey. Always advise
    /// confirming setbacks/boundaries with a licensed surveyor before relying on them.
    /// </summary>
    [Serializable]
    public sealed class ParcelInfo
    {
        /// <summary>Assessor's Parcel Number (APN), the public parcel identifier.</summary>
        public string Apn;

        /// <summary>Lot size in acres.</summary>
        public float LotSizeAcres;

        /// <summary>Zoning code/description as published by the jurisdiction (e.g. "R-1").</summary>
        public string Zoning;

        /// <summary>
        /// Parcel boundary as a closed ring in plan-view meters, relative to the
        /// subject point (origin = the queried lat/lng). Suitable for drawing a
        /// world-anchored overlay on the ground. May be empty when geometry is
        /// unavailable.
        /// </summary>
        public List<Vector2> Boundary = new List<Vector2>();
    }

    /// <summary>
    /// Automated valuation for the subject property, as returned by a valuation
    /// provider (e.g. RentCast's AVM).
    ///
    /// ADVISORY: every field here is an automated estimate with a confidence
    /// range, NOT an appraisal. The UI must frame it as a starting point and
    /// recommend a licensed appraisal / agent CMA before any decision.
    /// </summary>
    [Serializable]
    public sealed class PropertyValuation
    {
        /// <summary>Point estimate of market value in USD.</summary>
        public float Estimate;

        /// <summary>Low end of the estimate's confidence range, in USD.</summary>
        public float Low;

        /// <summary>High end of the estimate's confidence range, in USD.</summary>
        public float High;

        /// <summary>Estimated monthly market rent in USD (0 when unavailable).</summary>
        public float RentEstimate;
    }
}
