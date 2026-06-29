using System;
using System.Collections.Concurrent;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

namespace SuperRealEstate.ARCore
{
    /// <summary>
    /// Closes the voice loop: mic → speech-to-text → <see cref="VoiceCommandController"/>
    /// (agent → action) → speech synthesis of the reply. Push-to-talk by default
    /// (hold a button / pinch) so it isn't always listening. Picks the platform
    /// speech backend via <see cref="SpeechIO"/> — a real recognizer/synth on
    /// Android (Galaxy XR / phones), the manual/log fallback elsewhere so the whole
    /// loop is exercisable in the Editor (use <see cref="FeedTranscript"/>).
    ///
    /// Threading: native recognizer callbacks can arrive off Unity's main thread,
    /// but the voice pipeline uses UnityWebRequest (main-thread only), so results
    /// are queued and dispatched in <see cref="Update"/>.
    /// </summary>
    public sealed class VoiceCaptureController : MonoBehaviour
    {
        [SerializeField] private VoiceCommandController voice;
        [Tooltip("Push-to-talk: hold to listen. Bind to a controller button / pinch-hold.")]
        [SerializeField] private InputActionProperty pushToTalk;
        [Tooltip("Speak the agent's reply with TTS.")]
        [SerializeField] private bool speakReplies = true;

        [Serializable] public sealed class StringEvent : UnityEvent<string> { }
        [Serializable] public sealed class BoolEvent : UnityEvent<bool> { }

        [Tooltip("Interim hypothesis while speaking (for a live caption).")]
        public StringEvent OnPartialTranscript = new StringEvent();
        [Tooltip("Final recognized utterance (also sent to the agent).")]
        public StringEvent OnFinalTranscript = new StringEvent();
        [Tooltip("Listening on/off — drive a mic indicator.")]
        public BoolEvent OnListeningChanged = new BoolEvent();

        private ISpeechRecognizer _recognizer;
        private ISpeechSynthesizer _synth;
        private readonly ConcurrentQueue<string> _finals = new ConcurrentQueue<string>();
        private readonly ConcurrentQueue<string> _partials = new ConcurrentQueue<string>();
        private bool _wasListening;

        /// <summary>The active recognizer (e.g. to <c>Feed</c> a ManualSpeechRecognizer).</summary>
        public ISpeechRecognizer Recognizer => _recognizer;

        private void Awake()
        {
            _recognizer = SpeechIO.CreateRecognizer();
            _synth = SpeechIO.CreateSynthesizer();

            _recognizer.OnResult += HandleResult;
            _recognizer.OnPartial += HandlePartial;
            _recognizer.OnError += HandleError;

            if (voice != null && speakReplies) voice.OnReply.AddListener(Speak);
        }

        private void OnEnable()
        {
            if (pushToTalk.action != null)
            {
                pushToTalk.action.started += OnTalkStart;
                pushToTalk.action.canceled += OnTalkEnd;
                pushToTalk.action.Enable();
            }
        }

        private void OnDisable()
        {
            if (pushToTalk.action != null)
            {
                pushToTalk.action.started -= OnTalkStart;
                pushToTalk.action.canceled -= OnTalkEnd;
            }
        }

        private void OnDestroy()
        {
            if (_recognizer != null)
            {
                _recognizer.OnResult -= HandleResult;
                _recognizer.OnPartial -= HandlePartial;
                _recognizer.OnError -= HandleError;
            }
            if (voice != null) voice.OnReply.RemoveListener(Speak);
        }

        private void OnTalkStart(InputAction.CallbackContext _) => StartListening();
        private void OnTalkEnd(InputAction.CallbackContext _) => StopListening();

        /// <summary>Begin recognizing speech (call on press / mic button).</summary>
        public void StartListening() => _recognizer?.StartListening();

        /// <summary>Stop recognizing (call on release).</summary>
        public void StopListening() => _recognizer?.StopListening();

        /// <summary>Feed a transcript directly (debug HUD / keyboard / test) — runs the full loop.</summary>
        public void FeedTranscript(string text)
        {
            if (!string.IsNullOrWhiteSpace(text)) _finals.Enqueue(text);
        }

        // Recognizer callbacks may be off-thread → enqueue, drain on the main thread.
        private void HandleResult(string text) { if (!string.IsNullOrWhiteSpace(text)) _finals.Enqueue(text); }
        private void HandlePartial(string text) { if (!string.IsNullOrWhiteSpace(text)) _partials.Enqueue(text); }
        private void HandleError(string err) => Debug.LogWarning($"[VoiceCapture] {err}");

        private void Update()
        {
            while (_partials.TryDequeue(out string p)) OnPartialTranscript.Invoke(p);
            while (_finals.TryDequeue(out string f))
            {
                OnFinalTranscript.Invoke(f);
                voice?.Submit(f); // → agent → dispatcher → action (main thread)
            }

            bool listening = _recognizer != null && _recognizer.IsListening;
            if (listening != _wasListening)
            {
                _wasListening = listening;
                OnListeningChanged.Invoke(listening);
            }
        }

        private void Speak(string text)
        {
            if (speakReplies) _synth?.Speak(text);
        }
    }
}
