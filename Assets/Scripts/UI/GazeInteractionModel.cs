using System;

namespace SuperRealEstate.UI
{
    /// <summary>
    /// Platform-agnostic gaze interaction state machine. The per-device input
    /// layer (visionOS gaze+pinch, Android XR eye-gaze + hand/controller, phone
    /// touch ray) feeds it raw events; feature code listens to the normalized
    /// transitions. Encodes the design-system rules: gaze hovers/previews freely,
    /// but only an explicit commit selects (no Midas touch). Pure + tested.
    /// </summary>
    public sealed class GazeInteractionModel
    {
        public string HoveredId { get; private set; }
        public string SelectedId { get; private set; }
        public GazeState State { get; private set; } = GazeState.Idle;

        public event Action<string> Hovered;   // id
        public event Action<string> Unhovered; // id
        public event Action<string> Selected2; // id (committed)

        /// <summary>Gaze entered a target — hover/preview only, never commits.</summary>
        public void GazeEnter(string targetId)
        {
            if (string.IsNullOrEmpty(targetId) || targetId == HoveredId) return;
            GazeExit();
            HoveredId = targetId;
            State = GazeState.Hovered;
            Hovered?.Invoke(targetId);
        }

        /// <summary>Gaze left the current target.</summary>
        public void GazeExit()
        {
            if (HoveredId == null) return;
            string prev = HoveredId;
            HoveredId = null;
            if (State == GazeState.Hovered) State = GazeState.Idle;
            Unhovered?.Invoke(prev);
        }

        /// <summary>
        /// Explicit commit gesture (pinch / trigger / tap / confirmed dwell).
        /// Selects the currently-hovered target. Returns false (and does nothing)
        /// if nothing is hovered — committing on empty gaze must never act.
        /// </summary>
        public bool Commit()
        {
            if (string.IsNullOrEmpty(HoveredId)) return false;
            SelectedId = HoveredId;
            State = GazeState.Selected;
            Selected2?.Invoke(SelectedId);
            return true;
        }

        /// <summary>Clear a selection (e.g. after the action completes).</summary>
        public void Deselect()
        {
            SelectedId = null;
            State = HoveredId != null ? GazeState.Hovered : GazeState.Idle;
        }
    }
}
