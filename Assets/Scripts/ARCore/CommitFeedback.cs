using UnityEngine;
using UnityEngine.Events;

namespace SuperRealEstate.ARCore
{
    /// <summary>
    /// Multisensory confirmation on commit (pinch / tap): plays a short click and
    /// raises <see cref="OnCommit"/> so the integrator can fire platform haptics
    /// (Galaxy XR controller / hand-pinch, Vision Pro pinch). Listens once to the
    /// shared <c>GazeInteractionModel</c> on <see cref="SpatialPointerInput"/>, so a
    /// single scene object covers every selectable — no per-button wiring. A
    /// crisp audio/haptic "tick" is a big part of why selection feels premium in
    /// AR; visual-only feedback reads as cheap.
    /// </summary>
    public sealed class CommitFeedback : MonoBehaviour
    {
        [SerializeField] private SpatialPointerInput input;
        [Tooltip("Audio source for the click (2D/spatial, your choice). Optional.")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip clickClip;
        [Range(0f, 1f)] [SerializeField] private float volume = 0.6f;

        [Tooltip("Fire platform haptics here (controller rumble / hand-pinch buzz).")]
        public UnityEvent OnCommit = new UnityEvent();

        private void OnEnable()
        {
            if (input != null) input.Model.Selected2 += OnCommitted;
        }

        private void OnDisable()
        {
            if (input != null) input.Model.Selected2 -= OnCommitted;
        }

        private void OnCommitted(string _)
        {
            if (audioSource != null && clickClip != null)
                audioSource.PlayOneShot(clickClip, volume);
            OnCommit.Invoke();
        }
    }
}
