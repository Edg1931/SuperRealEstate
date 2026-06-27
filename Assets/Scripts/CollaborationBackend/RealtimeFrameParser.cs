using UnityEngine;
using SuperRealEstate.Collaboration;
using SuperRealEstate.Staging;

namespace SuperRealEstate.CollaborationBackend
{
    /// <summary>The kind of database change a Realtime frame carries.</summary>
    public enum RealtimeChangeType { Unknown, Insert, Update, Delete }

    /// <summary>
    /// Pure, dependency-light parsing for Supabase Realtime (Phoenix) frames —
    /// the part of the WebSocket transport that is easy to get subtly wrong, so
    /// it's isolated here and unit-tested against sample frames (no socket, no
    /// Unity runtime needed). The socket plumbing lives in
    /// <see cref="SupabaseRealtimeChannel"/>.
    ///
    /// A <c>postgres_changes</c> message looks like:
    /// <code>
    /// {"topic":"realtime:...","event":"postgres_changes",
    ///  "payload":{"data":{"type":"INSERT","table":"staging_placements",
    ///                      "record":{...row...},"old_record":{...}}},"ref":null}
    /// </code>
    /// We pull the top-level <c>event</c>, then for changes the <c>data.type</c>
    /// and the <c>record</c>/<c>old_record</c> object, and map the flat row to a
    /// <see cref="Placement"/>. Tolerant of extra/reordered fields.
    /// </summary>
    public static class RealtimeFrameParser
    {
        /// <summary>The top-level Phoenix <c>event</c> of a frame (e.g. "postgres_changes", "phx_reply").</summary>
        public static string EventOf(string frameJson) => ExtractStringValue(frameJson, "event");

        /// <summary>The top-level Phoenix <c>topic</c> of a frame.</summary>
        public static string TopicOf(string frameJson) => ExtractStringValue(frameJson, "topic");

        /// <summary>
        /// Parse a <c>postgres_changes</c> frame into a change type + the affected
        /// placement. For deletes the placement carries (at least) the id from
        /// <c>old_record</c>. Returns false when the frame isn't a parseable change.
        /// </summary>
        public static bool TryParsePlacementChange(string frameJson, out RealtimeChangeType change, out Placement placement)
        {
            change = RealtimeChangeType.Unknown;
            placement = null;

            string data = ExtractJsonObject(frameJson, "data");
            if (data == null) return false;

            change = ParseChangeType(ExtractStringValue(data, "type"));

            // Inserts/updates carry the new row in "record"; deletes carry the
            // prior row (with its id) in "old_record".
            string record = ExtractJsonObject(data, "record");
            if (string.IsNullOrEmpty(record) || record == "{}")
                record = ExtractJsonObject(data, "old_record");
            if (string.IsNullOrEmpty(record)) return false;

            placement = ToPlacement(record);
            return placement != null && !string.IsNullOrEmpty(placement.Id);
        }

        /// <summary>Map a flat <c>staging_placements</c> row JSON to a <see cref="Placement"/>.</summary>
        public static Placement ToPlacement(string recordJson)
        {
            if (string.IsNullOrEmpty(recordJson)) return null;
            var row = JsonUtility.FromJson<PlacementRecord>(recordJson);
            if (row == null || string.IsNullOrEmpty(row.id)) return null;
            return new Placement
            {
                Id = row.id,
                FurnitureAssetId = NullIfEmpty(row.furniture_asset_id),
                CatalogItemId = NullIfEmpty(row.catalog_item_id),
                Position = new Vector3(row.pos_x, row.pos_y, row.pos_z),
                YawDegrees = row.rot_y_deg,
                Scale = row.scale == 0f ? 1f : row.scale,
                AnchorId = NullIfEmpty(row.anchor_id),
            };
        }

