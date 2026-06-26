using System.Collections.Generic;
using UnityEngine;

namespace SuperRealEstate.PropertyData
{
    /// <summary>
    /// Pure, unit-tested statistics over a set of comparable sales. Used to turn
    /// raw comps into a grounded value range for the subject property.
    ///
    /// IMPORTANT: results are ADVISORY. A median-$/ft² estimate is a coarse
    /// heuristic, not an appraisal or CMA; the UI must frame it as a starting
    /// point and recommend a licensed appraisal. All helpers handle empty/zero
    /// input safely (returning 0 / empty ranges) so callers never special-case.
    /// </summary>
    public static class CompStats
    {
        /// <summary>
        /// Median price per square foot (USD/ft²) across the comps. Each comp's
        /// <see cref="Comp.PricePerSqFt"/> is used when present (&gt; 0); otherwise
        /// it is derived from <see cref="Comp.Price"/>/<see cref="Comp.SqFt"/>.
        /// Comps with no usable $/ft² are ignored. Returns 0 when none qualify.
        /// </summary>
        public static float MedianPricePerSqFt(IReadOnlyList<Comp> comps)
        {
            if (comps == null || comps.Count == 0) return 0f;

            var values = new List<float>(comps.Count);
            foreach (var c in comps)
            {
                if (c == null) continue;
                float ppsf = c.PricePerSqFt;
                if (ppsf <= 0f && c.SqFt > 0f) ppsf = c.Price / c.SqFt;
                if (ppsf > 0f) values.Add(ppsf);
            }

            return Median(values);
        }

        /// <summary>Average (mean) sale price in USD across the comps; 0 when empty.</summary>
        public static float AveragePrice(IReadOnlyList<Comp> comps)
        {
            if (comps == null || comps.Count == 0) return 0f;

            float sum = 0f;
            int count = 0;
            foreach (var c in comps)
            {
                if (c == null) continue;
                sum += c.Price;
                count++;
            }

            return count == 0 ? 0f : sum / count;
        }

        /// <summary>
        /// Min/max sale price (USD) across the comps. Returns (0, 0) when empty,
        /// so an unset range is unambiguous and safe to render.
        /// </summary>
        public static (float min, float max) PriceRange(IReadOnlyList<Comp> comps)
        {
            if (comps == null || comps.Count == 0) return (0f, 0f);

            bool any = false;
            float min = float.MaxValue;
            float max = float.MinValue;
            foreach (var c in comps)
            {
                if (c == null) continue;
                min = Mathf.Min(min, c.Price);
                max = Mathf.Max(max, c.Price);
                any = true;
            }

            return any ? (min, max) : (0f, 0f);
        }

        /// <summary>
        /// Advisory value estimate for the subject: median comp $/ft² × the
        /// subject's living area. Returns 0 when there are no usable comps or the
        /// subject area is non-positive. NOT an appraisal — a quick comp-based cue.
        /// </summary>
        public static float EstimateFromComps(float subjectSqFt, IReadOnlyList<Comp> comps)
        {
            if (subjectSqFt <= 0f) return 0f;
            float median = MedianPricePerSqFt(comps);
            return median <= 0f ? 0f : median * subjectSqFt;
        }

        // --- internal ---

        /// <summary>Median of a value list (averages the middle pair when even). 0 when empty.</summary>
        private static float Median(List<float> values)
        {
            if (values == null || values.Count == 0) return 0f;

            values.Sort();
            int mid = values.Count / 2;
            return (values.Count % 2 == 1)
                ? values[mid]
                : (values[mid - 1] + values[mid]) * 0.5f;
        }
    }
}
