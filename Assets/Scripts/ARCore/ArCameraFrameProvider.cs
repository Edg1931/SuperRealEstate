using System;
using Unity.Collections;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace SuperRealEstate.ARCore
{
    /// <summary>
    /// Captures a JPEG snapshot of the latest AR camera frame so the cloud
    /// analysis features (plant identification, surface-finish recognition) have
    /// an image to send. This is the one remaining code adapter wiring the AR
    /// camera to <see cref="SceneAppActions"/>: its <see cref="CaptureJpeg"/>
    /// method is handed to <c>SceneAppActions.Configure(..., frameProvider)</c>
    /// as the <see cref="Func{TResult}"/> source.
    ///
    /// It uses AR Foundation's CPU image path (<see cref="ARCameraManager"/> →
    /// <c>TryAcquireLatestCpuImage</c> → <c>Convert</c> → <c>EncodeToJPG</c>),
    /// which works the same on ARCore (Android phones), Android XR (Galaxy XR),
    /// and ARKit/visionOS — wherever the platform exposes the passthrough camera.
    /// On headsets that gate camera access behind a permission/enterprise API the
    /// acquire simply fails and we return <c>null</c>; callers already treat a
    /// null/empty frame as "point the camera and try again" rather than an error.
    ///
    /// Editor setup:
    ///  - Put this on the same object as (or assign) the <see cref="ARCameraManager"/>
    ///    that lives on the XR Origin's Main Camera.
    ///  - The bootstrap layer (<see cref="RealEstateApp"/>) passes
    ///    <see cref="CaptureJpeg"/> into <see cref="SceneAppActions.Configure"/>.
    /// </summary>
    public sealed class ArCameraFrameProvider : MonoBehaviour
    {
        [Tooltip("Camera manager on the XR Origin's Main Camera. Auto-found if left empty.")]
        [SerializeField] private ARCameraManager cameraManager;

        [Tooltip("JPEG quality (1–100). 80 is a good size/quality tradeoff for upload.")]
        [Range(1, 100)]
        [SerializeField] private int jpegQuality = 80;

        [Tooltip("Longest output edge in pixels; the frame is downscaled to fit. " +
                 "Smaller = faster upload, still plenty for recognition.")]
        [SerializeField] private int maxDimension = 1024;

        // Reused across captures to avoid per-frame GC churn.
        private Texture2D _texture;

        private void Reset() => cameraManager = FindCameraManager();
        private void Awake() { if (cameraManager == null) cameraManager = FindCameraManager(); }

        private ARCameraManager FindCameraManager()
        {
            ARCameraManager found = GetComponent<ARCameraManager>();
#if UNITY_2023_1_OR_NEWER
            return found != null ? found : FindAnyObjectByType<ARCameraManager>();
#else
            return found != null ? found : FindObjectOfType<ARCameraManager>();
#endif
        }

        /// <summary>
        /// Acquire the latest camera frame and encode it as JPEG bytes. Returns
        /// <c>null</c> when no frame is available (camera not ready, permission
        /// not granted, or the platform doesn't expose passthrough frames). Safe
        /// to call from a <see cref="Func{TResult}"/> on the main thread.
        /// </summary>
        public byte[] CaptureJpeg()
        {
            if (cameraManager == null)
                return null;

            if (!cameraManager.TryAcquireLatestCpuImage(out XRCpuImage image))
                return null;

            try
            {
                return Encode(image);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ArCameraFrameProvider] frame capture failed: {e.Message}");
                return null;
            }
            finally
            {
                image.Dispose();
            }
        }

        private byte[] Encode(XRCpuImage image)
        {
            Vector2Int outSize = OutputDimensions(image.width, image.height);

            var conversionParams = new XRCpuImage.ConversionParams
            {
                inputRect = new RectInt(0, 0, image.width, image.height),
                outputDimensions = outSize,
                outputFormat = TextureFormat.RGBA32,
                // Camera image rows are top-down relative to Texture2D's bottom-up
                // convention; mirror Y so the encoded JPEG is upright.
                transformation = XRCpuImage.Transformation.MirrorY,
            };

            int size = image.GetConvertedDataSize(conversionParams);
            var buffer = new NativeArray<byte>(size, Allocator.Temp);
            try
            {
                image.Convert(conversionParams, buffer);

                EnsureTexture(outSize.x, outSize.y);
                _texture.LoadRawTextureData(buffer);
                _texture.Apply(false);
                return _texture.EncodeToJPG(jpegQuality);
            }
            finally
            {
                buffer.Dispose();
            }
        }

        private Vector2Int OutputDimensions(int width, int height)
        {
            int longest = Mathf.Max(width, height);
            if (maxDimension <= 0 || longest <= maxDimension)
                return new Vector2Int(width, height);

            float scale = (float)maxDimension / longest;
            int w = Mathf.Max(1, Mathf.RoundToInt(width * scale));
            int h = Mathf.Max(1, Mathf.RoundToInt(height * scale));
            return new Vector2Int(w, h);
        }

        private void EnsureTexture(int width, int height)
        {
            if (_texture != null && _texture.width == width && _texture.height == height)
                return;

            if (_texture != null)
                Destroy(_texture);

            _texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        }

        private void OnDestroy()
        {
            if (_texture != null)
                Destroy(_texture);
        }
    }
}
