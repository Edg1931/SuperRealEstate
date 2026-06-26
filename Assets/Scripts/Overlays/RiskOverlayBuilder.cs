using System.Collections.Generic;
using UnityEngine;
using SuperRealEstate.PropertyData;

namespace SuperRealEstate.Overlays
{
    /// <summary>
    /// Turns a <see cref="PropertyRiskProfile"/> into concise content tags the AR
    /// layer floats as advisory property context — one tag per environmental layer
    /// ("Flood: Moderate · FEMA Zone AE") and one per nearby school
    /// ("Lincoln Elementary · 8/10 · 0.4 mi"). Pure + testable.
    ///
    /// ADVISORY: these tags surface NEUTRAL, sourced public data only (flood,
    /// wildfire, noise, schools). They are context, never a "safety" score, never
    /// demographic, and never a recommendation about a neighbourhood — fair-housing
    /// safe by construction. Colours convey severity at a glance, not judgement.
    /// </summary>
    public static class RiskOverlayBuilder
    {
        // Advisory severity colours. Avoid pure white (blooms on passthrough).
        private static readonly Color Positive = new Color(0.30f, 0.74f, 0.45f, 1f); // green  — None / Low
        private static readonly Color Caution  = new Color(0.95f, 0.74f, 0.36f, 1f); // amber  — Moderate
        private static readonly Color Strong   = new Color(0.90f, 0.45f, 0.32f, 1f); // strong — High

        /// <summary>
        /// Builds one <see cref="OverlayTag"/> per environmental layer and one per
        /// school. Never returns null; a null profile yields an empty list, and null
        /// layers/schools are skipped.
        /// </summary>
        public static List<OverlayTag> Build(PropertyRiskProfile profile)
        {
            var tags = new List<OverlayTag>();
            if (profile == null) return tags;

            if (profile.Layers != null)
            {
                foreach (var layer in profile.Layers)
                {
                    if (layer == null) continue;
                    tags.Add(new OverlayTag
                    {
                        Text = FormatLayer(layer),
                        Color = RatingColor(layer.Rating)
                    });
                }
            }

            if (profile.Schools != null)
            {
                foreach (var school in profile.Schools)
                {
                    if (school == null) continue;
                    tags.Add(new OverlayTag
                    {
                        Text = FormatSchool(school),
                        Color = SchoolColor(school.Rating)
                    });
                }
            }

            return tags;
        }

        /// <summary>
        /// Formats a layer as "Kind: Rating" plus "· Detail" when a detail is present,
        /// e.g. "Flood: Moderate · FEMA Zone AE", "Noise: Low", "Wildfire: High".
        /// </summary>
        public static string FormatLayer(RiskLayer layer)
        {
            string head = $"{KindLabel(layer.Kind)}: {layer.Rating}";
            if (string.IsNullOrEmpty(layer.Detail)) return head;
            return $"{head} · {layer.Detail}";
        }

        /// <summary>
        /// Formats a school as "Name · R/10 · D mi", e.g.
        /// "Lincoln Elementary · 8/10 · 0.4 mi". The rating clause is omitted when the
        /// rating is unavailable (0); the distance clause is omitted when non-positive.
        /// </summary>
        public static string FormatSchool(SchoolInfo school)
        {
            string text = string.IsNullOrEmpty(school.Name) ? "School" : school.Name;
            if (school.Rating > 0) text += $" · {school.Rating}/10";
            if (school.DistanceMiles > 0f) text += $" · {school.DistanceMiles:0.#} mi";
            return text;
        }

        /// <summary>
        /// Maps a severity band to an advisory colour: None/Low = positive green,
        /// Moderate = caution amber, High = a strong caution. Conveys severity, not
        /// judgement about the property or its neighbourhood.
        /// </summary>
        public static Color RatingColor(RiskRating rating)
        {
            switch (rating)
            {
                case RiskRating.None:
                case RiskRating.Low:      return Positive;
                case RiskRating.Moderate: return Caution;
                case RiskRating.High:     return Strong;
                default:                  return Positive;
            }
        }

        /// <summary>
        /// Maps a 0–10 school rating onto the same advisory palette so it reads at a
        /// glance: 7+ positive, 4–6 caution amber, below 4 strong. Neutral, educational
        /// context only — never a recommendation about a neighbourhood.
        /// </summary>
        public static Color SchoolColor(int rating)
        {
            if (rating >= 7) return Positive;
            if (rating >= 4) return Caution;
            return Strong;
        }

        /// <summary>Human-readable label for a <see cref="RiskKind"/>.</summary>
        private static string KindLabel(RiskKind kind)
        {
            switch (kind)
            {
                case RiskKind.Flood:    return "Flood";
                case RiskKind.Wildfire: return "Wildfire";
                case RiskKind.Noise:    return "Noise";
                case RiskKind.School:   return "School";
                default:                return kind.ToString();
            }
        }
    }
}
