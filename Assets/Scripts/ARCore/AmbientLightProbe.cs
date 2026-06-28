using UnityEngine;
using UnityEngine.XR.ARFoundation;
using SuperRealEstate.UI;

namespace SuperRealEstate.ARCore
{
    /// <summary>
    /// Reads AR light estimation from the camera and feeds the room's ambient
    /// luminance into <see cref="AmbientLight"/>, so spatial panels adapt their
    /// density to a dim vs. sun-filled room (<see cref="PaletteAdapt"/>). Uses
    /// <c>averageBrightness</c> when the platform provides it, else derives a value
    /// from <c>averageIntensityInLumens</c>. Smoothed so the UI doesn't flicker as
    /// the user turns toward a window.
    ///
    /// Editor setup: enable Light Estimation on the AR Camera Manager. Without a
    /// probe (tests / unsupported devices) the palette uses a neutral mid value.
    /// </summary>
    public sealed class AmbientLightProbe : MonoBehaviour
    {
        [Tooltip("Camera manager providing light estimation. Auto-found if empty.")]
        [SerializeField] private ARCameraManager cameraManager;

        [Tooltip("Lumens that map to full-bright (1.0) when only intensity is available.")]
        [SerializeField] private float lumensAtFullBright = 1200f;

        [Tooltip("Smoothing time (s); higher = steadier, slower to react.")]
        [SerializeField] private float smoothSeconds = 0.6f;

        private float _smoothed = 0.5f;
        private bool _seeded;

        private void Reset() => cameraManager = FindManager();
        private void Awake() { if (cameraManager == null) cameraManager = FindManager(); }

        private ARCameraManager FindManager()
        {
            ARCameraManager found = GetComponent<ARCameraManager>();
#if UNITY_2023_1_OR_NEWER
            return found != null ? found : FindAnyObjectByType<ARCameraManager>();
#else
            return found != null ? found : FindObjectOfType<ARCameraManager>();
#endif
        }

        private void OnEnable() { if (cameraManager != null) cameraManager.frameReceived += OnFrame; }
        private void OnDisable() { if (cameraManager != null) cameraManager.frameReceived -= OnFrame; }

        private void OnFrame(ARCameraFrameEventArgs args)
        {
            float? ambient = ReadAmbient(args);
            if (!ambient.HasValue) return;

            float target = Mathf.Clamp01(ambient.Value);
            if (!_seeded) { _smoothed = target; _seeded = true; }
            else
            {
                float k = 1f - Mathf.Exp(-Time.deltaTime / Mathf.Max(0.0001f, smoothSeconds));
                _smoothed = Mathf.Lerp(_smoothed, target, k);
            }

            AmbientLight.Set(_smoothed);
        }

        private float? ReadAmbient(ARCameraFrameEventArgs args)
        {
            // Prefer the normalized brightness when present.
            if (args.lightEstimation.averageBrightness.HasValue)
                return args.lightEstimation.averageBrightness.Value;

            // Else map lumens onto [0,1].
            if (args.lightEstimation.averageIntensityInLumens.HasValue && lumensAtFullBright > 0f)
                return args.lightEstimation.averageIntensityInLumens.Value / lumensAtFullBright;

            return null;
        }
    }
}
