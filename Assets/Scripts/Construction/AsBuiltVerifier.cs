using UnityEngine;

namespace SuperRealEstate.Construction
{
    public readonly struct AsBuiltResult
    {
        public readonly float DeviationM;
        public readonly bool WithinTolerance;
        public AsBuiltResult(float deviationM, bool withinTolerance)
        {
            DeviationM = deviationM; WithinTolerance = withinTolerance;
        }
        public float DeviationInches => DeviationM / 0.0254f;
    }

    /// <summary>
    /// As-built verification: compare where a fixture/run was *designed* against
    /// where it was actually *installed* (measured in AR) and flag deviations
    /// beyond tolerance. This is the "is everything where it should be?" check —
    /// catch the rough-in that drifted before drywall hides it. Pure + tested.
    /// </summary>
    public static class AsBuiltVerifier
    {
        public static AsBuiltResult Check(Vector3 designed, Vector3 measured, float toleranceM)
        {
            float d = Vector3.Distance(designed, measured);
            return new AsBuiltResult(d, d <= toleranceM);
        }
    }
}
