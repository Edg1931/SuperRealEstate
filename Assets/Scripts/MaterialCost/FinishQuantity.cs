using System;
using UnityEngine;

namespace SuperRealEstate.MaterialCost
{
    /// <summary>
    /// Material-type-aware quantity + cost. Pricing a renovation "the best way"
    /// means buying paint by the gallon (coats × coverage), flooring by the box
    /// (with waste), and tile by area — not naive area×price for everything.
    /// Pure math, unit-tested. Areas are metric (m²).
    /// </summary>
    public static class FinishQuantity
    {
        public const float SqFtPerSqM = 10.7639f;

        /// <summary>Gallons of paint for an area (rounds up). Default 2 coats, 350 ft²/gal.</summary>
        public static int PaintGallons(float areaSqM, int coats = 2, float coveragePerGallonSqFt = 350f)
        {
            if (coveragePerGallonSqFt <= 0f) throw new ArgumentOutOfRangeException(nameof(coveragePerGallonSqFt));
            float sqft = Mathf.Max(0f, areaSqM) * SqFtPerSqM;
            return Mathf.CeilToInt((sqft * Mathf.Max(1, coats)) / coveragePerGallonSqFt);
        }

        /// <summary>Cost to paint an area at a per-gallon price (e.g. a named Sherwin-Williams color).</summary>
        public static float PaintCost(float areaSqM, float pricePerGallon, int coats = 2, float coveragePerGallonSqFt = 350f)
            => PaintGallons(areaSqM, coats, coveragePerGallonSqFt) * pricePerGallon;

        /// <summary>Boxes of flooring for an area including waste (rounds up).</summary>
        public static int FlooringBoxes(float areaSqM, float sqFtPerBox, float wasteFactor = 0.10f)
        {
            if (sqFtPerBox <= 0f) throw new ArgumentOutOfRangeException(nameof(sqFtPerBox));
            float sqft = Mathf.Max(0f, areaSqM) * SqFtPerSqM * (1f + Mathf.Max(0f, wasteFactor));
            return Mathf.CeilToInt(sqft / sqFtPerBox);
        }

        /// <summary>
        /// Area-based finish cost for a catalog <see cref="Material"/>, honoring
        /// its unit (per m² vs per ft²) and a waste factor.
        /// </summary>
        public static float FinishCost(float areaSqM, Material material, float wasteFactor = 0.10f)
        {
            if (material == null) throw new ArgumentNullException(nameof(material));

            // Paint is bought by the gallon — coverage handles "waste", so price
            // by gallons, not area.
            if (material.Unit == MaterialUnit.PerGallon)
                return PaintCost(areaSqM, material.PricePerUnit);

            float waste = 1f + Mathf.Max(0f, wasteFactor);
            float qty = material.Unit switch
            {
                MaterialUnit.PerSquareMeter => areaSqM * waste,
                MaterialUnit.PerSquareFoot => areaSqM * SqFtPerSqM * waste,
                _ => areaSqM * SqFtPerSqM * waste, // sensible default for area finishes
            };
            return qty * material.PricePerUnit;
        }
    }
}
