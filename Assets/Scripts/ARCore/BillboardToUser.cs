using UnityEngine;
using SuperRealEstate.UI;

namespace SuperRealEstate.ARCore
{
    /// <summary>
    /// Keeps a spatial panel facing the user so text is never read at an angle —
    /// the single biggest "this feels right" factor for floating UI. Defaults to
    /// yaw-only billboarding (the panel stays upright, just turns toward you),
    /// which reads calmer than full free-rotation. Optionally re-seats the panel
    /// in the comfort zone on enable: central cone, sweet depth, slightly below
    /// the eye line (<see cref="SpatialComfort"/>), so a summoned panel always
    /// appears somewhere comfortable instead of wherever its anchor happened to be.
    ///
    /// Uses a lazy follow (a soft turn rate) rather than rigidly locking to the
    /// head, so the panel feels placed-in-the-world, not pasted-to-your-face.
    /// </summary>
    public sealed class BillboardToUser : MonoBehaviour
    {
        [Tooltip("Camera to face. Defaults to Camera.main.")]
        [SerializeField] private Transform target;
        [Tooltip("Keep upright (yaw only). Off = face the user fully.")]
        [SerializeField] private bool yawOnly = true;
        [Tooltip("Turn smoothing (higher = snappier). 0 = instant.")]
        [SerializeField] private float turnLerp = 8f;

        [Header("Comfort placement on enable")]
        [Tooltip("Re-seat in front of the user at a comfortable depth + height.")]
        [SerializeField] private bool placeOnEnable = true;
        [SerializeField] private float depthM = SpatialComfort.DepthSweetM;

        private void Awake() { if (target == null && Camera.main != null) target = Camera.main.transform; }

        private void OnEnable()
        {
            if (target == null && Camera.main != null) target = Camera.main.transform;
            if (placeOnEnable && target != null) SeatInComfortZone();
        }

        private void LateUpdate()
        {
            if (target == null) return;

            Vector3 toCam = transform.position - target.position;
            if (yawOnly) toCam.y = 0f;
            if (toCam.sqrMagnitude < 1e-6f) return;

            Quaternion want = Quaternion.LookRotation(toCam.normalized, Vector3.up);
            transform.rotation = (turnLerp <= 0f || MotionPrefs.ReduceMotion)
                ? want
                : Quaternion.Slerp(transform.rotation, want, 1f - Mathf.Exp(-turnLerp * Time.deltaTime));
        }

        /// <summary>Place the panel in the central cone, at sweet depth, just below eye line.</summary>
        public void SeatInComfortZone()
        {
            if (target == null) return;

            // Flatten the head forward to the horizontal so panels don't drift up/down
            // with head pitch; then drop slightly below the eye line.
            Vector3 fwd = target.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 1e-6f) fwd = Vector3.forward;
            fwd.Normalize();

            float depth = SpatialComfort.ClampDepth(depthM);
            float drop = depth * Mathf.Tan(SpatialComfort.BelowEyeLineDeg * Mathf.Deg2Rad);

            transform.position = target.position + fwd * depth - Vector3.up * drop;
        }
    }
}
