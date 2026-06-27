using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using SuperRealEstate.Collaboration;
using SuperRealEstate.Staging;

namespace SuperRealEstate.CollaborationBackend
{
    /// <summary>
    /// Supabase-backed <see cref="ISharedSessionService"/> — the shared-session
    /// backbone that keeps the agent's Vision Pro, the buyer's Galaxy XR, and a
    /// spouse's phone looking at the SAME staged layout. Writes go over PostgREST
    /// (<c>{url}/rest/v1/...</c>) against the tables in migration 0002 (+ the
    /// host-managed invite/join flow in 0009); live sync is delivered by polling
    /// <see cref="PollOnceAsync"/> on an interval (drive it from a MonoBehaviour
    /// such as <c>SharedSessionSync</c>, which keeps UnityWebRequest on the main
    /// thread). Every participant converges within one poll interval.
    ///
    /// Why polling: it is correct on every platform and needs no extra package.
    /// Supabase **Realtime** (a Phoenix WebSocket carrying <c>postgres_changes</c>
    /// + broadcast) is the lower-latency upgrade and the right home for
    /// high-frequency **presence** (head/pointer pose) — see
    /// <see cref="PublishPresenceAsync"/>. That socket is the remaining seam; the
    /// REST CRUD here is real and RLS-correct today.
    ///
    /// Security: joining is via an invite **code** (host shares it), redeemed
    /// through the <c>join_session</c> SECURITY DEFINER function — NOT by raw
    /// session id (the open self-join hole closed in migration 0009). So
    /// <see cref="JoinSessionAsync"/>'s first argument is an invite code.
    ///
    /// Call the REST methods from the main thread (UnityWebRequest).
    /// </summary>
    public sealed class SupabaseSharedSessionService : ISharedSessionService
    {
        private readonly string _restUrl;
        private readonly string _anonKey;
        private string _accessToken;
        private string _userId; // resolved from the JWT 'sub' claim

        // Last-seen state, so PollOnceAsync can diff and raise only real changes.
        private readonly Dictionary<string, string> _placementVersions = new Dictionary<string, string>(); // id -> updated_at
        private readonly HashSet<string> _participantIds = new HashSet<string>();

        public SupabaseSharedSessionService(string supabaseUrl, string anonKey, string accessToken = null)
        {
            if (string.IsNullOrEmpty(supabaseUrl)) throw new ArgumentNullException(nameof(supabaseUrl));
            _restUrl = $"{supabaseUrl.TrimEnd('/')}/rest/v1";
            _anonKey = anonKey;
            SetAccessToken(accessToken);
        }

        public SharedSession Current { get; private set; }

        /// <summary>Set after sign-in so writes pass RLS; also resolves the user id.</summary>
        public void SetAccessToken(string token)
        {
            _accessToken = token;
            _userId = DecodeJwtSub(token);
        }

        // --- inbound events (raised on the main thread by PollOnceAsync) ---
        public event Action<SessionParticipant> ParticipantJoined;
        public event Action<string> ParticipantLeft;
        public event Action<Placement> PlacementChanged;
        public event Action<string> PlacementRemoved;
        public event Action<PresenceUpdate> PresenceUpdated;

        // --- session lifecycle ---

        public async Task<SharedSession> CreateSessionAsync(string propertyId, DeviceKind device, CancellationToken ct = default)
        {
            RequireUser();

            var insert = new SessionInsert
            {
                host_id = _userId,
                property_id = NullIfEmpty(propertyId),
                status = "active",
            };
            string body = OmitEmpty(JsonUtility.ToJson(insert), "property_id");
            string json = await Post("/sessions", body, returnRepresentation: true, ct);
            SessionRow row = FirstRow<SessionRowList, SessionRow>(json);
            if (row == null) throw new Exception("Create session: no row returned.");

            // Host adds self as a participant (participants_host_insert policy).
            await Post("/session_participants",
                JsonUtility.ToJson(new ParticipantInsert
                {
                    session_id = row.id, user_id = _userId,
                    role = "host", device_kind = DeviceText(device),
                }),
                returnRepresentation: false, ct);

            Current = ToSession(row);
            Current.Participants.Add(new SessionParticipant
            {
                UserId = _userId, Role = ParticipantRole.Host, Device = device,
            });
            ResetSyncState();
            _participantIds.Add(_userId); // don't report ourselves as a new join on first poll
            return Current;
        }

