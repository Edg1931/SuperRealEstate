#if UNITY_ANDROID && !UNITY_EDITOR
using System;
using UnityEngine;

namespace SuperRealEstate.ARCore
{
    // Real Android speech backends (Galaxy XR / Android XR / Android phones) over
    // JNI. Compiled only into the Android player. Standard patterns, but the JNI
    // surface (signatures, the RECOGNITION listener, the UI-thread requirement)
    // is worth a quick on-device verification; it's isolated here so it can't
    // affect the Editor or other platforms.
    //
    // Manifest requirements for the Android build:
    //   <uses-permission android:name="android.permission.RECORD_AUDIO"/>
    //   (and request the runtime permission before listening)

    /// <summary>Marshals an <see cref="Action"/> onto the Android UI thread.</summary>
    internal sealed class JavaRunnable : AndroidJavaProxy
    {
        private readonly Action _action;
        public JavaRunnable(Action action) : base("java.lang.Runnable") { _action = action; }
        public void run()
        {
            try { _action?.Invoke(); }
            catch (Exception e) { Debug.LogWarning($"[JavaRunnable] {e.Message}"); }
        }
    }

    /// <summary>Android TextToSpeech via JNI.</summary>
    internal sealed class AndroidTextToSpeech : ISpeechSynthesizer
    {
        private AndroidJavaObject _tts;
        private volatile bool _ready;

        public AndroidTextToSpeech()
        {
            using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
            _tts = new AndroidJavaObject("android.speech.tts.TextToSpeech", activity, new InitListener(() => _ready = true));
        }

        public bool IsSpeaking => _ready && _tts != null && _tts.Call<bool>("isSpeaking");

        public void Speak(string text)
        {
            if (_tts == null || string.IsNullOrEmpty(text)) return;
            // speak(CharSequence, int queueMode=QUEUE_FLUSH(0), Bundle params=null, String utteranceId)
            _tts.Call<int>("speak", text, 0, (AndroidJavaObject)null, "sre");
        }

        public void Stop() { _tts?.Call<int>("stop"); }

        private sealed class InitListener : AndroidJavaProxy
        {
            private readonly Action _onReady;
            public InitListener(Action onReady) : base("android.speech.tts.TextToSpeech$OnInitListener") { _onReady = onReady; }
            public void onInit(int status) { if (status == 0) _onReady?.Invoke(); } // SUCCESS == 0
        }
    }

    /// <summary>Android SpeechRecognizer via JNI (created + driven on the UI thread).</summary>
    internal sealed class AndroidSpeechRecognizer : ISpeechRecognizer
    {
        private readonly AndroidJavaObject _activity;
        private AndroidJavaObject _recognizer;
        private AndroidJavaObject _intent;

        public bool IsListening { get; private set; }
        public event Action<string> OnResult;
        public event Action<string> OnPartial;
        public event Action<string> OnError;

        public AndroidSpeechRecognizer()
        {
            using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            _activity = player.GetStatic<AndroidJavaObject>("currentActivity");

            RunOnUi(() =>
            {
                using var sr = new AndroidJavaClass("android.speech.SpeechRecognizer");
                _recognizer = sr.CallStatic<AndroidJavaObject>("createSpeechRecognizer", _activity);
                _recognizer.Call("setRecognitionListener", new Listener(this));

                _intent = new AndroidJavaObject("android.content.Intent", "android.speech.action.RECOGNIZE_SPEECH");
                _intent.Call<AndroidJavaObject>("putExtra", "android.speech.extra.LANGUAGE_MODEL", "free_form");
                _intent.Call<AndroidJavaObject>("putExtra", "android.speech.extra.PARTIAL_RESULTS", true);
            });
        }

        public void StartListening()
        {
            IsListening = true;
            RunOnUi(() => { if (_recognizer != null && _intent != null) _recognizer.Call("startListening", _intent); });
        }

        public void StopListening()
        {
            IsListening = false;
            RunOnUi(() => _recognizer?.Call("stopListening"));
        }

        private void RunOnUi(Action a) => _activity.Call("runOnUiThread", new JavaRunnable(a));

        internal void EmitResult(string text) { IsListening = false; if (!string.IsNullOrEmpty(text)) OnResult?.Invoke(text); }
        internal void EmitPartial(string text) { if (!string.IsNullOrEmpty(text)) OnPartial?.Invoke(text); }
        internal void EmitError(string err) { IsListening = false; OnError?.Invoke(err); }

        private sealed class Listener : AndroidJavaProxy
        {
            private readonly AndroidSpeechRecognizer _owner;
            public Listener(AndroidSpeechRecognizer owner) : base("android.speech.RecognitionListener") { _owner = owner; }

            // Handle every RecognitionListener callback in one place; ignore the
            // high-frequency ones (onRmsChanged, etc.) instead of logging per call.
            public override AndroidJavaObject Invoke(string methodName, AndroidJavaObject[] javaArgs)
            {
                try
                {
                    switch (methodName)
                    {
                        case "onResults": _owner.EmitResult(FirstHypothesis(javaArgs)); break;
                        case "onPartialResults": _owner.EmitPartial(FirstHypothesis(javaArgs)); break;
                        case "onError": _owner.EmitError("speech recognition error"); break;
                    }
                }
                catch (Exception e) { Debug.LogWarning($"[AndroidSTT] {e.Message}"); }
                return null;
            }

            private static string FirstHypothesis(AndroidJavaObject[] args)
            {
                if (args == null || args.Length == 0 || args[0] == null) return null;
                using var list = args[0].Call<AndroidJavaObject>("getStringArrayList", "results_recognition");
                if (list == null || list.Call<int>("size") == 0) return null;
                return list.Call<string>("get", 0);
            }
        }
    }
}
#endif
