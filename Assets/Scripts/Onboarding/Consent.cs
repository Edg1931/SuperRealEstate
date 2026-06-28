using System;
using System.Collections.Generic;
using System.Text;

namespace SuperRealEstate.Onboarding
{
    /// <summary>
    /// A kind of permission/consent the user grants. These are app-level consents
    /// (separate from, and in addition to, the OS permission prompt): the user
    /// agrees to what we'll do with the sensor before we ask the platform for it.
    /// Required for store review (camera-usage purpose, on-device data) and for
    /// fair, transparent capture.
    /// </summary>
    public enum ConsentType
    {
        Camera,      // passthrough/photo frames for plant + finish recognition
        SceneScan,   // depth / plane / mesh scanning of the space
        Location,    // GPS for parcel lines / comps
        Microphone,  // voice agent
        CloudSync,   // store rooms/projects/sessions in the backend
        Analytics    // anonymous usage analytics (opt-in)
    }

    /// <summary>
    /// The actions that need consent before they run — the gate keys. Mapped to
    /// the consents they require by <see cref="ConsentGate"/>.
    /// </summary>
    public enum CaptureAction
    {
        IdentifyPlant,
        RecognizeFinish,
        MeasureRoom,
        ScanRoom,
        VoiceCommand,
        ShowComps,
        SaveToCloud
    }

    /// <summary>
    /// Records which app-level consents the user has granted or denied. Pure +
    /// serializable (persist via PlayerPrefs in the AR layer). Unknown/unset
    /// consent reads as NOT granted — capture is denied until explicitly allowed.
    /// </summary>
    public sealed class ConsentLedger
    {
        private readonly Dictionary<ConsentType, bool> _state = new Dictionary<ConsentType, bool>();

        /// <summary>Grant a consent.</summary>
        public void Grant(ConsentType type) => _state[type] = true;

        /// <summary>Explicitly deny/revoke a consent.</summary>
        public void Deny(ConsentType type) => _state[type] = false;

        /// <summary>True only when the user has explicitly granted this consent.</summary>
        public bool IsGranted(ConsentType type) => _state.TryGetValue(type, out bool ok) && ok;

        /// <summary>True when the user has answered (grant or deny) this consent.</summary>
        public bool HasDecided(ConsentType type) => _state.ContainsKey(type);

        /// <summary>Every consent the user has granted.</summary>
        public IReadOnlyList<ConsentType> Granted()
        {
            var list = new List<ConsentType>();
            foreach (KeyValuePair<ConsentType, bool> kv in _state)
                if (kv.Value) list.Add(kv.Key);
            return list;
        }

        /// <summary>
        /// Compact, stable serialization (e.g. "Camera=1;SceneScan=0"). Order is by
        /// enum name so the output is deterministic for tests/storage.
        /// </summary>
        public string Serialize()
        {
            var keys = new List<ConsentType>(_state.Keys);
            keys.Sort((a, b) => string.CompareOrdinal(a.ToString(), b.ToString()));
            var sb = new StringBuilder();
            foreach (ConsentType k in keys)
            {
                if (sb.Length > 0) sb.Append(';');
                sb.Append(k).Append('=').Append(_state[k] ? '1' : '0');
            }
            return sb.ToString();
        }

        /// <summary>Parse <see cref="Serialize"/> output. Tolerant of junk/empty.</summary>
        public static ConsentLedger Deserialize(string s)
        {
            var ledger = new ConsentLedger();
            if (string.IsNullOrWhiteSpace(s)) return ledger;

            foreach (string pair in s.Split(';'))
            {
                int eq = pair.IndexOf('=');
                if (eq <= 0) continue;
                string name = pair.Substring(0, eq).Trim();
                string val = pair.Substring(eq + 1).Trim();
                if (Enum.TryParse(name, out ConsentType type))
                    ledger._state[type] = val == "1";
            }
            return ledger;
        }
    }

    /// <summary>
    /// Decides whether a <see cref="CaptureAction"/> is allowed given the current
    /// <see cref="ConsentLedger"/>. Pure policy: each action lists the consents it
    /// requires; the action runs only if ALL are granted. Used to gate capture so
    /// we never send a camera frame (or scan, or audio) without consent.
    /// </summary>
    public static class ConsentGate
    {
        /// <summary>The consents an action requires (all must be granted).</summary>
        public static IReadOnlyList<ConsentType> Required(CaptureAction action)
        {
            switch (action)
            {
                case CaptureAction.IdentifyPlant:
                case CaptureAction.RecognizeFinish:
                    return new[] { ConsentType.Camera };
                case CaptureAction.MeasureRoom:
                case CaptureAction.ScanRoom:
                    return new[] { ConsentType.SceneScan };
                case CaptureAction.VoiceCommand:
                    return new[] { ConsentType.Microphone };
                case CaptureAction.ShowComps:
                    return new[] { ConsentType.Location };
                case CaptureAction.SaveToCloud:
                    return new[] { ConsentType.CloudSync };
                default:
                    return Array.Empty<ConsentType>();
            }
        }

        /// <summary>True when every consent the action needs has been granted.</summary>
        public static bool IsAllowed(CaptureAction action, ConsentLedger ledger)
        {
            if (ledger == null) return false;
            foreach (ConsentType c in Required(action))
                if (!ledger.IsGranted(c)) return false;
            return true;
        }

        /// <summary>The first missing consent for an action, or null if all granted.</summary>
        public static ConsentType? FirstMissing(CaptureAction action, ConsentLedger ledger)
        {
            foreach (ConsentType c in Required(action))
                if (ledger == null || !ledger.IsGranted(c)) return c;
            return null;
        }
    }
}
