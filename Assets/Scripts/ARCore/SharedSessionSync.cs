using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;
using SuperRealEstate.Collaboration;
using SuperRealEstate.CollaborationBackend;
using SuperRealEstate.Staging;

namespace SuperRealEstate.ARCore
{
    /// <summary>
    /// Drives a <see cref="SupabaseSharedSessionService"/> on the main thread:
    /// polls for roster + placement changes on an interval (UnityWebRequest must
    /// run on the main thread, so this uses a coroutine rather than a background
    /// timer) and re-emits the service's events as <see cref="UnityEvent"/>s the
    /// HUD / staging renderer can subscribe to.
    ///
    /// Wire-up: <see cref="Configure"/> with a constructed service (from the
    /// Supabase config + signed-in token), then call <see cref="BeginSync"/> once
    /// a session is created/joined. The staging renderer should react to
    /// <see cref="OnPlacementChanged"/>/<see cref="OnPlacementRemoved"/> so the
    /// agent's edit appears on the buyer's headset within one interval.
    /// </summary>
    public sealed class SharedSessionSync : MonoBehaviour
    {
        [Tooltip("Seconds between sync polls. Lower = snappier, more requests.")]
        [SerializeField] private float pollIntervalSeconds = 1.5f;

        [Serializable] public sealed class ParticipantEvent : UnityEvent<SessionParticipant> { }
        [Serializable] public sealed class PlacementEvent : UnityEvent<Placement> { }
        [Serializable] public sealed class StringEvent : UnityEvent<string> { }

        public ParticipantEvent OnParticipantJoined = new ParticipantEvent();
        public StringEvent OnParticipantLeft = new StringEvent();
        public PlacementEvent OnPlacementChanged = new PlacementEvent();
        public StringEvent OnPlacementRemoved = new StringEvent();

        private SupabaseSharedSessionService _service;
        private Coroutine _loop;
        private CancellationTokenSource _cts;

        /// <summary>The session backbone; null until configured.</summary>
        public SupabaseSharedSessionService Service => _service;

        /// <summary>Inject the service and forward its events to the UnityEvents.</summary>
        public void Configure(SupabaseSharedSessionService service)
        {
            Unsubscribe();
            _service = service;
            if (_service == null) return;

            _service.ParticipantJoined += HandleParticipantJoined;
            _service.ParticipantLeft += HandleParticipantLeft;
            _service.PlacementChanged += HandlePlacementChanged;
            _service.PlacementRemoved += HandlePlacementRemoved;
        }

        /// <summary>Start polling for live updates (after a session is active).</summary>
        public void BeginSync()
        {
            if (_service == null || _loop != null) return;
            _cts = new CancellationTokenSource();
            _loop = StartCoroutine(PollLoop());
        }

        /// <summary>Stop polling.</summary>
        public void EndSync()
        {
            if (_loop != null) { StopCoroutine(_loop); _loop = null; }
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }

        private IEnumerator PollLoop()
        {
            var wait = new WaitForSeconds(Mathf.Max(0.25f, pollIntervalSeconds));
            while (true)
            {
                yield return wait;
                if (_service == null) continue;

                Task task = _service.PollOnceAsync(_cts.Token);
                while (!task.IsCompleted) yield return null;

                if (task.IsFaulted)
                    Debug.LogWarning($"[SharedSessionSync] poll failed: {task.Exception?.GetBaseException().Message}");
            }
        }

        private void HandleParticipantJoined(SessionParticipant p) => OnParticipantJoined.Invoke(p);
        private void HandleParticipantLeft(string id) => OnParticipantLeft.Invoke(id);
        private void HandlePlacementChanged(Placement p) => OnPlacementChanged.Invoke(p);
        private void HandlePlacementRemoved(string id) => OnPlacementRemoved.Invoke(id);

        private void Unsubscribe()
        {
            if (_service == null) return;
            _service.ParticipantJoined -= HandleParticipantJoined;
            _service.ParticipantLeft -= HandleParticipantLeft;
            _service.PlacementChanged -= HandlePlacementChanged;
            _service.PlacementRemoved -= HandlePlacementRemoved;
        }

        private void OnDestroy()
        {
            EndSync();
            Unsubscribe();
        }
    }
}
