using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;
using SuperRealEstate.App;
using SuperRealEstate.Onboarding;
using SuperRealEstate.Platform;
using SuperRealEstate.RoomMeasure;
using SuperRealEstate.Voice;

namespace SuperRealEstate.ARCore
{
    /// <summary>
    /// The voice-agent pipeline, wired end to end: a recognized transcript →
    /// <see cref="IVoiceAgent"/> (the <c>voice-agent</c> Edge Function / Claude) →
    /// a spoken reply + an <see cref="VoiceAction"/> → <see cref="ActionDispatcher"/>
    /// (feature-gated for this device) → the real scene action via
    /// <see cref="SceneAppActions"/>. So "how much mulch for this bed?", "what's
    /// this tree?", and "remove this wall" actually do something.
    ///
    /// Speech-to-text and text-to-speech are on-device/platform concerns: feed the
    /// recognized text into <see cref="SubmitAsync"/> and speak the
    /// <see cref="OnReply"/> string with the platform TTS. The agent gets context
    /// (last measurement, a camera frame, location) so it can answer "this".
    ///
    /// Wire-up: <see cref="RealEstateApp"/> builds the agent + dispatcher + device
    /// capabilities and calls <see cref="Configure"/>.
    /// </summary>
    public sealed class VoiceCommandController : MonoBehaviour
    {
        [Serializable] public sealed class StringEvent : UnityEvent<string> { }

        [Tooltip("The agent's spoken reply — speak this with platform TTS / show it.")]
        public StringEvent OnReply = new StringEvent();

        [Tooltip("Raised when an action was understood but blocked on this device (with the reason).")]
        public StringEvent OnActionBlocked = new StringEvent();

        [Tooltip("Raised when the voice turn failed (network/parse).")]
        public StringEvent OnError = new StringEvent();

        private IVoiceAgent _agent;
        private ActionDispatcher _dispatcher;
        private XrCapabilities _caps;
        private Func<byte[]> _frameProvider;
        private Func<RoomMeasurements?> _measurements;
        private Func<(double? lat, double? lng)> _location;
        private ConsentService _consent;

        /// <summary>
        /// Inject the pipeline. <paramref name="frameProvider"/>/<paramref name="measurements"/>/
        /// <paramref name="location"/> are optional context sources; <paramref name="consent"/>
        /// (optional) gates voice on Microphone consent.
        /// </summary>
        public void Configure(
            IVoiceAgent agent,
            ActionDispatcher dispatcher,
            XrCapabilities caps,
            Func<byte[]> frameProvider = null,
            Func<RoomMeasurements?> measurements = null,
            Func<(double?, double?)> location = null,
            ConsentService consent = null)
        {
            _agent = agent;
            _dispatcher = dispatcher;
            _caps = caps;
            _frameProvider = frameProvider;
            _measurements = measurements;
            _location = location;
            _consent = consent;
        }

        /// <summary>Fire-and-forget entry point for UI/UnityEvent hooks.</summary>
        public void Submit(string transcript) => _ = SubmitAsync(transcript);

        /// <summary>
        /// Run one voice turn: ask the agent, surface the reply, dispatch the action
        /// (gated for this device). Returns the agent's response (or null on error).
        /// </summary>
        public async Task<VoiceResponse> SubmitAsync(string transcript, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(transcript)) return null;
            if (_agent == null || _dispatcher == null)
            {
                OnError.Invoke("voice isn't set up");
                return null;
            }

            if (_consent != null && !_consent.IsAllowed(CaptureAction.VoiceCommand))
            {
                OnReply.Invoke("enable the microphone in settings to use voice");
                return null;
            }

            try
            {
                var context = new VoiceContext
                {
                    Transcript = transcript,
                    Measurements = _measurements?.Invoke(),
                    FrameImage = _frameProvider?.Invoke(),
                };
                if (_location != null)
                {
                    (double? lat, double? lng) = _location();
                    context.Latitude = lat;
                    context.Longitude = lng;
                }

                VoiceResponse response = await _agent.AskAsync(context, ct);
                if (response == null)
                {
                    OnError.Invoke("no response");
                    return null;
                }

                if (!string.IsNullOrWhiteSpace(response.Reply))
                    OnReply.Invoke(response.Reply);

                DispatchResult result = await _dispatcher.DispatchAsync(response.Action, _caps, ct);
                if (result.Feature != null && !result.Available)
                    OnActionBlocked.Invoke(result.Reason);

                return response;
            }
            catch (OperationCanceledException)
            {
                return null; // not an error
            }
            catch (Exception e)
            {
                OnError.Invoke("voice request failed");
                Debug.LogWarning($"[VoiceCommandController] turn failed: {e.Message}");
                return null;
            }
        }
    }
}
