using System;
using UnityEngine;

namespace SuperRealEstate.Insights
{
    /// <summary>What kind of insight this is (drives icon, grouping, routing).</summary>
    public enum InsightCategory
    {
        Measurement,    // dimensions, area
        Cost,           // material / renovation cost
        Condition,      // wear, damage, defect cues (advisory)
        Light,          // natural light / orientation
        Comp,           // valuation / comparables
        CodeClearance,  // egress, ceiling height, stair geometry (advisory)
        Vegetation,     // plant / tree identification
        Appliance,      // appliance / fixture / finish recognition
        TalkingPoint,   // agent coaching / value justification
        Preference      // matches this buyer's stated wishlist
    }

    public enum InsightSeverity
    {
        Info,
        Suggestion,
        Caution
    }

    /// <summary>
    /// One piece of contextual insight to surface in the headset/phone and, for
    /// agent coaching, relay to the client. May be anchored in space via
    /// <see cref="HasWorldAnchor"/> + <see cref="WorldPosition"/>.
    ///
    /// IMPORTANT: Condition / CodeClearance / Comp insights are advisory only.
    /// When <see cref="IsAdvisory"/> is true the UI must show <see cref="Disclaimer"/>
    /// and never present the insight as a determination (see VISION.md guardrails).
    /// </summary>
    [Serializable]
    public sealed class SceneInsight
    {
        public InsightCategory Category;
        public InsightSeverity Severity = InsightSeverity.Info;

        public string Title;
        public string Detail;

        /// <summary>Optional, ready-to-say line the agent can relay to the client.</summary>
        public string SuggestedTalkingPoint;

        /// <summary>Model confidence in [0,1]; gate UI prominence on this.</summary>
        [Range(0f, 1f)] public float Confidence = 1f;

        public bool IsAdvisory;
        public string Disclaimer;

        public bool HasWorldAnchor;
        public Vector3 WorldPosition;

        /// <summary>Where the data came from (model name, API, record), for trust.</summary>
        public string Source;
    }
}
