using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using SuperRealEstate.App;
using SuperRealEstate.Platform;
using SuperRealEstate.UI;
using TMPro;

namespace SuperRealEstate.ARCore
{
    /// <summary>
    /// The core navigation chrome: a wrist-summoned radial tool palette
    /// (Measure / Identify / Finishes / Stage / Remove wall / Notes / Landscape),
    /// built at runtime from the <see cref="RadialToolMenu"/> model with
    /// <see cref="SpatialButtonBuilder"/>. Items the current device can't do are
    /// dimmed (resolved through <see cref="ActionDispatcher.ResolveTool"/> +
    /// <see cref="XrCapabilities"/>) so e.g. wall-removal portals read as
    /// unavailable on a phone instead of failing. Selecting a tool raises
    /// <see cref="OnToolSelected"/>; selecting a blocked tool raises
    /// <see cref="OnToolBlocked"/> with the reason.
    ///
    /// The menu billboards to face the user (calm yaw-follow) and seats in the
    /// comfort zone when shown. Summon/dismiss with <see cref="Show"/>/<see cref="Hide"/>.
    /// </summary>
    public sealed class RadialToolMenuView : MonoBehaviour
    {
        [SerializeField] private Transform anchor;
        [SerializeField] private TMP_FontAsset font;
        [Tooltip("Device profile used to dim unavailable tools. Match RealEstateApp.")]
        [SerializeField] private XrPlatform targetPlatform = XrPlatform.AndroidXrHeadset;
        [Tooltip("Gesture/button that summons the palette (palm-up, menu button, controller).")]
        [SerializeField] private InputActionProperty summonAction;

        [SerializeField] private float radiusM = 0.16f;
        [SerializeField] private float arcDegrees = 160f;
        [SerializeField] private float buttonWidthM = 0.18f;
        [SerializeField] private float buttonHeightM = 0.07f;

        [Serializable] public sealed class ToolEvent : UnityEvent<ToolId> { }
        [Serializable] public sealed class StringEvent : UnityEvent<string> { }

        [Tooltip("Raised when an available tool is selected.")]
        public ToolEvent OnToolSelected = new ToolEvent();

        [Tooltip("Raised (with a reason) when a tool unavailable on this device is selected.")]
        public StringEvent OnToolBlocked = new StringEvent();

        private readonly RadialToolMenu _model = new RadialToolMenu();
        private GameObject _root;
        private bool _visible;

        private void Awake() { if (anchor == null) anchor = transform; }

        private void OnEnable()
        {
            if (summonAction.action != null)
            {
                summonAction.action.performed += OnSummon;
                summonAction.action.Enable();
            }
        }

        private void OnDisable()
        {
            if (summonAction.action != null)
                summonAction.action.performed -= OnSummon;
        }

        private void OnSummon(InputAction.CallbackContext _) => Toggle();

        /// <summary>Is the palette currently shown?</summary>
        public bool IsVisible => _visible;

        /// <summary>Summon the palette (rebuilds it at the comfort zone, facing you).</summary>
        public void Show()
        {
            Build();
            _visible = true;
        }

        /// <summary>Dismiss the palette.</summary>
        public void Hide()
        {
            Clear();
            _visible = false;
        }

        /// <summary>Toggle the palette.</summary>
        public void Toggle() { if (_visible) Hide(); else Show(); }

        private void Build()
        {
            Clear();

            _root = new GameObject("RadialToolMenu");
            _root.transform.SetParent(anchor, worldPositionStays: false);
            _root.AddComponent<BillboardToUser>();

            XrCapabilities caps = XrCapabilityProfiles.For(targetPlatform);
            int n = _model.Items.Count;
            float sweep = Mathf.Deg2Rad * arcDegrees;
            float start = -sweep * 0.5f;
            float step = n > 1 ? sweep / (n - 1) : 0f;

            for (int i = 0; i < n; i++)
            {
                ToolMenuItem item = _model.Items[i];
                DispatchResult res = ActionDispatcher.ResolveTool(item.Tool, caps);
                bool available = res.Available;

                ToolId tool = item.Tool;
                string reason = res.Reason;
                Action onSelect = available
                    ? (Action)(() => OnToolSelected.Invoke(tool))
                    : () => OnToolBlocked.Invoke(string.IsNullOrEmpty(reason) ? $"{item.Label} isn't available on this device" : reason);

                // Dim unavailable tools (muted accent), full accent otherwise.
                Color accent = available ? DesignTokens.Accent : DesignTokens.OnSurfaceMuted;
                GameObject btn = SpatialButtonBuilder.Build(
                    $"tool.{tool}", item.Label, buttonWidthM, buttonHeightM, onSelect, font, accent);
                btn.transform.SetParent(_root.transform, worldPositionStays: false);

                float a = start + step * i;
                btn.transform.localPosition = new Vector3(Mathf.Sin(a) * radiusM, Mathf.Cos(a) * radiusM, -0.004f);
            }
        }

        private void Clear()
        {
            if (_root == null) return;
            if (Application.isPlaying) Destroy(_root); else DestroyImmediate(_root);
            _root = null;
        }

        private void OnDestroy() => Clear();
    }
}
