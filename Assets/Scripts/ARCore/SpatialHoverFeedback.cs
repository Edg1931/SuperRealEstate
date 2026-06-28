using UnityEngine;
using SuperRealEstate.UI;

namespace SuperRealEstate.ARCore
{
    /// <summary>
    /// The "where am I looking / what will I select" feedback every spatial
    /// control needs. On hover it gently grows the target (<see cref="SpatialComfort.HoverScale"/>)
    /// and brightens its accent; on commit it gives a quick press dip. Driven by
    /// the <see cref="GazeTarget"/>'s events (which the input layer raises from the
    /// shared <c>GazeInteractionModel</c>), so it behaves identically on Android XR
    /// eye-gaze, visionOS pointer, and phone touch.
    ///
    /// This matters most on Android XR / Galaxy XR, where (unlike visionOS) there
    /// is no system-provided hover highlight — without this, eye-gaze selection is
    /// a guess. Animation honors a short fade and eases, never snaps.
    /// </summary>
    [RequireComponent(typeof(GazeTarget))]
    public sealed class SpatialHoverFeedback : MonoBehaviour
    {
        [SerializeField] private float hoverScale = SpatialComfort.HoverScale;
        [SerializeField] private float fadeSeconds = SpatialComfort.HoverFadeSeconds;

        [Tooltip("Renderer brightened on hover (e.g. the button's accent bar). Optional.")]
        [SerializeField] private Renderer highlight;
        [SerializeField] private Color baseTint = Color.white;
        [SerializeField] private Color hoverTint = Color.white;

        private GazeTarget _target;
        private Vector3 _baseScale;
        private bool _hovered;
        private float _press;      // 1 → 0 press-pulse envelope
        private float _scaleK = 1f; // current scale multiplier
        private Color _curTint;
        private MaterialPropertyBlock _mpb;

        /// <summary>Configure the highlight renderer + its base/hover colors (from the builder).</summary>
        public void SetHighlight(Renderer renderer, Color baseColor, Color hoverColor)
        {
            highlight = renderer;
            baseTint = baseColor;
            hoverTint = hoverColor;
            _curTint = baseColor;
        }

        private void Awake()
        {
            _target = GetComponent<GazeTarget>();
            _baseScale = transform.localScale;
            _curTint = baseTint;
            _mpb = new MaterialPropertyBlock();
        }

        private void OnEnable()
        {
            _target.OnHover.AddListener(OnHover);
            _target.OnUnhover.AddListener(OnUnhover);
            _target.OnSelect.AddListener(OnSelect);
        }

        private void OnDisable()
        {
            _target.OnHover.RemoveListener(OnHover);
            _target.OnUnhover.RemoveListener(OnUnhover);
            _target.OnSelect.RemoveListener(OnSelect);
        }

        private void OnHover() => _hovered = true;
        private void OnUnhover() => _hovered = false;
        private void OnSelect() => _press = 1f;

        private void Update()
        {
            // Exponential smoothing toward the target (frame-rate independent ease).
            float k = 1f - Mathf.Exp(-Time.deltaTime / Mathf.Max(0.0001f, fadeSeconds));

            float pressDip = _press > 0f ? Mathf.Lerp(1f, 0.95f, _press) : 1f;
            _press = Mathf.Max(0f, _press - Time.deltaTime / Mathf.Max(0.0001f, fadeSeconds));

            float targetK = (_hovered ? hoverScale : 1f) * pressDip;
            _scaleK = Mathf.Lerp(_scaleK, targetK, k);
            transform.localScale = _baseScale * _scaleK;

            if (highlight != null)
            {
                _curTint = Color.Lerp(_curTint, _hovered ? hoverTint : baseTint, k);
                highlight.GetPropertyBlock(_mpb);
                _mpb.SetColor("_Color", _curTint);     // Sprites/Default / Unlit
                _mpb.SetColor("_BaseColor", _curTint); // URP Lit/Unlit
                highlight.SetPropertyBlock(_mpb);
            }
        }
    }
}
