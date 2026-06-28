using System.Collections.Generic;
using UnityEngine;
using SuperRealEstate.Collaboration;

namespace SuperRealEstate.ARCore
{
    /// <summary>
    /// Renders other participants in a shared session: a head proxy at each
    /// remote user's head pose plus a gaze/pointer ray showing where they're
    /// looking — the visual side of the presence stream
    /// (<see cref="SupabaseRealtimeChannel"/> → <c>SharedSessionSync.OnPresence</c>).
    /// So the agent can see exactly what the buyer is pointing at, and vice versa,
    /// across a Vision Pro, a Galaxy XR, and a phone.
    ///
    /// Wire-up: subscribe <c>SharedSessionSync.OnPresence</c> → <see cref="Apply"/>
    /// and <c>SharedSessionSync.OnParticipantLeft</c> → <see cref="Remove"/>. Set
    /// <see cref="LocalUserId"/> so your own presence isn't drawn. Visuals are
    /// deliberate placeholders (a tinted sphere + a line); swap in real avatar
    /// prefabs via <see cref="HeadPrefab"/> when art lands — call sites don't change.
    /// </summary>
    public sealed class PresenceAvatars : MonoBehaviour
    {
        [Tooltip("This device's user id — its presence is not drawn. Set after sign-in.")]
        [SerializeField] private string localUserId;

        [Tooltip("Optional real avatar prefab; a placeholder sphere is used when empty.")]
        [SerializeField] private GameObject headPrefab;

        [Tooltip("Length (m) of the drawn gaze/pointer ray.")]
        [SerializeField] private float rayLength = 3f;

        [Tooltip("Diameter (m) of the placeholder head proxy.")]
        [SerializeField] private float headSizeM = 0.22f;

        /// <summary>This device's user id; its presence is skipped.</summary>
        public string LocalUserId { get => localUserId; set => localUserId = value; }

        /// <summary>Optional avatar prefab (placeholder sphere when null).</summary>
        public GameObject HeadPrefab { get => headPrefab; set => headPrefab = value; }

        private readonly Dictionary<string, Avatar> _avatars = new Dictionary<string, Avatar>();
        private readonly Dictionary<string, List<Material>> _avatarMaterials = new Dictionary<string, List<Material>>();
        private Transform _root;
        private Material _baseMaterial;

        /// <summary>Update (or create) the avatar for a participant from a presence update.</summary>
        public void Apply(PresenceUpdate presence)
        {
            string id = presence.UserId;
            if (string.IsNullOrEmpty(id) || id == localUserId) return;

            EnsureRoot();
            Avatar avatar = GetOrCreate(id);

            avatar.Head.SetPositionAndRotation(presence.HeadPose.position, presence.HeadPose.rotation);

            if (presence.HasPointer)
            {
                Vector3 origin = presence.Pointer.origin;
                Vector3 dir = presence.Pointer.direction.sqrMagnitude > 1e-6f
                    ? presence.Pointer.direction.normalized
                    : presence.HeadPose.forward;
                avatar.Ray.enabled = true;
                avatar.Ray.SetPosition(0, origin);
                avatar.Ray.SetPosition(1, origin + dir * rayLength);
            }
            else
            {
                avatar.Ray.enabled = false;
            }
        }

        /// <summary>Remove a participant's avatar (e.g. when they leave).</summary>
        public void Remove(string userId)
        {
            if (string.IsNullOrEmpty(userId)) return;
            if (_avatars.TryGetValue(userId, out Avatar avatar))
            {
                if (avatar.Root != null) DestroySafe(avatar.Root.gameObject);
                _avatars.Remove(userId);
            }
            DestroyMaterials(userId);
        }

        /// <summary>Remove every avatar.</summary>
        public void Clear()
        {
            foreach (Avatar a in _avatars.Values)
                if (a.Root != null) DestroySafe(a.Root.gameObject);
            _avatars.Clear();

            foreach (List<Material> mats in _avatarMaterials.Values)
                foreach (Material m in mats)
                    if (m != null) DestroySafe(m);
            _avatarMaterials.Clear();
        }

        private void DestroyMaterials(string userId)
        {
            if (!_avatarMaterials.TryGetValue(userId, out List<Material> mats)) return;
            foreach (Material m in mats)
                if (m != null) DestroySafe(m);
            _avatarMaterials.Remove(userId);
        }

        // --- build ---

        private Avatar GetOrCreate(string userId)
        {
            if (_avatars.TryGetValue(userId, out Avatar existing) && existing.Root != null)
                return existing;

            var go = new GameObject($"Avatar_{userId}");
            go.transform.SetParent(_root, worldPositionStays: false);
            Color color = ColorFor(userId);
            var mats = new List<Material>(2);

            // Head proxy.
            Transform head;
            if (headPrefab != null)
            {
                head = Instantiate(headPrefab, go.transform).transform;
            }
            else
            {
                GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sphere.transform.SetParent(go.transform, worldPositionStays: false);
                sphere.transform.localScale = Vector3.one * headSizeM;
                var col = sphere.GetComponent<Collider>();
                if (col != null) DestroySafe(col); // avatars shouldn't block raycasts
                var mr = sphere.GetComponent<MeshRenderer>();
                if (mr != null) mr.sharedMaterial = TintedMaterial(color, mats);
                head = sphere.transform;
            }

            // Gaze/pointer ray.
            var rayGo = new GameObject("Ray");
            rayGo.transform.SetParent(go.transform, worldPositionStays: false);
            var line = rayGo.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.widthMultiplier = 0.01f;
            line.material = TintedMaterial(color, mats);
            line.startColor = line.endColor = color;
            line.enabled = false;

            var avatar = new Avatar(go.transform, head, line);
            _avatars[userId] = avatar;
            _avatarMaterials[userId] = mats;
            return avatar;
        }

        private Material TintedMaterial(Color color, List<Material> track)
        {
            if (_baseMaterial == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit")
                                ?? Shader.Find("Standard")
                                ?? Shader.Find("Sprites/Default");
                _baseMaterial = new Material(shader) { name = "PresenceAvatarBase" };
            }
            // One material instance per renderer so colors differ; tracked so it's
            // destroyed with the avatar (no leak).
            var mat = new Material(_baseMaterial) { color = color };
            track.Add(mat);
            return mat;
        }

        /// <summary>Stable per-user color so the same person keeps the same hue.</summary>
        private static Color ColorFor(string userId)
        {
            int hash = 17;
            foreach (char c in userId) hash = hash * 31 + c;
            float hue = (hash & 0x7fffffff) % 360 / 360f;
            return Color.HSVToRGB(hue, 0.6f, 0.95f);
        }

        private void EnsureRoot()
        {
            if (_root != null) return;
            _root = new GameObject("PresenceAvatars").transform;
            _root.SetParent(transform, worldPositionStays: false);
        }

        private static void DestroySafe(UnityEngine.Object o)
        {
            if (Application.isPlaying) Destroy(o);
            else DestroyImmediate(o);
        }

        private void OnDestroy()
        {
            Clear();
            if (_baseMaterial != null) DestroySafe(_baseMaterial);
        }

        private readonly struct Avatar
        {
            public readonly Transform Root;
            public readonly Transform Head;
            public readonly LineRenderer Ray;
            public Avatar(Transform root, Transform head, LineRenderer ray) { Root = root; Head = head; Ray = ray; }
        }
    }
}
