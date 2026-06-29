#if (UNITY_IOS || UNITY_VISIONOS) && !UNITY_EDITOR
using System;
using System.Runtime.InteropServices;
using AOT;
using UnityEngine;

namespace SuperRealEstate.ARCore
{
    /// <summary>
    /// iOS / visionOS speech backend bridging to the native SFSpeechRecognizer +
    /// AVSpeechSynthesizer plugin (Assets/Plugins/iOS/SRESpeech.mm). One shared
    /// instance serves both <see cref="ISpeechRecognizer"/> and
    /// <see cref="ISpeechSynthesizer"/> (the native side is a singleton). Native
    /// recognition callbacks arrive on a background queue — they raise the events
    /// directly and <c>VoiceCaptureController</c> marshals them to the main thread.
    ///
    /// Compiled only into the Apple players. Needs Speech.framework +
    /// AVFoundation linked and the Info.plist usage strings (see the .mm header).
    /// </summary>
    internal sealed class AppleSpeech : ISpeechRecognizer, ISpeechSynthesizer
    {
        private delegate void SpeechCallback(IntPtr utf8);

        [DllImport("__Internal")] private static extern void _sreSpeechSetCallbacks(SpeechCallback onResult, SpeechCallback onPartial, SpeechCallback onError);
        [DllImport("__Internal")] private static extern void _sreSpeechRequestAuth();
        [DllImport("__Internal")] private static extern void _sreSpeechStart();
        [DllImport("__Internal")] private static extern void _sreSpeechStop();
        [DllImport("__Internal")] private static extern void _sreTtsSpeak([MarshalAs(UnmanagedType.LPUTF8Str)] string text);
        [DllImport("__Internal")] private static extern void _sreTtsStop();
        [DllImport("__Internal")] private static extern bool _sreTtsIsSpeaking();

        private static AppleSpeech s_instance;

        /// <summary>The shared backend (one native singleton for STT + TTS).</summary>
        public static AppleSpeech Instance => s_instance ??= new AppleSpeech();

        public bool IsListening { get; private set; }
        public event Action<string> OnResult;
        public event Action<string> OnPartial;
        public event Action<string> OnError;

        private AppleSpeech()
        {
            _sreSpeechSetCallbacks(ResultCb, PartialCb, ErrorCb);
            _sreSpeechRequestAuth();
        }

        public void StartListening() { IsListening = true; _sreSpeechStart(); }
        public void StopListening() { IsListening = false; _sreSpeechStop(); }

        public bool IsSpeaking => _sreTtsIsSpeaking();
        public void Speak(string text) { if (!string.IsNullOrEmpty(text)) _sreTtsSpeak(text); }
        public void Stop() => _sreTtsStop();

        // --- native → managed callbacks (static; IL2CPP-safe) ---

        [MonoPInvokeCallback(typeof(SpeechCallback))]
        private static void ResultCb(IntPtr p) => s_instance?.RaiseResult(Str(p));

        [MonoPInvokeCallback(typeof(SpeechCallback))]
        private static void PartialCb(IntPtr p) => s_instance?.RaisePartial(Str(p));

        [MonoPInvokeCallback(typeof(SpeechCallback))]
        private static void ErrorCb(IntPtr p) => s_instance?.RaiseError(Str(p));

        private void RaiseResult(string s) { IsListening = false; if (!string.IsNullOrEmpty(s)) OnResult?.Invoke(s); }
        private void RaisePartial(string s) { if (!string.IsNullOrEmpty(s)) OnPartial?.Invoke(s); }
        private void RaiseError(string s) { IsListening = false; OnError?.Invoke(string.IsNullOrEmpty(s) ? "speech error" : s); }

        private static string Str(IntPtr p) => p == IntPtr.Zero ? null : Marshal.PtrToStringUTF8(p);
    }
}
#endif
