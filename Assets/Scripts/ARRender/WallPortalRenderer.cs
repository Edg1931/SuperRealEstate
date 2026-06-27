using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using SuperRealEstate.Renovation;

namespace SuperRealEstate.ARRender
{
    /// <summary>
    /// In-scene implementation of <see cref="IPortalRenderer"/>: shows a "removed"
    /// wall as a portal into a pre-scanned adjacent space (see
    /// docs/Diminished-Reality.md). It builds a quad over the wall aperture and
    /// surfaces the revealed capture on it — this is portal *compositing* (we know
    /// what's behind from the scan), not live inpainting, which is why it's
    /// feasible on today's passthrough hardware.
    ///
    /// Rendering is deliberately material-driven so the visual can mature without
    /// touching call sites:
    ///  • <see cref="PortalRenderMode.OpaqueOverlay"/> uses <see cref="opaqueMaterial"/>
    ///    (or a generated placeholder) — the revealed room as a flat surface over
    ///    the real wall.
    ///  • <see cref="PortalRenderMode.StencilCutout"/> uses <see cref="stencilMaterial"/>
    ///    — a stencil/depth-composited cutout for cleaner edges where a stencil
    ///    shader is supplied.
    /// Provide the revealed capture's material via <see cref="RevealedCaptureMaterials"/>
    /// (keyed by <c>RevealedCaptureId</c>); without a match a neutral placeholder
    /// is used, consistent with <see cref="StagedSceneRenderer"/>.
    ///
    /// All quads live under a single "PortalRoot" child so everything can be torn
    /// down at once. The aperture corners are already world-space, so the root is
    /// kept at identity and mesh vertices are authored in world coordinates.
    /// </summary>
    public sealed class WallPortalRenderer : MonoBehaviour, IPortalRenderer
    {
        private const string RootName = "PortalRoot";

        [Tooltip("Material for OpaqueOverlay portals. Falls back to a generated placeholder.")]
        [SerializeField] private Material opaqueMaterial;

        [Tooltip("Material for StencilCutout portals (supply a stencil shader). " +
                 "Falls back to the opaque material / placeholder.")]
        [SerializeField] private Material stencilMaterial;

        /// <summary>
        /// Optional per-capture materials keyed by <c>RemovedWallPortal.RevealedCaptureId</c>.
        /// When a portal's capture id has a match here, that material textures the
        /// quad (e.g. an equirectangular/baked view of the scanned adjacent room);
        /// otherwise the mode's default material is used.
        /// </summary>
        public Dictionary<string, Material> RevealedCaptureMaterials { get; } = new Dictionary<string, Material>();

        private readonly Dictionary<string, GameObject> _portals = new Dictionary<string, GameObject>();
        private Transform _root;
        private Material _placeholder;

        /// <inheritdoc />
        public Task ShowPortalAsync(RemovedWallPortal portal, WallAperture aperture, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            if (portal == null) return Task.CompletedTask;

            EnsureRoot();

            string id = string.IsNullOrEmpty(portal.Id) ? portal.WallId : portal.Id;
            if (string.IsNullOrEmpty(id)) return Task.CompletedTask;

            // Rebuild if this portal is already showing (mode/aperture may change).
            if (_portals.TryGetValue(id, out GameObject existing) && existing != null)
                DestroySafe(existing);

            GameObject go = BuildPortalQuad(id, portal, aperture);
            _portals[id] = go;
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task HidePortalAsync(string portalId, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            if (!string.IsNullOrEmpty(portalId) && _portals.TryGetValue(portalId, out GameObject go))
            {
                if (go != null) DestroySafe(go);
                _portals.Remove(portalId);
            }
            return Task.CompletedTask;
        }

        /// <summary>Hide every portal this renderer is showing.</summary>
        public void HideAll()
        {
            foreach (GameObject go in _portals.Values)
                if (go != null) DestroySafe(go);
            _portals.Clear();
        }

        // --- build ---------------------------------------------------------

        private GameObject BuildPortalQuad(string id, RemovedWallPortal portal, WallAperture aperture)
        {
            var go = new GameObject($"Portal_{id}");
            go.transform.SetParent(_root, worldPositionStays: false);
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            var filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = BuildApertureMesh(aperture);

            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = ResolveMaterial(portal);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            return go;
        }

        /// <summary>
        /// Build a double-sided quad from the aperture's four world-space corners
        /// (bottom-start, bottom-end, top-end, top-start). Double-sided so the
        /// portal reads correctly whether the user is in front of or behind the
        /// removed wall.
        /// </summary>
        private static Mesh BuildApertureMesh(WallAperture aperture)
        {
            Vector3[] c = aperture.Corners;
            var mesh = new Mesh { name = "PortalAperture" };

            if (c == null || c.Length < 4)
                return mesh;

            mesh.vertices = new[] { c[0], c[1], c[2], c[3] };
            mesh.uv = new[]
            {
                new Vector2(0f, 0f), // bottom-start
                new Vector2(1f, 0f), // bottom-end
                new Vector2(1f, 1f), // top-end
                new Vector2(0f, 1f), // top-start
            };
            // Front faces (0,2,1)/(0,3,2) and the reverse winding for the back.
            mesh.triangles = new[]
            {
                0, 2, 1, 0, 3, 2, // front
                0, 1, 2, 0, 2, 3, // back
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private Material ResolveMaterial(RemovedWallPortal portal)
        {
            if (!string.IsNullOrEmpty(portal.RevealedCaptureId)
                && RevealedCaptureMaterials.TryGetValue(portal.RevealedCaptureId, out Material captureMat)
                && captureMat != null)
                return captureMat;

            if (portal.Mode == PortalRenderMode.StencilCutout && stencilMaterial != null)
                return stencilMaterial;

            if (opaqueMaterial != null)
                return opaqueMaterial;

            return Placeholder();
        }

        private Material Placeholder()
        {
            if (_placeholder != null) return _placeholder;

            // Same defensive shader-fallback pattern the staged renderer uses, so
            // the portal is visible even before art/material assets land.
            Shader shader = Shader.Find("Universal Render Pipeline/Lit")
                            ?? Shader.Find("Standard")
                            ?? Shader.Find("Sprites/Default");
            _placeholder = new Material(shader) { name = "PortalPlaceholder" };
            _placeholder.color = new Color(0.55f, 0.6f, 0.7f, 1f); // muted "revealed space"
            return _placeholder;
        }

        // --- lifecycle -----------------------------------------------------

        private void EnsureRoot()
        {
            if (_root != null) return;
            Transform existing = transform.Find(RootName);
            _root = existing != null ? existing : new GameObject(RootName).transform;
            if (existing == null) _root.SetParent(transform, worldPositionStays: false);
        }

        private static void DestroySafe(UnityEngine.Object o)
        {
            if (Application.isPlaying) Destroy(o);
            else DestroyImmediate(o);
        }

        private void OnDestroy()
        {
            if (_placeholder != null) DestroySafe(_placeholder);
        }
    }
}
