namespace SuperRealEstate.UI
{
    /// <summary>
    /// App-wide motion preference, read by the spatial UI animators
    /// (<c>SpatialHoverFeedback</c>, <c>BillboardToUser</c>) so they can collapse
    /// animation to instant when the user (or the OS) asks for reduced motion — an
    /// accessibility + comfort requirement on headsets (animation in the periphery
    /// can trigger discomfort). A simple shared value, like <see cref="AmbientLight"/>:
    /// the settings layer sets it; animators read it each frame. Defaults to off.
    /// </summary>
    public static class MotionPrefs
    {
        /// <summary>When true, UI animations snap to their target instead of easing.</summary>
        public static bool ReduceMotion { get; private set; }

        public static void SetReduceMotion(bool reduce) => ReduceMotion = reduce;
    }
}
