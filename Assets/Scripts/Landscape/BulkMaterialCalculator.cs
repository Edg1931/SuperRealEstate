using UnityEngine;

namespace SuperRealEstate.Landscape
{
    public readonly struct FenceTakeoff
    {
        public readonly float LinearFeet;
        public readonly int Posts;
        public readonly int Panels;
        public FenceTakeoff(float linearFeet, int posts, int panels)
        {
            LinearFeet = linearFeet; Posts = posts; Panels = panels;
        }
    }

    /// <summary>
    /// Outdoor "how much do I need?" math: measure a flower bed, patio, or lawn
    /// in AR and get bulk-material quantities — mulch/soil/gravel by volume,
    /// concrete, sod, seed, pavers, fence. Pure + unit-tested. Area comes from
    /// the same AR measurement pipeline (RoomMeasure / MeasurementService).
    /// </summary>
    public static class BulkMaterialCalculator
    {
        public const float SqFtPerSqM = 10.7639f;
        private const float CubicFeetPerYard = 27f;
        private const float MetersPerFoot = 0.3048f;

        /// <summary>Cubic yards of loose fill (mulch / topsoil / gravel) for an area at a depth.</summary>
        public static float CubicYards(float areaSqM, float depthInches)
        {
            float cubicFeet = areaSqM * SqFtPerSqM * (Mathf.Max(0f, depthInches) / 12f);
            return cubicFeet / CubicFeetPerYard;
        }

        /// <summary>Bags of loose fill (rounds up). Default 2 ft³ mulch bag.</summary>
        public static int Bags(float areaSqM, float depthInches, float bagCubicFeet = 2f)
        {
            if (bagCubicFeet <= 0f) bagCubicFeet = 2f;
            float cubicFeet = areaSqM * SqFtPerSqM * (Mathf.Max(0f, depthInches) / 12f);
            return Mathf.CeilToInt(cubicFeet / bagCubicFeet);
        }

        /// <summary>Concrete volume for a slab (cubic yards) — same as loose-fill volume.</summary>
        public static float ConcreteCubicYards(float areaSqM, float thicknessInches)
            => CubicYards(areaSqM, thicknessInches);

        /// <summary>Square feet of sod including waste (5% default).</summary>
        public static float SodSqFt(float areaSqM, float wasteFactor = 0.05f)
            => areaSqM * SqFtPerSqM * (1f + Mathf.Max(0f, wasteFactor));

        /// <summary>Pounds of grass seed at a coverage rate (default 4 lb / 1000 ft²).</summary>
        public static float GrassSeedLbs(float areaSqM, float lbsPer1000SqFt = 4f)
            => (areaSqM * SqFtPerSqM / 1000f) * lbsPer1000SqFt;

        /// <summary>Number of pavers to cover an area (rounds up, with waste).</summary>
        public static int Pavers(float areaSqM, float paverWidthInches, float paverLengthInches, float wasteFactor = 0.10f)
        {
            float paverSqFt = (paverWidthInches * paverLengthInches) / 144f;
            if (paverSqFt <= 0f) return 0;
            float areaSqFt = areaSqM * SqFtPerSqM * (1f + Mathf.Max(0f, wasteFactor));
            return Mathf.CeilToInt(areaSqFt / paverSqFt);
        }

        /// <summary>Fence takeoff from a run length: posts + panels.</summary>
        public static FenceTakeoff Fence(float lengthM, float panelLengthFt = 8f)
        {
            if (panelLengthFt <= 0f) panelLengthFt = 8f;
            float ft = lengthM / MetersPerFoot;
            int panels = Mathf.CeilToInt(ft / panelLengthFt);
            int posts = panels + 1; // one more post than panels for a straight run
            return new FenceTakeoff(ft, posts, panels);
        }

        /// <summary>Convenience: quantity × unit price.</summary>
        public static float Cost(float quantity, float unitPrice) => quantity * unitPrice;
    }
}
