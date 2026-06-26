using System;
using System.Collections.Generic;

namespace SuperRealEstate.PropertyData
{
    /// <summary>
    /// Category of advisory "risk &amp; lifestyle" context shown around a property.
    ///
    /// IMPORTANT: every member is NEUTRAL, sourced public data — flood zone,
    /// wildfire risk, ambient noise, and school ratings. By design this enum models
    /// only objective, publicly-published environmental and educational context. It
    /// deliberately excludes anything that could enable steering (crime,
    /// demographics, "safety", and the like) to stay fair-housing-safe.
    /// </summary>
    public enum RiskKind
    {
        /// <summary>Flood exposure, e.g. a FEMA flood-zone designation.</summary>
        Flood,

        /// <summary>Wildfire exposure, e.g. a published WUI / fire-hazard severity rating.</summary>
        Wildfire,

        /// <summary>Ambient / environmental noise level from public noise-map data.</summary>
        Noise,

        /// <summary>Nearby public-school context (see <see cref="SchoolInfo"/>).</summary>
        School
    }

    /// <summary>
    /// A neutral, advisory severity bucket for a <see cref="RiskLayer"/>. These are
    /// coarse, qualitative bands derived from the cited public source — never a
    /// precise score and never a determination about the property.
    /// </summary>
    public enum RiskRating
    {
        /// <summary>No measurable exposure reported by the source (e.g. outside any flood zone).</summary>
        None,

        /// <summary>Low reported exposure.</summary>
        Low,

        /// <summary>Moderate reported exposure.</summary>
        Moderate,

        /// <summary>High reported exposure.</summary>
        High
    }

    /// <summary>
    /// One advisory environmental-context layer for the subject property (flood,
    /// wildfire, or ambient noise), as derived from a cited public-data source.
    ///
    /// ADVISORY: every layer is NEUTRAL, sourced public data presented as context
    /// only. Ratings are coarse qualitative bands, not a guarantee, appraisal, or
    /// determination about the property. Always advise confirming current
    /// designations with the authoritative source (e.g. FEMA, the local fire
    /// authority, or a published noise map) before relying on them.
    /// </summary>
    [Serializable]
    public sealed class RiskLayer
    {
        /// <summary>Which advisory category this layer describes.</summary>
        public RiskKind Kind;

        /// <summary>Coarse, neutral severity band for this layer.</summary>
        public RiskRating Rating = RiskRating.None;

        /// <summary>
        /// Short human-readable detail straight from the source, e.g.
        /// "FEMA Zone AE" or "65 dB daytime average".
        /// </summary>
        public string Detail;

        /// <summary>
        /// Attribution for the figure, e.g. "FEMA NFHL" or "DOT National Transportation
        /// Noise Map" — kept so the UI can always cite where the context came from.
        /// </summary>
        public string Source;
    }

    /// <summary>
    /// Public-school context near the subject property, as published by a school-data
    /// source.
    ///
    /// ADVISORY: school assignment, boundaries, and ratings change and are sourced
    /// from third-party / public records that may be stale. This is NEUTRAL,
    /// educational context only — present it as a starting point and advise
    /// confirming current enrollment boundaries with the school district.
    /// </summary>
    [Serializable]
    public sealed class SchoolInfo
    {
        /// <summary>School name, for display, e.g. "Lincoln Elementary".</summary>
        public string Name;

        /// <summary>Grade level/band, e.g. "Elementary", "Middle", "High".</summary>
        public string Level;

        /// <summary>Published rating on a 0–10 scale (0 when unavailable).</summary>
        public int Rating;

        /// <summary>Straight-line distance from the subject property, in miles.</summary>
        public float DistanceMiles;
    }

    /// <summary>
    /// The full advisory "risk &amp; lifestyle" context for one property: a set of
    /// environmental layers plus nearby public-school context.
    ///
    /// ADVISORY: this whole profile is NEUTRAL, sourced public data shown as
    /// property context — never a "safety" score, never demographic, and never a
    /// recommendation about a neighbourhood or its residents. It exists to inform,
    /// not to steer; the UI must frame it accordingly and cite each source.
    /// </summary>
    [Serializable]
    public sealed class PropertyRiskProfile
    {
        /// <summary>Advisory environmental layers (flood / wildfire / noise). May be empty.</summary>
        public List<RiskLayer> Layers = new List<RiskLayer>();

        /// <summary>Nearby public-school context. May be empty.</summary>
        public List<SchoolInfo> Schools = new List<SchoolInfo>();
    }
}