        /// <param name="inviteCode">The invite code the host shared (NOT a raw
        /// session id). Redeemed through <c>join_session</c>.</param>
        public async Task<SharedSession> JoinSessionAsync(
            string inviteCode, DeviceKind device, ParticipantRole role, CancellationToken ct = default)
        {
            RequireUser();
            if (string.IsNullOrWhiteSpace(inviteCode)) throw new ArgumentNullException(nameof(inviteCode));

            // Redeem the code → returns the session id (adds us as a participant).
            string rpcBody = JsonUtility.ToJson(new JoinArgs { p_code = inviteCode, p_device_kind = DeviceText(device) });
            string rpcResult = await Post("/rpc/join_session", rpcBody, returnRepresentation: false, ct);
            string sessionId = TrimJsonScalar(rpcResult);
            if (string.IsNullOrEmpty(sessionId))
                throw new Exception("Join failed: invalid or expired invite code.");

            // Load the session + roster now that we can read them.
            string sessJson = await Get($"/sessions?id=eq.{Esc(sessionId)}&select=id,host_id,property_id,layout_id,spatial_anchor_id,status", ct);
            SessionRow row = FirstRow<SessionRowList, SessionRow>(sessJson)
                             ?? throw new Exception("Join failed: session not readable.");

            Current = ToSession(row);
            ResetSyncState();
            await RefreshRosterAsync(raiseEvents: false, ct);
            return Current;
        }

        public async Task LeaveSessionAsync(CancellationToken ct = default)
        {
            if (Current == null) return;
            RequireUser();
            await Delete($"/session_participants?session_id=eq.{Esc(Current.Id)}&user_id=eq.{Esc(_userId)}", ct);
            Current = null;
            ResetSyncState();
        }

        // --- shared layout edits ---

        public async Task UpsertPlacementAsync(Placement placement, CancellationToken ct = default)
        {
            if (placement == null) throw new ArgumentNullException(nameof(placement));
            string layoutId = Current?.LayoutId;
            if (string.IsNullOrEmpty(layoutId))
                throw new InvalidOperationException("No active layout — set the session's LayoutId before placing furniture.");

            var dto = new PlacementWrite
            {
                layout_id = layoutId,
                furniture_asset_id = NullIfEmpty(placement.FurnitureAssetId),
                catalog_item_id = NullIfEmpty(placement.CatalogItemId),
                pos_x = placement.Position.x, pos_y = placement.Position.y, pos_z = placement.Position.z,
                rot_y_deg = placement.YawDegrees, scale = placement.Scale,
                anchor_id = NullIfEmpty(placement.AnchorId),
            };

            // The placement_single_source CHECK requires at most one source set;
            // OmitEmpty drops the empty one so the other is the sole source.
            string body = OmitEmpty(JsonUtility.ToJson(dto), "furniture_asset_id", "catalog_item_id", "anchor_id");
            if (!string.IsNullOrEmpty(placement.Id))
            {
                // Update existing row.
                string json = await Patch($"/staging_placements?id=eq.{Esc(placement.Id)}", body, ct);
                PlacementRow row = FirstRow<PlacementRowList, PlacementRow>(json);
                if (row != null) _placementVersions[row.id] = row.updated_at; // our own write — don't re-raise on poll
            }
            else
            {
                // Insert new row and capture the generated id.
                string json = await Post("/staging_placements", body, returnRepresentation: true, ct);
                PlacementRow row = FirstRow<PlacementRowList, PlacementRow>(json);
                if (row != null)
                {
                    placement.Id = row.id;
                    _placementVersions[row.id] = row.updated_at;
                }
            }
        }

