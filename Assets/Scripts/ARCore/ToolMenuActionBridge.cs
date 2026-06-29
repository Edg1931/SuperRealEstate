using System;
using UnityEngine;
using UnityEngine.Events;
using SuperRealEstate.UI;

namespace SuperRealEstate.ARCore
{
    /// <summary>
    /// Connects the <see cref="RadialToolMenuView"/> (the navigation chrome) to
    /// real actions — the missing link that made the tool palette drive nothing
    /// but visuals. Parameterless tools (Measure / Identify / Finishes) run their
    /// <see cref="SceneAppActions"/> immediately; tools that need a target
    /// (Stage / Remove wall / Landscape / Notes) raise
    /// <see cref="OnToolModeRequested"/> so the app can enter that mode (e.g. "gaze
    /// the wall to remove"). Blocked tools surface their reason via
    /// <see cref="OnInfo"/>. With this, gaze+pinch navigation works end to end on
    /// both Vision Pro and Galaxy XR — voice is no longer the only way to act.
    /// </summary>
    public sealed class ToolMenuActionBridge : MonoBehaviour
    {
        [SerializeField] private RadialToolMenuView menu;
        [SerializeField] private SceneAppActions actions;
        [Tooltip("Dismiss the palette once a tool is chosen.")]
        [SerializeField] private bool hideMenuOnSelect = true;

        [Serializable] public sealed class ToolEvent : UnityEvent<ToolId> { }
        [Serializable] public sealed class StringEvent : UnityEvent<string> { }

        [Tooltip("Raised for tools that need a target/selection (Stage, Remove wall, Landscape, Notes).")]
        public ToolEvent OnToolModeRequested = new ToolEvent();

        [Tooltip("Status / blocked-reason text for the HUD.")]
        public StringEvent OnInfo = new StringEvent();

        private void OnEnable()
        {
            if (menu == null) return;
            menu.OnToolSelected.AddListener(HandleSelected);
            menu.OnToolBlocked.AddListener(HandleBlocked);
        }

        private void OnDisable()
        {
            if (menu == null) return;
            menu.OnToolSelected.RemoveListener(HandleSelected);
            menu.OnToolBlocked.RemoveListener(HandleBlocked);
        }

        private void HandleSelected(ToolId tool)
        {
            switch (tool)
            {
                case ToolId.Measure:
                    if (actions != null) _ = actions.MeasureRoomAsync();
                    break;
                case ToolId.Identify:
                    if (actions != null) _ = actions.IdentifyPlantAsync();
                    break;
                case ToolId.Finish:
                    if (actions != null) _ = actions.RecognizeFinishAsync();
                    break;

                // These need a target/selection — hand off to the app's mode layer.
                case ToolId.Stage:
                case ToolId.RemoveWall:
                case ToolId.Landscape:
                case ToolId.Notes:
                    OnToolModeRequested.Invoke(tool);
                    break;
            }

            if (hideMenuOnSelect && menu != null) menu.Hide();
        }

        private void HandleBlocked(string reason) => OnInfo.Invoke(reason);
    }
}
