using System.Collections.Generic;
using UnityEngine;
using SuperRealEstate.Insights;
using SuperRealEstate.RoomMeasure;
using SuperRealEstate.UI;

namespace SuperRealEstate.Presentation
{
    /// <summary>Semantic color role → DesignTokens color (single source of truth).</summary>
    public enum UiRole { Neutral, Accent, Positive, Caution, Advisory }

    public static class RoleColor
    {
        public static Color For(UiRole role) => role switch
        {
            UiRole.Accent => DesignTokens.Accent,
            UiRole.Positive => DesignTokens.Positive,
            UiRole.Caution => DesignTokens.Caution,
            UiRole.Advisory => DesignTokens.Advisory,
            _ => DesignTokens.OnSurface,
        };
    }

    /// <summary>Display-ready insight card (maps <see cref="SceneInsight"/> → UI).</summary>
    public readonly struct InsightCardVM
    {
        public readonly string Title;
        public readonly string Detail;
        public readonly string TalkingPoint;
        public readonly UiRole Role;
        public readonly bool IsAdvisory;
        public readonly string Disclaimer;

        public InsightCardVM(string title, string detail, string talkingPoint, UiRole role, bool advisory, string disclaimer)
        {
            Title = title; Detail = detail; TalkingPoint = talkingPoint;
            Role = role; IsAdvisory = advisory; Disclaimer = disclaimer;
        }

        public static InsightCardVM From(SceneInsight i)
        {
            UiRole role = i.IsAdvisory ? UiRole.Advisory
                : i.Severity == InsightSeverity.Caution ? UiRole.Caution
                : i.Severity == InsightSeverity.Suggestion ? UiRole.Accent
                : UiRole.Neutral;
            return new InsightCardVM(i.Title, i.Detail, i.SuggestedTalkingPoint, role, i.IsAdvisory, i.Disclaimer);
        }
    }

    /// <summary>Display-ready plant card with fact lines and warning chips.</summary>
    public readonly struct PlantCardVM
    {
        public readonly string Title;       // common name
        public readonly string Subtitle;    // scientific name (italic in view)
        public readonly List<string> Facts;
        public readonly List<string> Warnings; // toxic / invasive / pollen
        public readonly bool IsAdvisory;

        public PlantCardVM(string title, string subtitle, List<string> facts, List<string> warnings, bool advisory)
        {
            Title = title; Subtitle = subtitle; Facts = facts; Warnings = warnings; IsAdvisory = advisory;
        }

        public static PlantCardVM From(PlantIdentification p)
        {
            var facts = new List<string>();
            if (!string.IsNullOrEmpty(p.CareLevel)) facts.Add($"Care: {p.CareLevel}");
            if (!string.IsNullOrEmpty(p.Water)) facts.Add($"Water: {p.Water}");
            if (!string.IsNullOrEmpty(p.Sun)) facts.Add($"Sun: {p.Sun}");
            if (!string.IsNullOrEmpty(p.MatureSize)) facts.Add($"Mature: {p.MatureSize}");
            if (p.ReplacementCost > 0f) facts.Add($"~${p.ReplacementCost:0}");

            var warnings = new List<string>();
            if (p.ToxicToPetsOrKids) warnings.Add("Toxic to pets/kids");
            if (p.Invasive) warnings.Add("Invasive");
            if (p.PollenAllergy == "high" || p.PollenAllergy == "moderate") warnings.Add($"{p.PollenAllergy} pollen");

            return new PlantCardVM(p.CommonName, p.ScientificName, facts, warnings, p.IsAdvisory);
        }
    }

    /// <summary>Room measurements formatted for a glanceable readout (metric + imperial).</summary>
    public readonly struct MeasurementReadoutVM
    {
        public readonly string FloorArea;
        public readonly string WallArea;
        public readonly string Perimeter;
        public readonly string CeilingHeight;

        public MeasurementReadoutVM(string floor, string wall, string perimeter, string ceiling)
        {
            FloorArea = floor; WallArea = wall; Perimeter = perimeter; CeilingHeight = ceiling;
        }

        public static MeasurementReadoutVM From(RoomMeasurements m) => new MeasurementReadoutVM(
            $"{m.FloorAreaSqM:0.0} m² · {m.FloorAreaSqFt:0} ft²",
            $"{m.WallAreaSqM:0.0} m² · {m.WallAreaSqFt:0} ft²",
            $"{m.PerimeterM:0.0} m · {m.PerimeterFt:0} ft",
            $"{m.CeilingHeightM:0.00} m · {m.CeilingHeightFt:0.0} ft");
    }
}
