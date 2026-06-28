using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using SuperRealEstate.ARRender;
using SuperRealEstate.Onboarding;
using SuperRealEstate.UI;

namespace SuperRealEstate.ARCore
{
    /// <summary>
    /// The onboarding VIEW: turns <see cref="OnboardingController"/>'s per-step
    /// events into floating spatial panels with buttons (built procedurally from
    /// the design system via <see cref="SpatialPanelBuilder"/> /
    /// <see cref="SpatialButtonBuilder"/>). Buttons call back into the controller
    /// (grant consent, advance, skip), so the flow logic stays in the tested
    /// controller and this is pure presentation. Swap in authored prefabs later
    /// by replacing the builders — the controller wiring is unchanged.
    ///
    /// Assign a <see cref="font"/> to label buttons (optional; without it the
    /// panels still build, just unlabeled). Tune the meter sizes for your rig.
    /// </summary>
    public sealed class OnboardingPanelView : MonoBehaviour
    {
        [SerializeField] private OnboardingController controller;
        [Tooltip("Where the panel floats. Defaults to this transform.")]
        [SerializeField] private Transform anchor;
        [Tooltip("TextMeshPro font for button/title text (optional; uses TMP default).")]
        [SerializeField] private TMP_FontAsset font;

        [SerializeField] private float panelWidthM = 0.62f;
        [SerializeField] private float buttonWidthM = 0.54f;
        [SerializeField] private float buttonHeightM = 0.08f;

        private GameObject _current;
        private Transform _root; // persistent billboarded container (seated once)

        private void Awake() { if (anchor == null) anchor = transform; }

        private Transform Root()
        {
            if (_root != null) return _root;
            var go = new GameObject("OnboardingPanelRoot");
            go.transform.SetParent(anchor, worldPositionStays: false);
            go.AddComponent<BillboardToUser>(); // seats in the comfort zone once, then faces the user
            _root = go.transform;
            return _root;
        }

        private void OnEnable()
        {
            if (controller == null) return;
            controller.OnStep.AddListener(BuildForStep);
            controller.OnCompleted.AddListener(Clear);
        }

        private void OnDisable()
        {
            if (controller == null) return;
            controller.OnStep.RemoveListener(BuildForStep);
            controller.OnCompleted.RemoveListener(Clear);
        }

        private void BuildForStep(OnboardingStep step)
        {
            Clear();

            switch (step)
            {
                case OnboardingStep.Welcome:
                    BuildPanel("Welcome to SuperRealEstate", new[]
                    {
                        Btn("welcome.start", "Get started", () => controller.Next()),
                    });
                    break;

                case OnboardingStep.Consent:
                    BuildPanel("Allow access", new[]
                    {
                        Btn("consent.camera", "Allow camera", () => controller.GrantCamera()),
                        Btn("consent.scan", "Allow room scanning", () => controller.GrantSceneScan()),
                        Btn("consent.mic", "Allow microphone", () => controller.GrantMicrophone()),
                        Btn("consent.continue", "Continue", () => controller.Next()),
                    });
                    break;

                case OnboardingStep.SignIn:
                    BuildPanel("Sign in (optional)", new[]
                    {
                        Btn("signin.skip", "Continue without an account", () => controller.SkipSignIn()),
                    });
                    break;

                case OnboardingStep.ReadyToScan:
                    BuildPanel("You're all set", new[]
                    {
                        Btn("ready.start", "Start", () => controller.Next()),
                    });
                    break;

                case OnboardingStep.Done:
                    Clear();
                    break;
            }
        }

        // Inter-target gap sized so eye-gaze targets clear the minimum angular
        // spacing (~1°) at the sweet depth — SpaceS alone is too tight to gaze.
        private static readonly float Gap =
            SpatialComfort.AngularToMeters(SpatialComfort.MinTargetSpacingDeg * 1.6f, SpatialComfort.DepthSweetM);

        private void BuildPanel(string title, IReadOnlyList<ButtonSpec> buttons)
        {
            float titleH = buttonHeightM * 1.1f;
            float pad = DesignTokens.SpaceL;
            float height = pad * 2f + titleH + buttons.Count * (buttonHeightM + Gap);

            _current = SpatialPanelBuilder.Build(panelWidthM, height, "OnboardingPanel");
            _current.transform.SetParent(Root(), worldPositionStays: false);
            _current.transform.localPosition = Vector3.zero; // billboarded root handles facing/placement

            float top = height * 0.5f - pad;

            // Title (TMP label; uses the TMP default font when none assigned).
            TextMeshPro titleLabel = SpatialLabel.Build(
                _current.transform, title, font,
                DesignTokens.TypeTitleDeg, SpatialComfort.DepthSweetM, AmbientLight.Palette.OnSurface);
            titleLabel.transform.localPosition = new Vector3(0f, top - titleH * 0.5f, -0.003f);

            float y = top - titleH - Gap - buttonHeightM * 0.5f;
            foreach (ButtonSpec b in buttons)
            {
                GameObject btn = SpatialButtonBuilder.Build(b.Id, b.Label, buttonWidthM, buttonHeightM, b.OnSelect, font);
                btn.transform.SetParent(_current.transform, worldPositionStays: false);
                btn.transform.localPosition = new Vector3(0f, y, -0.004f);
                y -= buttonHeightM + Gap;
            }
        }

        private void Clear()
        {
            if (_current == null) return;
            if (Application.isPlaying) Destroy(_current); else DestroyImmediate(_current);
            _current = null;
        }

        private static ButtonSpec Btn(string id, string label, Action onSelect) => new ButtonSpec(id, label, onSelect);

        private readonly struct ButtonSpec
        {
            public readonly string Id;
            public readonly string Label;
            public readonly Action OnSelect;
            public ButtonSpec(string id, string label, Action onSelect) { Id = id; Label = label; OnSelect = onSelect; }
        }
    }
}
