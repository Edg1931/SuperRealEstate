using System.Collections.Generic;
using UnityEngine;
using SuperRealEstate.PropertyData;

namespace SuperRealEstate.Overlays
{
    /// <summary>
    /// Turns comparable sales into concise content tags the AR layer floats over
    /// neighbouring homes (e.g. "$525,000 · $312/ft²"). Pure + testable.
    ///
    /// ADVISORY: comps are third-party records that may be stale or incomplete.
    /// These tags are talking points, never an appraisal or determination of value.
    /// </summary>
    public static class CompsOverlayBuilder
    {
        /// <summary>
        /// Builds one <see cref="OverlayTag"/> per comp. The $/ft² clause is omitted
        /// when square footage is missing or zero (divide-by-zero safe). Never
        /// returns null; null comps are skipped.
        /// </summary>
        public static List<OverlayTag> Build(IReadOnlyList<Comp> comps)
        {
            var tags = new List<OverlayTag>();
            if (comps == null) return tags;

            foreach (var comp in comps)
            {
                if (comp == null) continue;
                tags.Add(new OverlayTag
                {
                    Text = Format(comp),
                    Color = new Color(0.95f, 0.74f, 0.36f, 1f) // advisory amber
                });
            }

            return tags;
        }

        /// <summary>Formats "$price" plus "· $X/ft²" when sqft is known.</summary>
        public static string Format(Comp comp)
        {
            string price = $"${comp.Price:N0}";

            // Prefer a provider-supplied $/ft²; otherwise derive it, guarding sqft <= 0.
            float perSqFt = comp.PricePerSqFt;
            if (perSqFt <= 0f && comp.SqFt > 0f)
                perSqFt = comp.Price / comp.SqFt;

            if (perSqFt <= 0f) return price; // no sqft -> price only

            return $"{price} · ${perSqFt:N0}/ft²";
        }
    }
}