        public async Task RemovePlacementAsync(string placementId, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(placementId)) return;
            await Delete($"/staging_placements?id=eq.{Esc(placementId)}", ct);
            _placementVersions.Remove(placementId);
        }

        /// <summary>
        /// Presence (head/pointer pose) updates many times per second — far too hot
        /// for REST/Postgres. This is intentionally a no-op over the REST path; it
        /// belongs on a Supabase Realtime **broadcast** channel (the remaining
        /// seam). Wire that and forward inbound updates to <see cref="PresenceUpdated"/>.
        /// </summary>
        public Task PublishPresenceAsync(PresenceUpdate presence, CancellationToken ct = default)
            => Task.CompletedTask;

        // --- live sync (poll) ---

        /// <summary>
        /// Fetch the roster + placements once and raise events for anything that
        /// changed since the last poll. Drive this on an interval from the main
        /// thread (UnityWebRequest). Safe to call when there is no active session.
        /// </summary>
        public async Task PollOnceAsync(CancellationToken ct = default)
        {
            if (Current == null) return;
            await RefreshRosterAsync(raiseEvents: true, ct);
            await RefreshPlacementsAsync(ct);
        }

        private async Task RefreshRosterAsync(bool raiseEvents, CancellationToken ct)
        {
            string json = await Get($"/session_participants?session_id=eq.{Esc(Current.Id)}&select=id,user_id,role,device_kind", ct);
            ParticipantRow[] rows = Rows<ParticipantRowList, ParticipantRow>(json);

            var seen = new HashSet<string>();
            var roster = new List<SessionParticipant>(rows.Length);
            foreach (ParticipantRow r in rows)
            {
                seen.Add(r.user_id);
                var p = new SessionParticipant
                {
                    UserId = r.user_id,
                    Role = ParseRole(r.role),
                    Device = ParseDevice(r.device_kind),
                    IsRemote = r.user_id != _userId,
                };
                roster.Add(p);

                if (raiseEvents && !_participantIds.Contains(r.user_id))
                    ParticipantJoined?.Invoke(p);
            }

            if (raiseEvents)
                foreach (string goneUserId in new List<string>(_participantIds))
                    if (!seen.Contains(goneUserId))
                        ParticipantLeft?.Invoke(goneUserId); // userId of the participant who left

            _participantIds.Clear();
            _participantIds.UnionWith(seen);
            Current.Participants = roster;
        }

        private async Task RefreshPlacementsAsync(CancellationToken ct)
        {
            string layoutId = Current?.LayoutId;
            if (string.IsNullOrEmpty(layoutId)) return;

            string json = await Get(
                $"/staging_placements?layout_id=eq.{Esc(layoutId)}&select=id,furniture_asset_id,catalog_item_id,pos_x,pos_y,pos_z,rot_y_deg,scale,anchor_id,updated_at", ct);
            PlacementRow[] rows = Rows<PlacementRowList, PlacementRow>(json);

            var seen = new HashSet<string>();
            foreach (PlacementRow r in rows)
            {
                seen.Add(r.id);
                bool isNewOrChanged =
                    !_placementVersions.TryGetValue(r.id, out string prev) || prev != r.updated_at;
                if (isNewOrChanged)
                {
                    _placementVersions[r.id] = r.updated_at;
                    PlacementChanged?.Invoke(ToPlacement(r));
                }
            }

            foreach (string goneId in new List<string>(_placementVersions.Keys))
                if (!seen.Contains(goneId))
                {
                    _placementVersions.Remove(goneId);
                    PlacementRemoved?.Invoke(goneId);
                }
        }

        // --- mapping ---

        private static SharedSession ToSession(SessionRow r) => new SharedSession
        {
            Id = r.id, HostId = r.host_id, PropertyId = r.property_id,
            LayoutId = r.layout_id, SpatialAnchorId = r.spatial_anchor_id,
        };

