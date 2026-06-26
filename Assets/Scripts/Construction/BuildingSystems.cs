using System;
using System.Collections.Generic;
using UnityEngine;

namespace SuperRealEstate.Construction
{
    /// <summary>The trades / building systems we can model and overlay in AR.</summary>
    public enum BuildingSystem
    {
        Electrical,
        Plumbing,
        Hvac,
        Structural,
        LowVoltage,   // data/AV/security
        Gas,
        Sewer
    }

    public enum ElementKind { Run, Fixture }

    /// <summary>
    /// One piece of a building system: a **run** (a polyline — wire/duct/pipe) or
    /// a **fixture** (a point — outlet, register, valve, panel). The same model
    /// drives both the AR overlay ("show me where the mechanicals are") and
    /// takeoffs/clash/as-built checks for the construction team.
    /// </summary>
    [Serializable]
    public sealed class SystemElement
    {
        public string Id;
        public BuildingSystem System;
        public ElementKind Kind = ElementKind.Run;
        public string Label;
        public string Spec;            // "12 AWG", "6in duct", "1/2in PEX"
        public List<Vector3> Points = new List<Vector3>(); // run polyline, or [point] fixture
        public bool AsBuiltVerified;   // false = designed; true = confirmed installed

        public float LengthM
        {
            get
            {
                if (Points == null || Points.Count < 2) return 0f;
                float total = 0f;
                for (int i = 1; i < Points.Count; i++) total += Vector3.Distance(Points[i - 1], Points[i]);
                return total;
            }
        }
    }

    /// <summary>All systems for a building model (mirrors a `building_systems` record).</summary>
    [Serializable]
    public sealed class SystemsModel
    {
        public string BuildingModelId;
        public List<SystemElement> Elements = new List<SystemElement>();
    }

    /// <summary>Takeoff math: how much wire/duct/pipe and how many fixtures. Pure + tested.</summary>
    public static class SystemMetrics
    {
        private const float MetersPerFoot = 0.3048f;

        public static float TotalLengthM(SystemsModel model, BuildingSystem system)
        {
            float total = 0f;
            foreach (var e in model.Elements)
                if (e.System == system && e.Kind == ElementKind.Run) total += e.LengthM;
            return total;
        }

        public static float TotalLinearFeet(SystemsModel model, BuildingSystem system)
            => TotalLengthM(model, system) / MetersPerFoot;

        public static int FixtureCount(SystemsModel model, BuildingSystem system)
        {
            int n = 0;
            foreach (var e in model.Elements)
                if (e.System == system && e.Kind == ElementKind.Fixture) n++;
            return n;
        }
    }
}
