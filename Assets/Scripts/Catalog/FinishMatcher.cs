using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using SuperRealEstate.Insights;

namespace SuperRealEstate.Catalog
{
    public readonly struct FinishMatch
    {
        public readonly ProductCandidate Product;
        public readonly float Confidence;   // 0..1
        public readonly string Basis;       // "sku" | "brand+product" | "color" | "category"

        public FinishMatch(ProductCandidate product, float confidence, string basis)
        {
            Product = product;
            Confidence = confidence;
            Basis = basis;
        }
    }

    /// <summary>
    /// Scores an AI-recognized <see cref="SurfaceFinding"/> against candidate
    /// products and picks the best — the bridge that turns "that wall is
    /// Sherwin-Williams Agreeable Gray SW 7029" into a real, priced, buyable,
    /// re-finishable catalog product. Pure + deterministic, so it's unit-tested.
    /// </summary>
    public static class FinishMatcher
    {
        // Max RGB euclidean distance (sqrt(3 * 255^2)).
        private const float MaxColorDist = 441.673f;

        public static FinishMatch? Best(SurfaceFinding finding, IReadOnlyList<ProductCandidate> candidates)
        {
            if (finding == null || candidates == null || candidates.Count == 0) return null;

            ProductCandidate best = null;
            float bestScore = 0f;
            string bestBasis = "category";

            foreach (var c in candidates)
            {
                float score = 0f;
                string basis = "category";

                bool skuHit = SkuMatches(finding.Product, c.Sku);
                if (skuHit) { score += 0.5f; basis = "sku"; }

                if (BrandMatches(finding.Brand, c.Brand)) score += 0.25f;

                float nameOverlap = TokenOverlap(finding.Product, c.Product); // 0..1
                score += 0.25f * nameOverlap;
                if (!skuHit && (BrandMatches(finding.Brand, c.Brand) || nameOverlap > 0.5f))
                    basis = "brand+product";

                float colorProx = ColorProximity(finding.ColorHex, c.ColorHex); // 0..1
                score += 0.15f * colorProx;
                if (score < 0.2f && colorProx > 0.9f) basis = "color";

                if (CategoryMatches(finding.MaterialType, c.Category)) score += 0.1f;

                if (score > bestScore)
                {
                    bestScore = score;
                    best = c;
                    bestBasis = basis;
                }
            }

            if (best == null) return null;
            return new FinishMatch(best, Mathf.Clamp01(bestScore), bestBasis);
        }

        // --- scoring helpers ---

        private static bool BrandMatches(string a, string b)
            => !string.IsNullOrEmpty(a) && !string.IsNullOrEmpty(b)
               && Normalize(a) == Normalize(b);

        private static bool CategoryMatches(string a, string b)
            => !string.IsNullOrEmpty(a) && !string.IsNullOrEmpty(b)
               && Normalize(a) == Normalize(b);

        /// <summary>True if the recognized text contains the candidate's product code.</summary>
        private static bool SkuMatches(string recognizedText, string sku)
        {
            if (string.IsNullOrEmpty(recognizedText) || string.IsNullOrEmpty(sku)) return false;
            string r = Normalize(recognizedText);
            string s = Normalize(sku);
            return s.Length > 0 && r.Contains(s);
        }

        /// <summary>Jaccard overlap of word tokens (letters/digits only).</summary>
        public static float TokenOverlap(string a, string b)
        {
            var ta = Tokens(a);
            var tb = Tokens(b);
            if (ta.Count == 0 || tb.Count == 0) return 0f;

            int inter = 0;
            foreach (var t in ta) if (tb.Contains(t)) inter++;
            int union = ta.Count + tb.Count - inter;
            return union == 0 ? 0f : (float)inter / union;
        }

        /// <summary>1.0 = identical color, 0.0 = maximally distant; 0 if unparseable.</summary>
        public static float ColorProximity(string hexA, string hexB)
        {
            if (!TryParseHex(hexA, out var a) || !TryParseHex(hexB, out var b)) return 0f;
            float d = Mathf.Sqrt(
                (a.r - b.r) * (a.r - b.r) +
                (a.g - b.g) * (a.g - b.g) +
                (a.b - b.b) * (a.b - b.b));
            return Mathf.Clamp01(1f - (d / MaxColorDist));
        }

        private static HashSet<string> Tokens(string s)
        {
            var set = new HashSet<string>();
            if (string.IsNullOrEmpty(s)) return set;
            var sb = new StringBuilder();
            foreach (char ch in s.ToLowerInvariant())
            {
                if (char.IsLetterOrDigit(ch)) sb.Append(ch);
                else if (sb.Length > 0) { set.Add(sb.ToString()); sb.Clear(); }
            }
            if (sb.Length > 0) set.Add(sb.ToString());
            return set;
        }

        private static string Normalize(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            var sb = new StringBuilder();
            foreach (char ch in s.ToLowerInvariant())
                if (char.IsLetterOrDigit(ch)) sb.Append(ch);
            return sb.ToString();
        }

        private static bool TryParseHex(string hex, out (float r, float g, float b) rgb)
        {
            rgb = default;
            if (string.IsNullOrEmpty(hex)) return false;
            string h = hex.Trim().TrimStart('#');
            if (h.Length != 6) return false;
            try
            {
                rgb = (
                    Convert.ToInt32(h.Substring(0, 2), 16),
                    Convert.ToInt32(h.Substring(2, 2), 16),
                    Convert.ToInt32(h.Substring(4, 2), 16));
                return true;
            }
            catch { return false; }
        }
    }
}
