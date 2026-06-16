using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SuperRealEstate.Insights
{
    /// <summary>
    /// An identified plant / tree / flower (walk up to it and the app names it).
    /// Beyond the name, it carries the things a buyer/agent actually cares about:
    /// care effort, water/sun needs, mature size, toxicity to kids/pets, invasive
    /// status, allergy/pollen, and rough replacement cost. Advisory.
    /// </summary>
    [Serializable]
    public sealed class PlantIdentification
    {
        public string CommonName;
        public string ScientificName;
        public string Type;            // tree | shrub | flower | grass | groundcover | succulent
        public string CareLevel;       // easy | moderate | high
        public string Water;           // low | medium | high
        public string Sun;             // full | partial | shade
        public string MatureSize;      // e.g. "15–25 ft"
        public bool ToxicToPetsOrKids;
        public bool Invasive;
        public string PollenAllergy;    // none | low | moderate | high
        public float ReplacementCost;
        public float Confidence;        // 0..1
        public bool IsAdvisory = true;
        public string Note;
    }

    /// <summary>
    /// Identifies vegetation from a captured frame. The concrete implementation
    /// calls a specialist API (Pl@ntNet / Plant.id) through a Supabase Edge
    /// Function so the key stays server-side — same pattern as scene-insights.
    /// </summary>
    public interface IPlantIdentifier
    {
        Task<IReadOnlyList<PlantIdentification>> IdentifyAsync(byte[] frameImage, CancellationToken ct = default);
    }
}
