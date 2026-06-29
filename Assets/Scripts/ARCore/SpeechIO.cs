using System;
using UnityEngine;

namespace SuperRealEstate.ARCore
{
    /// <summary>
    /// On-device speech-to-text. Implementations are platform-specific
    /// (Android XR / Android via the system recognizer; iOS/visionOS via a native
    /// plugin). Events may fire off the main thread — consumers marshal as needed.
    /// </summary>
    public interface ISpeechRecognizer
    {
        bool IsListening { get; }
        void StartListening();
        void StopListening();

        /// <summary>A final recognized utterance.</summary>
        event Action<string> OnResult;
        /// <summary>An interim hypothesis (may revise).</summary>
        event Action<string> OnPartial;
        /// <summary>A recognition error (human-readable).</summary>
        event Action<string> OnError;
    }

    /// <summary>On-device text-to-speech (speaks the agent's reply).</summary>
    public interface ISpeechSynthesizer
    {
        bool IsSpeaking { get; }
        void Speak(string text);
        void Stop();
    }

    /// <summary>
    /// A recognizer with no platform backend: feed it transcripts directly via
    /// <see cref="Feed"/> (a debug HUD field, a keyboard, or a test). Lets the
    /// whole voice loop (transcript → agent → action → reply) run in the Editor
    /// and on any platform before a native recognizer is wired. Fires on the
    /// calling thread.
    /// </summary>
    public sealed class ManualSpeechRecognizer : ISpeechRecognizer
    {
        public bool IsListening { get; private set; }
        public event Action<string> OnResult;
        public event Action<string> OnPartial;
        public event Action<string> OnError;

        public void StartListening() => IsListening = true;
        public void StopListening() => IsListening = false;

        /// <summary>Inject a final transcript (e.g. from a debug field).</summary>
        public void Feed(string text)
        {
            IsListening = false;
            if (!string.IsNullOrWhiteSpace(text)) OnResult?.Invoke(text);
        }

        public void FeedPartial(string text)
        {
            if (!string.IsNullOrWhiteSpace(text)) OnPartial?.Invoke(text);
        }
    }

    /// <summary>A synthesizer that logs instead of speaking (Editor / unsupported platforms).</summary>
    public sealed class NullSpeechSynthesizer : ISpeechSynthesizer
    {
        public bool IsSpeaking => false;
        public void Speak(string text) { if (!string.IsNullOrEmpty(text)) Debug.Log($"[TTS] {text}"); }
        public void Stop() { }
    }

    /// <summary>Picks the best speech backend for the current platform.</summary>
    public static class SpeechIO
    {
        public static ISpeechRecognizer CreateRecognizer()
        {
#if (UNITY_IOS || UNITY_VISIONOS) && !UNITY_EDITOR
            try { return AppleSpeech.Instance; }
            catch (Exception e) { Debug.LogWarning($"[SpeechIO] Apple recognizer unavailable: {e.Message}"); return new ManualSpeechRecognizer(); }
#elif UNITY_ANDROID && !UNITY_EDITOR
            try { return new AndroidSpeechRecognizer(); }
            catch (Exception e) { Debug.LogWarning($"[SpeechIO] Android recognizer unavailable: {e.Message}"); return new ManualSpeechRecognizer(); }
#else
            // Editor / unsupported: the manual recognizer keeps the loop testable
            // (VoiceCaptureController.FeedTranscript).
            return new ManualSpeechRecognizer();
#endif
        }

        public static ISpeechSynthesizer CreateSynthesizer()
        {
#if (UNITY_IOS || UNITY_VISIONOS) && !UNITY_EDITOR
            try { return AppleSpeech.Instance; }
            catch (Exception e) { Debug.LogWarning($"[SpeechIO] Apple TTS unavailable: {e.Message}"); return new NullSpeechSynthesizer(); }
#elif UNITY_ANDROID && !UNITY_EDITOR
            try { return new AndroidTextToSpeech(); }
            catch (Exception e) { Debug.LogWarning($"[SpeechIO] Android TTS unavailable: {e.Message}"); return new NullSpeechSynthesizer(); }
#else
            return new NullSpeechSynthesizer();
#endif
        }
    }
}
