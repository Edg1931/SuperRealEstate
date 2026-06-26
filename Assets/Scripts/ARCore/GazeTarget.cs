using UnityEngine;
using UnityEngine.Events;

namespace SuperRealEstate.ARCore
{
    /// <summary>
    /// Marks a collider as a gaze-selectable target (an insight card, a finish
    /// chip, a plant tag…). The platform input adapter raycasts to these and
    /// drives the shared <c>GazeInteractionModel</c>; these UnityEvents let the
    /// view react (glow on hover, act on select) without knowing the platform.
    /// Requires a Collider for raycasting.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class GazeTarget : MonoBehaviour
    {
        [Tooltip("Stable id used by the interaction model (e.g. 'finish:wall-3').")]
        public string Id;

        public UnityEvent OnHover = new UnityEvent();
        public UnityEvent OnUnhover = new UnityEvent();
        public UnityEvent OnSelect = new UnityEvent();

        private void Reset()
        {
            if (string.IsNullOrEmpty(Id)) Id = $"{name}-{GetInstanceID()}";
        }
    }
}
