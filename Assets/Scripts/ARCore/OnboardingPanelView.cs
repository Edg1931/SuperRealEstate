using System;
using System.Collections.Generic;
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
        [Tooltip("Font for button/title text (optional).")]
        [SerializeField] private Font font;

        [SerializeField] private float panelWidthM = 0.62f;
        [SerializeField] private float buttonWidthM = 0.54f;
        [SerializeField] private float buttonHeightM = 0.08f;

        private GameObject _current;

        private void Awake() { if (anchor == null) anchor = transform; }

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

        private void BuildPanel(string title, IReadOnlyList<ButtonSpec> buttons)
        {
            float titleH = buttonHeightM * 1.1f;
            float pad = DesignTokens.SpaceM;
            float height = pad * 2f + titleH + buttons.Count * (buttonHeightM + DesignTokens.SpaceS);

            _current = SpatialPanelBuilder.Build(panelWidthM, height, "OnboardingPanel");
            _current.transform.SetParent(anchor, worldPositionStays: false);

            float top = height * 0.5f - pad;

            // Title (label-only mini panel, no collider).
            if (font != null)
                AddTitle(_current.transform, title, top - titleH * 0.5f);

            float y = top - titleH - DesignTokens.SpaceS - buttonHeightM * 0.5f;
            foreach (ButtonSpec b in buttons)
            {
                GameObject btn = SpatialButtonBuilder.Build(b.Id, b.Label, buttonWidthM, buttonHeightM, b.OnSelect, font);
                btn.transform.SetParent(_current.transform, worldPositionStays: false);
                btn.transform.localPosition = new Vector3(0f, y, -0.004f);
                y -= buttonHeightM + DesignTokens.SpaceS;
            }
        }

        private void AddTitle(Transform parent, string text, float localY)
        {
            var go = new GameObject("Title");
            go.transform.SetParent(parent, worldPositionStays: false);
            go.transform.localPosition = new Vector3(0f, localY, -0.003f);

            var tm = go.AddComponent<TextMesh>();
            tm.text = text;
            tm.font = font;
            tm.fontSize = 64;
            tm.characterSize = buttonHeightM * 0.022f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = DesignTokens.OnSurface;

            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null && font.material != null) mr.sharedMaterial = font.material;
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
