using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using SuperRealEstate.Renovation;

namespace SuperRealEstate.ARCore
{
    /// <summary>
    /// On-device room scan: walk a room, let plane detection converge on the
    /// walls + floor, then <see cref="CaptureModel"/> turns the detected vertical
    /// planes into an editable <see cref="BuildingModel"/> (one <see cref="Wall"/>
    /// per wall plane, floor height from the floor plane). This is the capture
    /// path that complements the CubiCasa/Matterport importers — no upload, just
    /// the headset/phone's own scene understanding.
    ///
    /// The geometry math is the AR-free, unit-tested <see cref="WallScanBuilder"/>;
    /// this MonoBehaviour is the thin glue that reads <c>ARPlaneManager</c>. Wire
    /// the resulting model into <see cref="SceneAppActions.SetBuildingModel"/> so
    /// "remove that wall" works on a freshly scanned room, and/or save it as a
    /// project (building_models.source_type = 'ar_scan').
    ///
    /// Editor setup: add alongside an AR Session + XR Origin with an
    /// <c>ARPlaneManager</c> (Detection Mode = Horizontal + Vertical); assign
    /// <see cref="planeManager"/>.
    /// </summary>
    public sealed class RoomScanController : MonoBehaviour
    {
        [SerializeField] private ARPlaneManager planeManager;

        [Tooltip("Fallback floor height (m) when no horizontal floor plane is found.")]
        [SerializeField] private float defaultFloorY = 0f;

        [Serializable] public sealed class BuildingModelEvent : UnityEvent<BuildingModel> { }

        [Tooltip("Raised when a room is scanned into an editable building model.")]
        public BuildingModelEvent OnModelCaptured = new BuildingModelEvent();

        private void Reset() => planeManager = GetComponent<ARPlaneManager>();
        private void Awake() { if (planeManager == null) planeManager = GetComponent<ARPlaneManager>(); }

        /// <summary>The most recently captured model (null until <see cref="CaptureModel"/>).</summary>
        public BuildingModel LastModel { get; private set; }

        /// <summary>
        /// Snapshot the currently-detected vertical planes into a building model.
        /// Returns false (and emits nothing) when no usable walls are present yet.
        /// </summary>
        public bool CaptureModel(out BuildingModel model)
        {
            model = null;
            if (planeManager == null) return false;

            float floorY = EstimateFloorY();
            var surfaces = new List<ScannedSurface>();
            foreach (ARPlane plane in planeManager.trackables)
            {
                if (!IsVertical(plane.alignment)) continue;
                Transform t = plane.transform;
                surfaces.Add(new ScannedSurface(t.position, t.right, plane.size.x, plane.size.y));
            }

            BuildingModel built = WallScanBuilder.BuildModel(surfaces, floorY);
            if (built.Walls.Count == 0) return false;

            LastModel = built;
            model = built;
            OnModelCaptured.Invoke(built);
            return true;
        }

        private float EstimateFloorY()
        {
            float bestY = float.MaxValue;
            bool found = false;
            foreach (ARPlane plane in planeManager.trackables)
            {
                if (plane.alignment != PlaneAlignment.HorizontalUp) continue;
                float y = plane.transform.position.y;
                if (y < bestY) { bestY = y; found = true; }
            }
            return found ? bestY : defaultFloorY;
        }

        private static bool IsVertical(PlaneAlignment alignment) => alignment == PlaneAlignment.Vertical;
    }
}
