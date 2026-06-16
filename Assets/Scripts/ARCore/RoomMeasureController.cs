using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using SuperRealEstate.RoomMeasure;

namespace SuperRealEstate.ARCore
{
    /// <summary>
    /// Phase 1 room-measurement controller. Watches detected planes, picks the
    /// largest horizontal-up plane as the floor (and an above-floor horizontal
    /// plane as the ceiling, if present), then on <see cref="CaptureRoom"/>
    /// builds a <see cref="RoomGeometry"/> and computes measurements.
    ///
    /// Editor setup required (see docs/VisionOS-Setup.md):
    ///  - Add this to a GameObject in an AR scene alongside an ARSession +
    ///    ARPlaneManager (XR Origin).
    ///  - Assign <see cref="planeManager"/> in the inspector.
    ///  - On visionOS, configure PolySpatial + a volume camera.
    ///
    /// The geometry/measurement math lives in the AR-agnostic RoomMeasure
    /// assembly and is unit-tested; this class is the thin AR glue.
    /// </summary>
    [RequireComponent(typeof(ARPlaneManager))]
    public sealed class RoomMeasureController : MonoBehaviour
    {
        [SerializeField] private ARPlaneManager planeManager;

        [Tooltip("Fallback ceiling height (m) when no ceiling plane is detected.")]
        [SerializeField] private float defaultCeilingHeight = 2.5f;

        [Serializable] public sealed class RoomMeasuredEvent : UnityEvent<RoomMeasurements> { }

        [Tooltip("Raised when a room is captured and measured.")]
        public RoomMeasuredEvent OnRoomMeasured = new RoomMeasuredEvent();

        private void Reset() => planeManager = GetComponent<ARPlaneManager>();
        private void Awake() { if (planeManager == null) planeManager = GetComponent<ARPlaneManager>(); }

        /// <summary>
        /// Capture the current best-guess room and emit measurements. Call this
        /// from a UI button / pinch / gaze-confirm gesture once the user is
        /// satisfied the floor plane covers the room.
        /// </summary>
        public bool CaptureRoom(out RoomMeasurements measurements)
        {
            measurements = default;

            ARPlane floor = LargestPlane(PlaneAlignment.HorizontalUp);
            if (floor == null) return false;

            float ceilingHeight = EstimateCeilingHeight(floor);
            RoomGeometry room = FloorOutlineBuilder.BuildRoom(
                new Pose(floor.transform.position, floor.transform.rotation),
                ToManagedBoundary(floor.boundary),
                ceilingHeight);

            if (!room.IsValid) return false;

            measurements = MeasurementService.Compute(room);
            OnRoomMeasured.Invoke(measurements);
            return true;
        }

        private float EstimateCeilingHeight(ARPlane floor)
        {
            ARPlane ceiling = HighestPlane(PlaneAlignment.HorizontalDown, aboveY: floor.transform.position.y);
            if (ceiling == null) return defaultCeilingHeight;
            return Mathf.Abs(ceiling.transform.position.y - floor.transform.position.y);
        }

        private ARPlane LargestPlane(PlaneAlignment alignment)
        {
            ARPlane best = null;
            float bestSize = 0f;
            foreach (ARPlane plane in planeManager.trackables)
            {
                if (plane.alignment != alignment) continue;
                float size = plane.size.x * plane.size.y;
                if (size > bestSize) { bestSize = size; best = plane; }
            }
            return best;
        }

        private ARPlane HighestPlane(PlaneAlignment alignment, float aboveY)
        {
            ARPlane best = null;
            float bestY = aboveY;
            foreach (ARPlane plane in planeManager.trackables)
            {
                if (plane.alignment != alignment) continue;
                float y = plane.transform.position.y;
                if (y > bestY) { bestY = y; best = plane; }
            }
            return best;
        }

        private static List<Vector2> ToManagedBoundary(NativeArray<Vector2> boundary)
        {
            var list = new List<Vector2>(boundary.Length);
            for (int i = 0; i < boundary.Length; i++) list.Add(boundary[i]);
            return list;
        }
    }
}
