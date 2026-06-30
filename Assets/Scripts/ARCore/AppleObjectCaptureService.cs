#if (UNITY_IOS || UNITY_VISIONOS) && !UNITY_EDITOR
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using AOT;
using UnityEngine;
using SuperRealEstate.Capture;

namespace SuperRealEstate.ARCore
{
    /// <summary>
    /// On-device furniture reconstruction for iOS / visionOS via Apple Object
    /// Capture (RealityKit PhotogrammetrySession), bridged by
    /// Assets/Plugins/iOS/SREObjectCapture.swift. The highest-quality, fully
    /// private path — photos never leave the device. Writes the captured JPEGs to
    /// a temp folder, runs the native reconstruct (async), and returns the USDZ.
    /// The metric scale comes from the AR-measured bounds we captured (the model
    /// is then placed/fit-checked to true size). Compiled only into the Apple
    /// players. Gated by <see cref="Supported"/> (Object Capture is hardware-gated).
    ///
    /// Threading: the native callback fires off-thread, but this is awaited from
    /// the Unity main thread, so the continuation resumes there via Unity's
    /// SynchronizationContext.
    /// </summary>
    internal sealed class AppleObjectCaptureService : IFurnitureCaptureService
    {
        private delegate void OcCallback(bool ok, IntPtr utf8);

        [DllImport("__Internal")] private static extern void _sreObjectCaptureSetCallback(OcCallback cb);
        [DllImport("__Internal")] private static extern bool _sreObjectCaptureSupported();
        [DllImport("__Internal")] private static extern void _sreObjectCaptureReconstruct(
            [MarshalAs(UnmanagedType.LPUTF8Str)] string inputDir,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string outputPath);

        // One in-flight reconstruction at a time (the native session is a singleton).
        private static TaskCompletionSource<(bool ok, string message)> s_pending;

        public CaptureMethod Method => CaptureMethod.ObjectCapture;

        /// <summary>True when this device supports on-device Object Capture (iOS 17+, capable hardware).</summary>
        public static bool Supported
        {
            get { try { return _sreObjectCaptureSupported(); } catch { return false; } }
        }

        public async Task<FurnitureCaptureResult> ReconstructAsync(CaptureSubmission submission, CancellationToken ct = default)
        {
            string inputDir = Path.Combine(Application.temporaryCachePath, "capture-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(inputDir);
            var photos = submission.Photos;
            for (int i = 0; i < photos.Count; i++)
            {
                if (photos[i] == null || photos[i].Length == 0) continue;
                File.WriteAllBytes(Path.Combine(inputDir, $"img_{i:000}.jpg"), photos[i]);
            }
            string output = Path.Combine(Application.temporaryCachePath, "model-" + Guid.NewGuid().ToString("N") + ".usdz");

            s_pending = new TaskCompletionSource<(bool, string)>();
            _sreObjectCaptureSetCallback(OnReconstructed);
            _sreObjectCaptureReconstruct(inputDir, output);

            using (ct.Register(() => s_pending?.TrySetResult((false, "cancelled"))))
            {
                (bool ok, string message) = await s_pending.Task; // resumes on the Unity main thread
                if (!ok) throw new Exception($"Object Capture failed: {message}");
            }

            CaptureBounds bounds = submission.MetricBounds ?? new CaptureBounds(1f, 1f, 1f);
            return new FurnitureCaptureResult
            {
                AssetId = Guid.NewGuid().ToString("N"),
                Name = submission.Name,
                Method = CaptureMethod.ObjectCapture,
                Bounds = bounds,
                ModelUrl = "file://" + output, // on-device USDZ
            };
        }

        [MonoPInvokeCallback(typeof(OcCallback))]
        private static void OnReconstructed(bool ok, IntPtr utf8)
        {
            string message = utf8 == IntPtr.Zero ? null : Marshal.PtrToStringUTF8(utf8);
            s_pending?.TrySetResult((ok, message));
        }
    }
}
#endif
