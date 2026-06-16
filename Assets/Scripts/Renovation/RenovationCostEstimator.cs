using System;
using System.Collections.Generic;
using SuperRealEstate.MaterialCost;

namespace SuperRealEstate.Renovation
{
    /// <summary>Editable labor/demolition rates (USD). Tune per market.</summary>
    [Serializable]
    public sealed class DemolitionRates
    {
        public float WallDemoPerSqFt = 3.0f;        // remove a non-load-bearing wall
        public float LoadBearingBeamPerLinFt = 120f; // add header/beam when removing bearing wall
        public float OpeningCutPerEach = 450f;       // cut a new door/passage
    }

    public readonly struct RenovationLineItem
    {
        public readonly string Description;
        public readonly float Subtotal;

        public RenovationLineItem(string description, float subtotal)
        {
            Description = description;
            Subtotal = subtotal;
        }
    }

    /// <summary>
    /// Cost of renovation operations: demolition + new finishes. Pure math.
    /// Finish cost is unit-aware via <see cref="FinishQuantity"/> so paint is
    /// priced by the gallon, flooring by the box, etc., per the material's unit.
    /// Pair totals with comps for renovation ROI.
    /// </summary>
    public static class RenovationCostEstimator
    {
        private const float SqMetersPerSqFoot = 0.092903f;
        private const float MetersPerFoot = 0.3048f;

        /// <summary>Demolish a wall; adds a beam/header line if it's load-bearing.</summary>
        public static IEnumerable<RenovationLineItem> WallRemoval(
            float wallSideAreaSqM, float wallLengthM, bool isLoadBearing, DemolitionRates rates = null)
        {
            rates ??= new DemolitionRates();
            float areaSqFt = wallSideAreaSqM / SqMetersPerSqFoot;
            yield return new RenovationLineItem("Wall demolition", areaSqFt * rates.WallDemoPerSqFt);

            if (isLoadBearing)
            {
                float lenFt = wallLengthM / MetersPerFoot;
                yield return new RenovationLineItem(
                    "Structural header/beam (load-bearing)", lenFt * rates.LoadBearingBeamPerLinFt);
            }
        }

        /// <summary>
        /// Cost to apply a new finish material to a surface of the given metric
        /// area, accounting for the material's unit and waste/coverage.
        /// </summary>
        public static RenovationLineItem FinishChange(
            string description, float areaSqM, Material material, float wasteFactor = 0.10f)
        {
            if (material == null) throw new ArgumentNullException(nameof(material));
            float subtotal = FinishQuantity.FinishCost(areaSqM, material, wasteFactor);
            return new RenovationLineItem(description, subtotal);
        }

        public static float Total(IEnumerable<RenovationLineItem> items)
        {
            if (items == null) throw new ArgumentNullException(nameof(items));
            float total = 0f;
            foreach (var item in items) total += item.Subtotal;
            return total;
        }
    }
}