        /// <summary>
        /// Parse a presence <c>broadcast</c> frame (our own schema, nested at
        /// <c>payload.payload</c>) into a <see cref="PresenceUpdate"/>.
        /// </summary>
        public static bool TryParsePresence(string frameJson, out PresenceUpdate presence)
        {
            presence = default;
            string outer = ExtractJsonObject(frameJson, "payload");
            string inner = outer != null ? ExtractJsonObject(outer, "payload") : null;
            if (string.IsNullOrEmpty(inner)) return false;

            var dto = JsonUtility.FromJson<PresenceDto>(inner);
            if (dto == null || string.IsNullOrEmpty(dto.uid)) return false;

            presence = new PresenceUpdate
            {
                UserId = dto.uid,
                HeadPose = new Pose(new Vector3(dto.px, dto.py, dto.pz), new Quaternion(dto.qx, dto.qy, dto.qz, dto.qw)),
                HasPointer = dto.hasPointer != 0,
                Pointer = new Ray(new Vector3(dto.rox, dto.roy, dto.roz), new Vector3(dto.rdx, dto.rdy, dto.rdz)),
            };
            return true;
        }

        public static RealtimeChangeType ParseChangeType(string type)
        {
            if (string.IsNullOrEmpty(type)) return RealtimeChangeType.Unknown;
            switch (type.ToUpperInvariant())
            {
                case "INSERT": return RealtimeChangeType.Insert;
                case "UPDATE": return RealtimeChangeType.Update;
                case "DELETE": return RealtimeChangeType.Delete;
                default: return RealtimeChangeType.Unknown;
            }
        }

        // --- pure JSON helpers (string-aware; tolerant of nesting) ---

        /// <summary>Value of a string field <c>"key":"value"</c>, or null. Honors escapes.</summary>
        public static string ExtractStringValue(string json, string key)
        {
            if (string.IsNullOrEmpty(json)) return null;
            int colon = FindKey(json, key);
            if (colon < 0) return null;

            int i = SkipWhitespace(json, colon + 1);
            if (i >= json.Length || json[i] != '"') return null; // not a string value
            i++;

            var sb = new System.Text.StringBuilder();
            while (i < json.Length)
            {
                char c = json[i];
                if (c == '\\' && i + 1 < json.Length)
                {
                    char n = json[i + 1];
                    sb.Append(n switch { 'n' => '\n', 't' => '\t', 'r' => '\r', _ => n });
                    i += 2;
                    continue;
                }
                if (c == '"') return sb.ToString();
                sb.Append(c);
                i++;
            }
            return null;
        }

        /// <summary>
        /// The <c>{...}</c> object value of <paramref name="key"/> (brace-matched,
        /// string-aware), or null. Returns the object including its outer braces.
        /// </summary>
        public static string ExtractJsonObject(string json, string key)
        {
            if (string.IsNullOrEmpty(json)) return null;
            int colon = FindKey(json, key);
            if (colon < 0) return null;

            int i = SkipWhitespace(json, colon + 1);
            if (i >= json.Length || json[i] != '{') return null;

            int start = i;
            int depth = 0;
            bool inString = false;
            for (; i < json.Length; i++)
            {
                char c = json[i];
                if (inString)
                {
                    if (c == '\\') { i++; continue; }
                    if (c == '"') inString = false;
                    continue;
                }
                if (c == '"') { inString = true; continue; }
                if (c == '{') depth++;
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0) return json.Substring(start, i - start + 1);
                }
            }
            return null;
        }

        /// <summary>Index just after the <c>:</c> following <c>"key"</c> at any depth, or -1.</summary>
        private static int FindKey(string json, string key)
        {
            string token = "\"" + key + "\"";
            int from = 0;
            while (true)
            {
                int idx = json.IndexOf(token, from, System.StringComparison.Ordinal);
                if (idx < 0) return -1;
                int after = SkipWhitespace(json, idx + token.Length);
                if (after < json.Length && json[after] == ':')
                    return after; // points at ':'
                from = idx + token.Length;
            }
        }

        private static int SkipWhitespace(string s, int i)
        {
            while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
            return i;
        }

        private static string NullIfEmpty(string s) => string.IsNullOrEmpty(s) ? null : s;

        [System.Serializable]
        private sealed class PlacementRecord
        {
            public string id, furniture_asset_id, catalog_item_id, anchor_id, updated_at;
            public float pos_x, pos_y, pos_z, rot_y_deg, scale;
        }

        [System.Serializable]
        private sealed class PresenceDto
        {
            public string uid;
            public float px, py, pz, qx, qy, qz, qw;
            public int hasPointer;
            public float rox, roy, roz, rdx, rdy, rdz;
        }
    }
}