        private static Placement ToPlacement(PlacementRow r) => new Placement
        {
            Id = r.id,
            FurnitureAssetId = r.furniture_asset_id,
            CatalogItemId = r.catalog_item_id,
            Position = new Vector3(r.pos_x, r.pos_y, r.pos_z),
            YawDegrees = r.rot_y_deg,
            Scale = r.scale == 0f ? 1f : r.scale,
            AnchorId = r.anchor_id,
        };

        private static string DeviceText(DeviceKind d) => d switch
        {
            DeviceKind.VisionPro => "vision_pro",
            DeviceKind.AndroidXR => "android_xr",
            DeviceKind.IosPhone => "ios_phone",
            DeviceKind.AndroidPhone => "android_phone",
            DeviceKind.Tablet => "tablet",
            _ => "web",
        };

        private static DeviceKind ParseDevice(string s) => s switch
        {
            "vision_pro" => DeviceKind.VisionPro,
            "android_xr" => DeviceKind.AndroidXR,
            "ios_phone" => DeviceKind.IosPhone,
            "android_phone" => DeviceKind.AndroidPhone,
            "tablet" => DeviceKind.Tablet,
            _ => DeviceKind.Web,
        };

        private static ParticipantRole ParseRole(string s) => s switch
        {
            "host" => ParticipantRole.Host,
            "agent" => ParticipantRole.Agent,
            "guest" => ParticipantRole.Guest,
            _ => ParticipantRole.Buyer,
        };

        private void RequireUser()
        {
            if (string.IsNullOrEmpty(_userId))
                throw new InvalidOperationException("Not signed in — set a user access token before using sessions.");
        }

        private void ResetSyncState()
        {
            _placementVersions.Clear();
            _participantIds.Clear();
        }

        // --- HTTP helpers (UnityWebRequest, matching SupabaseBackendClient) ---

        private Task<string> Get(string pathAndQuery, CancellationToken ct)
            => Send("GET", pathAndQuery, null, false, ct);

        private Task<string> Post(string path, string body, bool returnRepresentation, CancellationToken ct)
            => Send("POST", path, body, returnRepresentation, ct);

        private Task<string> Patch(string pathAndQuery, string body, CancellationToken ct)
            => Send("PATCH", pathAndQuery, body, true, ct);

        private Task<string> Delete(string pathAndQuery, CancellationToken ct)
            => Send("DELETE", pathAndQuery, null, false, ct);

        private async Task<string> Send(string verb, string pathAndQuery, string body, bool returnRepresentation, CancellationToken ct)
        {
            using var www = new UnityWebRequest(_restUrl + pathAndQuery, verb)
            {
                downloadHandler = new DownloadHandlerBuffer(),
            };
            if (body != null)
            {
                www.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
                www.SetRequestHeader("Content-Type", "application/json");
            }
            www.SetRequestHeader("apikey", _anonKey);
            www.SetRequestHeader("Authorization", $"Bearer {(_accessToken ?? _anonKey)}");
            if (returnRepresentation) www.SetRequestHeader("Prefer", "return=representation");

            var tcs = new TaskCompletionSource<bool>();
            using (ct.Register(() => www.Abort()))
            {
                www.SendWebRequest().completed += _ => tcs.TrySetResult(true);
                await tcs.Task;
            }

            ct.ThrowIfCancellationRequested();
            if (www.result != UnityWebRequest.Result.Success)
                throw new Exception($"Supabase {verb} {pathAndQuery} failed: {www.responseCode} {www.error}");
            return www.downloadHandler.text;
        }

        private static string Esc(string s) => UnityWebRequest.EscapeURL(s ?? string.Empty);
        private static string NullIfEmpty(string s) => string.IsNullOrEmpty(s) ? null : s;

