using UnityEngine;

namespace SuperRealEstate.Renovation
{
    public enum LoadBearingLikelihood { Low, Possible, Likely }

    public readonly struct LoadBearingAssessment
    {
        public readonly LoadBearingLikelihood Likelihood;
        public readonly string Reasons;
        public readonly string Disclaimer;

        public LoadBearingAssessment(LoadBearingLikelihood likelihood, string reasons, string disclaimer)
        {
            Likelihood = likelihood;
            Reasons = reasons;
            Disclaimer = disclaimer;
        }
    }

    /// <summary>
    /// Heuristic, ADVISORY assessment of whether a wall is likely structural —
    /// surfaced *before* someone imagines removing it. Never a determination;
    /// always carries a disclaimer (see VISION.md responsible-AI guardrails).
    /// </summary>
    public static class LoadBearingAdvisor
    {
        public const string Disclaimer =
            "Advisory only. This wall may be load-bearing — confirm with a licensed " +
            "structural engineer before removing or altering it. This is not an " +
            "engineering assessment.";

        /// <summary>
        /// Combine common structural signals. <paramref name="perpendicularToJoists"/>
        /// is the strongest cue (a wall carrying joists is almost certainly bearing).
        /// </summary>
        public static LoadBearingAssessment Assess(
            bool isExterior,
            float lengthM,
            bool perpendicularToJoists,
            float longSpanThresholdM = 3.5f)
        {
            int score = 0;
            var reasons = new System.Text.StringBuilder();

            if (isExterior) { score += 2; reasons.Append("exterior wall; "); }
            if (perpendicularToJoists) { score += 2; reasons.Append("runs perpendicular to floor joists; "); }
            if (lengthM >= longSpanThresholdM) { score += 1; reasons.Append("long uninterrupted span; "); }

            LoadBearingLikelihood likelihood =
                score >= 3 ? LoadBearingLikelihood.Likely :
                score >= 1 ? LoadBearingLikelihood.Possible :
                             LoadBearingLikelihood.Low;

            string reasonText = reasons.Length > 0 ? reasons.ToString().TrimEnd(' ', ';') : "no strong structural cues";
            return new LoadBearingAssessment(likelihood, reasonText, Disclaimer);
        }
    }
}
