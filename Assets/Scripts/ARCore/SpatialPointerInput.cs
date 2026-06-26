using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using SuperRealEstate.UI;

namespace SuperRealEstate.ARCore
{
    /// <summary>
    /// One input adapter for every platform. It raycasts from a
    /// <see cref="pointerOrigin"/> transform and drives the shared
    /// <see cref="GazeInteractionModel"/>; the platform decides what drives the
    /// origin and the select action:
    ///
    ///  • Android XR  → pointerOrigin follows the OpenXR **eye-gaze** pose;
    ///                  selectAction = pinch / trigger.
    ///  • visionOS    → pointerOrigin follows the spatial pointer; selectAction =
    ///                  SpatialTapGesture (the system still does gaze hover
    ///                  privately — we react to the resolved hit).
    ///  • Phone       → pointerOrigin = AR camera; selectAction = screen tap.
    ///
    /// Feature code subscribes to the model — never to a platform.
    /// </summary>
    public sealed class SpatialPointerInput : MonoBehaviour
    {
        [Tooltip("Transform whose position+forward defines the ray (eye-gaze pose / pointer / camera).")]
        [SerializeField] private Transform pointerOrigin;

        [Tooltip("Commit gesture: pinch / trigger / tap.")]
        [SerializeField] private InputActionProperty selectAction;

        [SerializeField] private float maxDistance = 8f;
        [SerializeField] private LayerMask targetMask = ~0;

        /// <summary>The shared, platform-agnostic interaction state.</summary>
        public GazeInteractionModel Model { get; } = new GazeInteractionModel();

        private readonly Dictionary<string, GazeTarget> _byId = new Dictionary<string, GazeTarget>();
        private GazeTarget _current;

        private void OnEnable()
        {
            Model.Hovered += OnModelHovered;
            Model.Unhovered += OnModelUnhovered;
            Model.Selected2 += OnModelSelected;
            if (selectAction.action != null)
            {
                selectAction.action.performed += OnSelectPerformed;
                selectAction.action.Enable();
            }
        }

        private void OnDisable()
        {
            Model.Hovered -= OnModelHovered;
            Model.Unhovered -= OnModelUnhovered;
            Model.Selected2 -= OnModelSelected;
            if (selectAction.action != null) selectAction.action.performed -= OnSelectPerformed;
        }

        private void Update()
        {
            if (pointerOrigin == null) return;

            if (Physics.Raycast(pointerOrigin.position, pointerOrigin.forward, out var hit, maxDistance, targetMask)
                && hit.collider.TryGetComponent(out GazeTarget target))
            {
                _current = target;
                _byId[target.Id] = target;
                Model.GazeEnter(target.Id); // no-op if already hovered (no Midas touch)
            }
            else
            {
                _current = null;
                Model.GazeExit();
            }
        }

        private void OnSelectPerformed(InputAction.CallbackContext _) => Model.Commit();

        private void OnModelHovered(string id) { if (_byId.TryGetValue(id, out var t)) t.OnHover.Invoke(); }
        private void OnModelUnhovered(string id) { if (_byId.TryGetValue(id, out var t)) t.OnUnhover.Invoke(); }
        private void OnModelSelected(string id) { if (_byId.TryGetValue(id, out var t)) t.OnSelect.Invoke(); }
    }
}
