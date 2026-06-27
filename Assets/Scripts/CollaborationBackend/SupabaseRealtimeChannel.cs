using System;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using SuperRealEstate.Collaboration;
using SuperRealEstate.Staging;

namespace SuperRealEstate.CollaborationBackend
{
    /// <summary>What a drained Realtime event represents.</summary>
    public enum RealtimeEventKind { PlacementUpserted, PlacementRemoved, Presence }

    /// <summary>One inbound Realtime event, dequeued and applied on the main thread.</summary>
    public readonly struct RealtimeEvent
    {
        public readonly RealtimeEventKind Kind;
        public readonly Placement Placement;     // PlacementUpserted
        public readonly string Id;               // PlacementRemoved (placement id)
        public readonly PresenceUpdate Presence; // Presence

        private RealtimeEvent(RealtimeEventKind kind, Placement placement, string id, PresenceUpdate presence)
        {
            Kind = kind; Placement = placement; Id = id; Presence = presence;
        }

        public static RealtimeEvent Upsert(Placement p) => new RealtimeEvent(RealtimeEventKind.PlacementUpserted, p, p?.Id, default);
        public static RealtimeEvent Remove(string id) => new RealtimeEvent(RealtimeEventKind.PlacementRemoved, null, id, default);
        public static RealtimeEvent Pose(PresenceUpdate p) => new RealtimeEvent(RealtimeEventKind.Presence, null, null, p);
    }

    /// <summary>
    /// Supabase **Realtime** transport (Phoenix WebSocket) for a shared session's
    /// layout: low-latency <c>postgres_changes</c> on <c>staging_placements</c>
    /// plus a <c>broadcast</c> channel for high-frequency **presence** (where each
    /// participant is looking) — the two things polling can't do well. This is the
    /// lower-latency upgrade to <see cref="SupabaseSharedSessionService"/>'s poll;
    /// run it alongside or instead of polling. Inbound frames are parsed (see
    /// <see cref="RealtimeFrameParser"/>, unit-tested) off the socket thread and
    /// queued; the owner drains <see cref="TryDequeue"/> on the main thread.
    ///
    /// Wire format targets Supabase Realtime (apikey in the URL, an
    /// <c>access_token</c> event so RLS applies to the change stream, a 25s
    /// heartbeat). The protocol details are best confirmed against live frames on
    /// device; the JSON parsing they feed is tested. Polling remains the proven
    /// default, so enabling this is additive and safe.
    /// </summary>
    public sealed class SupabaseRealtimeChannel : IDisposable
    {
        private readonly string _wsUrl;     // wss://<host>/realtime/v1/websocket?apikey=...&vsn=1.0.0
        private readonly string _anonKey;
        private string _accessToken;
        private string _topic;              // realtime:<layout>

        private ClientWebSocket _socket;
        private CancellationTokenSource _cts;
        private readonly SemaphoreSlim _sendLock = new SemaphoreSlim(1, 1);
        private readonly ConcurrentQueue<RealtimeEvent> _inbox = new ConcurrentQueue<RealtimeEvent>();
        private int _ref;

        public bool IsConnected => _socket != null && _socket.State == WebSocketState.Open;

        public SupabaseRealtimeChannel(string supabaseUrl, string anonKey, string accessToken = null)
        {
            if (string.IsNullOrEmpty(supabaseUrl)) throw new ArgumentNullException(nameof(supabaseUrl));
            string host = supabaseUrl.Replace("https://", "").Replace("http://", "").TrimEnd('/');
            _anonKey = anonKey;
            _accessToken = accessToken;
            _wsUrl = $"wss://{host}/realtime/v1/websocket?apikey={Uri.EscapeDataString(anonKey ?? string.Empty)}&vsn=1.0.0";
        }

        /// <summary>Set/refresh the user token used for RLS on the change stream.</summary>
        public void SetAccessToken(string token) => _accessToken = token;

        /// <summary>Drain one queued inbound event (call on the main thread). False when empty.</summary>
        public bool TryDequeue(out RealtimeEvent ev) => _inbox.TryDequeue(out ev);

        /// <summary>
        /// Connect and subscribe to a layout's placement changes + presence
        /// broadcast. Safe to call once per session; re-subscribing reconnects.
        /// </summary>
        public async Task ConnectAsync(string layoutId, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(layoutId)) throw new ArgumentNullException(nameof(layoutId));
            await CloseAsync();

            _topic = $"realtime:layout-{layoutId}";
            _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            _socket = new ClientWebSocket();
            await _socket.ConnectAsync(new Uri(_wsUrl), _cts.Token);

            await SendAsync(JoinFrame(layoutId), _cts.Token);
            if (!string.IsNullOrEmpty(_accessToken))
                await SendAsync(AccessTokenFrame(), _cts.Token);

            _ = Task.Run(() => ReceiveLoop(_cts.Token));
            _ = Task.Run(() => HeartbeatLoop(_cts.Token));
        }

        /// <summary>Broadcast this device's presence (head/pointer pose) to the others.</summary>
        public async Task SendPresenceAsync(PresenceUpdate presence, CancellationToken ct = default)
        {
            if (!IsConnected) return;
            await SendAsync(PresenceFrame(presence), ct);
        }