        /// <summary>
        /// Drop <c>"field":""</c> entries from flat JsonUtility output. JsonUtility
        /// writes null/empty strings as <c>""</c> (it can't omit), but PostgREST
        /// rejects <c>""</c> for nullable <c>uuid</c> columns — so optional uuid FKs
        /// must be omitted entirely to read as SQL NULL. Safe for the flat,
        /// quote-delimited JSON JsonUtility produces (no nested objects here).
        /// </summary>
        private static string OmitEmpty(string json, params string[] fields)
        {
            foreach (string f in fields)
            {
                json = json.Replace($"\"{f}\":\"\",", string.Empty); // field in the middle
                json = json.Replace($",\"{f}\":\"\"", string.Empty);  // field at the end
                json = json.Replace($"\"{f}\":\"\"", string.Empty);   // sole field
            }
            return json;
        }

        // JsonUtility can't parse a top-level array — wrap it as an object.
        private static string Wrap(string jsonArray)
            => "{\"items\":" + (string.IsNullOrEmpty(jsonArray) ? "[]" : jsonArray) + "}";

        private static TItem[] Rows<TList, TItem>(string json) where TList : class, IRowList<TItem>
        {
            var list = JsonUtility.FromJson<TList>(Wrap(json));
            return list?.Items ?? Array.Empty<TItem>();
        }

        private static TItem FirstRow<TList, TItem>(string json) where TList : class, IRowList<TItem>
        {
            TItem[] rows = Rows<TList, TItem>(json);
            return rows.Length > 0 ? rows[0] : default;
        }

        /// <summary>A PostgREST scalar RPC result like <c>"&lt;uuid&gt;"</c> → the bare value.</summary>
        private static string TrimJsonScalar(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            s = s.Trim();
            if (s.StartsWith("\"") && s.EndsWith("\"") && s.Length >= 2)
                s = s.Substring(1, s.Length - 2);
            return s;
        }

        /// <summary>Read the <c>sub</c> (user id) claim from a Supabase JWT, or null.</summary>
        private static string DecodeJwtSub(string jwt)
        {
            if (string.IsNullOrEmpty(jwt)) return null;
            try
            {
                string[] parts = jwt.Split('.');
                if (parts.Length < 2) return null;

                string payload = parts[1].Replace('-', '+').Replace('_', '/');
                switch (payload.Length % 4) { case 2: payload += "=="; break; case 3: payload += "="; break; }

                string jsonPayload = Encoding.UTF8.GetString(Convert.FromBase64String(payload));
                var claims = JsonUtility.FromJson<JwtClaims>(jsonPayload);
                return string.IsNullOrEmpty(claims?.sub) ? null : claims.sub;
            }
            catch
            {
                return null;
            }
        }

        // --- DTOs (snake_case to match PostgREST) ---

        private interface IRowList<T> { T[] Items { get; } }

        [Serializable] private sealed class JwtClaims { public string sub; }
        [Serializable] private sealed class JoinArgs { public string p_code; public string p_device_kind; }

        [Serializable] private sealed class SessionInsert { public string host_id, property_id, status; }
        [Serializable] private sealed class ParticipantInsert { public string session_id, user_id, role, device_kind; }
        [Serializable] private sealed class PlacementWrite {
            public string layout_id, furniture_asset_id, catalog_item_id, anchor_id;
            public float pos_x, pos_y, pos_z, rot_y_deg, scale; }

        [Serializable] private sealed class SessionRow { public string id, host_id, property_id, layout_id, spatial_anchor_id, status; }
        [Serializable] private sealed class SessionRowList : IRowList<SessionRow> { public SessionRow[] items; public SessionRow[] Items => items; }

        [Serializable] private sealed class ParticipantRow { public string id, user_id, role, device_kind; }
        [Serializable] private sealed class ParticipantRowList : IRowList<ParticipantRow> { public ParticipantRow[] items; public ParticipantRow[] Items => items; }

        [Serializable] private sealed class PlacementRow {
            public string id, furniture_asset_id, catalog_item_id, anchor_id, updated_at;
            public float pos_x, pos_y, pos_z, rot_y_deg, scale; }
        [Serializable] private sealed class PlacementRowList : IRowList<PlacementRow> { public PlacementRow[] items; public PlacementRow[] Items => items; }
    }
}
