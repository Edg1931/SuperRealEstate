using UnityEngine;
using UnityEngine.InputSystem;

namespace SuperRealEstate.ARCore
{
    /// <summary>
    /// Drives a target <see cref="Transform"/> (the one assigned as
    /// <see cref="SpatialPointerInput"/>'s <c>pointerOrigin</c>) from pose input
    /// actions, so the gaze/pointer ray follows the platform's aim source. One
    /// component, every platform — bind the actions per device in the inspector:
    ///
    ///  • Android XR → bind <see cref="positionAction"/>/<see cref="rotationAction"/>
    ///    to the OpenXR **eye-gaze** pose (Eye Gaze Interaction Profile); the
    ///    Select action on <c>SpatialPointerInput</c> is pinch/trigger.
    ///  • visionOS  → bind to the spatial-pointer pose (PolySpatial surfaces it as
    ///    an Input System device); Select = SpatialTapGesture. The system still
    ///    does gaze hover privately — we only consume the resolved pointer pose.
    ///  • Phone/tablet → no pose actions needed; assign <see cref="fallbackSource"/>
    ///    to the AR camera and the origin simply tracks the camera.
    ///
    /// Consuming poses through the Input System (not raw OpenXR/PolySpatial types)
    /// keeps this assembly free of per-platform package dependencies. When no pose
    /// action has data, it falls back to <see cref="fallbackSource"/> (e.g. the
    /// camera) so the ray is always defined.
    /// </summary>
    public sealed class PointerPoseDriver : MonoBehaviour
    {
        [Tooltip("Transform to drive — the same one set as SpatialPointerInput.pointerOrigin. Defaults to this object.")]
        [SerializeField] private Transform target;

        [Tooltip("Pose position action (e.g. OpenXR eye-gaze position / visionOS pointer position).")]
        [SerializeField] private InputActionProperty positionAction;

        [Tooltip("Pose rotation action (e.g. OpenXR eye-gaze rotation / visionOS pointer rotation).")]
        [SerializeField] private InputActionProperty rotationAction;

        [Tooltip("Used when no pose data is available (e.g. the AR camera on phones). Optional.")]
        [SerializeField] private Transform fallbackSource;

        [SerializeField] private bool trackPosition = true;
        [SerializeField] private bool trackRotation = true;

        private void Awake() { if (target == null) target = transform; }

        private void OnEnable()
        {
            EnableAction(positionAction);
            EnableAction(rotationAction);
        }

        private void OnDisable()
        {
            DisableAction(positionAction);
            DisableAction(rotationAction);
        }

        private void Update()
        {
            if (target == null) return;

            bool gotPose = false;
            Vector3 position = target.position;
            Quaternion rotation = target.rotation;

            if (trackPosition && TryReadVector3(positionAction, out Vector3 p))
            {
                position = p;
                gotPose = true;
            }

            if (trackRotation && TryReadQuaternion(rotationAction, out Quaternion r))
            {
                rotation = r;
                gotPose = true;
            }

            // No live pose this frame → follow the fallback source (e.g. camera).
            if (!gotPose && fallbackSource != null)
            {
                position = fallbackSource.position;
                rotation = fallbackSource.rotation;
            }

            target.SetPositionAndRotation(position, rotation);
        }

        private static void EnableAction(InputActionProperty prop)
        {
            InputAction a = prop.action;
            if (a != null && !a.enabled) a.Enable();
        }

        private static void DisableAction(InputActionProperty prop)
        {
            InputAction a = prop.action;
            // Only disable inline actions we own; leave shared asset actions alone.
            if (a != null && prop.reference == null && a.enabled) a.Disable();
        }

        private static bool TryReadVector3(InputActionProperty prop, out Vector3 value)
        {
            value = default;
            InputAction a = prop.action;
            if (a == null || !a.enabled) return false;
            // A pose action with no bound, tracking device reports no control.
            if (a.activeControl == null) return false;
            value = a.ReadValue<Vector3>();
            return true;
        }

        private static bool TryReadQuaternion(InputActionProperty prop, out Quaternion value)
        {
            value = Quaternion.identity;
            InputAction a = prop.action;
            if (a == null || !a.enabled) return false;
            if (a.activeControl == null) return false;
            value = a.ReadValue<Quaternion>();
            return true;
        }
    }
}