        public async Task CloseAsync()
        {
            try { _cts?.Cancel(); } catch { /* ignore */ }
            if (_socket != null)
            {
                try
                {
                    if (_socket.State == WebSocketState.Open)
                        await _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None);
                }
                catch { /* ignore */ }
                _socket.Dispose();
                _socket = null;
            }
            _cts?.Dispose();
            _cts = null;
        }

        public void Dispose() => _ = CloseAsync();

        // --- socket loops ---

        private async Task ReceiveLoop(CancellationToken ct)
        {
            var buffer = new byte[16 * 1024];
            var sb = new StringBuilder();
            try
            {
                while (!ct.IsCancellationRequested && _socket != null && _socket.State == WebSocketState.Open)
                {
                    WebSocketReceiveResult result = await _socket.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
                    if (result.MessageType == WebSocketMessageType.Close) break;

                    sb.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                    if (!result.EndOfMessage) continue;

                    string frame = sb.ToString();
                    sb.Clear();
                    Dispatch(frame);
                }
            }
            catch (OperationCanceledException) { /* closing */ }
            catch (Exception e)
            {
                Debug.LogWarning($"[SupabaseRealtimeChannel] receive loop ended: {e.Message}");
            }
        }

        private async Task HeartbeatLoop(CancellationToken ct)
        {
            try
            {
                while (!ct.IsCancellationRequested && IsConnected)
                {
                    await Task.Delay(TimeSpan.FromSeconds(25), ct);
                    await SendAsync(HeartbeatFrame(), ct);
                }
            }
            catch (OperationCanceledException) { /* closing */ }
            catch (Exception e)
            {
                Debug.LogWarning($"[SupabaseRealtimeChannel] heartbeat ended: {e.Message}");
            }
        }

        private void Dispatch(string frame)
        {
            string evt = RealtimeFrameParser.EventOf(frame);
            if (evt == "postgres_changes")
            {
                if (RealtimeFrameParser.TryParsePlacementChange(frame, out RealtimeChangeType change, out Placement p))
                {
                    if (change == RealtimeChangeType.Delete) _inbox.Enqueue(RealtimeEvent.Remove(p.Id));
                    else _inbox.Enqueue(RealtimeEvent.Upsert(p));
                }
            }
            else if (evt == "broadcast")
            {
                if (RealtimeFrameParser.TryParsePresence(frame, out PresenceUpdate presence))
                    _inbox.Enqueue(RealtimeEvent.Pose(presence));
            }
            // phx_reply / presence_state / system frames need no action here.
        }

        // --- frame builders (Phoenix message envelopes) ---

        private string JoinFrame(string layoutId)
        {
            string filter = Esc($"layout_id=eq.{layoutId}");
            return "{" +
                $"\"topic\":\"{Esc(_topic)}\",\"event\":\"phx_join\",\"payload\":{{" +
                "\"config\":{\"postgres_changes\":[{" +
                "\"event\":\"*\",\"schema\":\"public\",\"table\":\"staging_placements\"," +
                $"\"filter\":\"{filter}\"}}],\"broadcast\":{{\"self\":false}}}}}}," +
                $"\"ref\":\"{NextRef()}\"}}";
        }

        private string AccessTokenFrame()
            => "{" +
               $"\"topic\":\"{Esc(_topic)}\",\"event\":\"access_token\"," +
               $"\"payload\":{{\"access_token\":\"{Esc(_accessToken)}\"}},\"ref\":\"{NextRef()}\"}}";

        private string HeartbeatFrame()
            => $"{{\"topic\":\"phoenix\",\"event\":\"heartbeat\",\"payload\":{{}},\"ref\":\"{NextRef()}\"}}";

        private string PresenceFrame(PresenceUpdate p)
        {
            Vector3 hp = p.HeadPose.position;
            Quaternion hr = p.HeadPose.rotation;
            var inner = new StringBuilder();
            inner.Append("{")
                 .Append($"\"uid\":\"{Esc(p.UserId)}\",")
                 .Append($"\"px\":{F(hp.x)},\"py\":{F(hp.y)},\"pz\":{F(hp.z)},")
                 .Append($"\"qx\":{F(hr.x)},\"qy\":{F(hr.y)},\"qz\":{F(hr.z)},\"qw\":{F(hr.w)},")
                 .Append($"\"hasPointer\":{(p.HasPointer ? 1 : 0)},")
                 .Append($"\"rox\":{F(p.Pointer.origin.x)},\"roy\":{F(p.Pointer.origin.y)},\"roz\":{F(p.Pointer.origin.z)},")
                 .Append($"\"rdx\":{F(p.Pointer.direction.x)},\"rdy\":{F(p.Pointer.direction.y)},\"rdz\":{F(p.Pointer.direction.z)}")
                 .Append("}");

            return "{" +
                   $"\"topic\":\"{Esc(_topic)}\",\"event\":\"broadcast\",\"payload\":{{" +
                   $"\"type\":\"broadcast\",\"event\":\"presence\",\"payload\":{inner}}}," +
                   $"\"ref\":\"{NextRef()}\"}}";
        }

        private async Task SendAsync(string text, CancellationToken ct)
        {
            if (_socket == null || _socket.State != WebSocketState.Open) return;
            byte[] bytes = Encoding.UTF8.GetBytes(text);
            await _sendLock.WaitAsync(ct);
            try
            {
                await _socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, ct);
            }
            finally
            {
                _sendLock.Release();
            }
        }

        private int NextRef() => Interlocked.Increment(ref _ref);

        private static string F(float v) => v.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture);

        /// <summary>Escape a string for embedding in a JSON string literal.</summary>
        private static string Esc(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }
}
