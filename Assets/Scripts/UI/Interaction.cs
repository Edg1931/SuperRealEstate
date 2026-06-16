namespace SuperRealEstate.UI
{
    public enum SpatialDevice { VisionPro, GalaxyXR, Phone, Tablet }

    /// <summary>How a selection is committed. Gaze aims/previews; these commit.</summary>
    public enum InputModality
    {
        GazePinch,      // look + pinch (primary on headsets)
        ControllerRay,  // ray + trigger
        TouchScreen,    // tap + on-screen AR ray (phone/tablet)
        Voice,          // "what's this?", "measure this room"
        GazeDwell       // accessibility fallback only
    }

    /// <summary>Gaze target lifecycle for hover/preview/commit feedback.</summary>
    public enum GazeState { Idle, Hovered, Selected }

    public static class Interaction
    {
        /// <summary>The primary commit modality for a device (gaze always aims).</summary>
        public static InputModality PrimaryModality(SpatialDevice device) => device switch
        {
            SpatialDevice.VisionPro => InputModality.GazePinch,
            SpatialDevice.GalaxyXR  => InputModality.GazePinch,
            SpatialDevice.Phone     => InputModality.TouchScreen,
            SpatialDevice.Tablet    => InputModality.TouchScreen,
            _ => InputModality.GazePinch
        };

        /// <summary>
        /// Gaze may PREVIEW/hover freely, but must never trigger an action on its
        /// own (no "Midas touch"). Only an explicitly-committed target
        /// (<see cref="GazeState.Selected"/>, reached via pinch / controller /
        /// touch / voice / confirmed dwell) can act; hovering cannot.
        /// </summary>
        public static bool CanCommit(GazeState state) => state == GazeState.Selected;
    }
}
