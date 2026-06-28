using System.Collections.Generic;
using UnityEngine;
using SuperRealEstate.Projects;
using SuperRealEstate.Renovation;
using SuperRealEstate.Staging;

namespace SuperRealEstate.ARRender
{
    /// <summary>
    /// In-scene renderer layer: the concrete, visual implementation of
    /// <see cref="IStagedSceneRenderer"/>. Given a world-ready
    /// <see cref="StagedScene"/> (the renovated structure plus furniture
    /// placements), it builds throwaway Unity GameObjects under a single
    /// "StagedRoot" child so the whole scene can be cleared in one shot.
    ///
    /// This is deliberately a placeholder visual: walls are drawn as thin
    /// vertical boxes and furniture as scaled cubes. Swap in real prefabs via
    /// <see cref="PrefabMap"/> (keyed by FurnitureAssetId or CatalogItemId)
    /// when art assets land — the contract and call sites stay the same.
    /// </summary>
    public sealed class StagedSceneRenderer : MonoBehaviour, IStagedSceneRenderer
    {
        private const string RootName = "StagedRoot";

        /// <summary>Default footprint (m) of a furniture placeholder before scale.</summary>
        [SerializeField] private float placeholderSizeM = 0.6f;

        /// <summary>Thickness (m) used to give each wall quad some depth.</summary>
        [SerializeField] private float wallThicknessM = 0.1f;

        /// <summary>
        /// Optional real prefabs keyed by <c>FurnitureAssetId</c> /
        /// <c>CatalogItemId</c>. When a placement's id has a match here, the
        /// prefab is instantiated instead of the placeholder box. Populate from
        /// the asset/catalog layer; leave empty to render placeholders only.
        /// </summary>
        public Dictionary<string, GameObject> PrefabMap { get; } = new Dictionary<string, GameObject>();

        /// <summary>
        /// Optional asset library of real furniture prefabs; its entries are
        /// loaded into <see cref="PrefabMap"/> on Awake so assigning art is a
        /// no-code drop-in. Additional prefabs can still be added to PrefabMap at
        /// runtime (e.g. models fetched by url).
        /// </summary>
        [SerializeField] private FurniturePrefabLibrary furnitureLibrary;

        private Transform _root;

        private void Awake() => LoadLibrary(furnitureLibrary);

        /// <summary>Merge a furniture prefab library's entries into <see cref="PrefabMap"/>.</summary>
        public void LoadLibrary(FurniturePrefabLibrary library) => library?.PopulateInto(PrefabMap);

        /// <summary>Builds (or rebuilds) the in-scene visuals for <paramref name="scene"/>.</summary>
        public void Render(StagedScene scene)
        {
            EnsureRoot();
            ClearChildren(_root);
            if (scene == null) return;

            DrawWalls(scene.Model);
            DrawPlacements(scene.Placements);
        }

        /// <summary>Removes everything this renderer has drawn.</summary>
        public void Clear()
        {
            if (_root == null) return;
            ClearChildren(_root);
        }

        // --- Structure ----------------------------------------------------

        private void DrawWalls(BuildingModel model)
        {
            if (model == null || model.Walls == null) return; // empty-staging: no structure

            foreach (var wall in model.Walls)
            {
                if (wall == null) continue;
                DrawWall(wall);
            }
        }

        // Each wall is a thin vertical box spanning Start->End at HeightM, centred
        // on the floor segment and raised so its base sits on the ground plane.
        private void DrawWall(Wall wall)
        {
            // Plan coordinates (Vector2, meters on the floor) map to the X/Z plane.
            var start = new Vector3(wall.Start.x, 0f, wall.Start.y);
            var end = new Vector3(wall.End.x, 0f, wall.End.y);
            var delta = end - start;
            float length = delta.magnitude;
            if (length <= Mathf.Epsilon) return; // degenerate wall

            float height = Mathf.Max(0.01f, wall.HeightM);
            float thickness = wall.ThicknessM > 0f ? wall.ThicknessM : wallThicknessM;

            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = string.IsNullOrEmpty(wall.Id) ? "Wall" : "Wall_" + wall.Id;
            go.transform.SetParent(_root, false);

            // Centre of the segment, lifted by half the wall height.
            go.transform.localPosition = new Vector3(
                (start.x + end.x) * 0.5f,
                height * 0.5f,
                (start.z + end.z) * 0.5f);

            // Orient the cube's local X along the wall direction.
            go.transform.localRotation = Quaternion.LookRotation(delta.normalized, Vector3.up)
                                         * Quaternion.Euler(0f, 90f, 0f);
            go.transform.localScale = new Vector3(length, height, thickness);

            ApplyColor(go, new Color(0.62f, 0.64f, 0.67f, 1f)); // neutral structural gray
        }

        // --- Furniture ----------------------------------------------------

        private void DrawPlacements(List<Placement> placements)
        {
            if (placements == null) return;

            foreach (var placement in placements)
            {
                if (placement == null) continue;
                DrawPlacement(placement);
            }
        }

        private void DrawPlacement(Placement placement)
        {
            float scale = placement.Scale > 0f ? placement.Scale : 1f;
            var go = ResolvePrefab(placement);

            if (go == null)
            {
                // Placeholder: a scaled cube sitting on the floor (size 0.6^3 * Scale).
                go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                float size = placeholderSizeM * scale;
                go.transform.localScale = new Vector3(size, size, size);
                ApplyColor(go, new Color(0.31f, 0.66f, 0.88f, 1f)); // accent
            }
            else
            {
                go.transform.localScale = go.transform.localScale * scale;
            }

            go.name = PlacementName(placement);
            go.transform.SetParent(_root, false);
            go.transform.localPosition = placement.Position;
            go.transform.localRotation = Quaternion.Euler(0f, placement.YawDegrees, 0f);
        }

        // Instantiates a mapped prefab if the placement's id is registered.
        private GameObject ResolvePrefab(Placement placement)
        {
            if (PrefabMap == null || PrefabMap.Count == 0) return null;

            GameObject prefab = null;
            if (!string.IsNullOrEmpty(placement.CatalogItemId))
                PrefabMap.TryGetValue(placement.CatalogItemId, out prefab);
            if (prefab == null && !string.IsNullOrEmpty(placement.FurnitureAssetId))
                PrefabMap.TryGetValue(placement.FurnitureAssetId, out prefab);

            return prefab != null ? Instantiate(prefab) : null;
        }

        private static string PlacementName(Placement placement)
        {
            string id = !string.IsNullOrEmpty(placement.CatalogItemId)
                ? placement.CatalogItemId
                : placement.FurnitureAssetId;
            return string.IsNullOrEmpty(id) ? "Placement" : "Placement_" + id;
        }

        // --- Helpers ------------------------------------------------------

        private void EnsureRoot()
        {
            if (_root != null) return;

            var existing = transform.Find(RootName);
            if (existing != null)
            {
                _root = existing;
                return;
            }

            var rootGo = new GameObject(RootName);
            rootGo.transform.SetParent(transform, false);
            _root = rootGo.transform;
        }

        private static void ClearChildren(Transform parent)
        {
            if (parent == null) return;
            for (int i = parent.childCount - 1; i >= 0; i--)
                DestroyImmediate(parent.GetChild(i).gameObject);
        }

        // Sets a primitive's color on an instance material so it is independent
        // of the shared shared-material asset.
        private static void ApplyColor(GameObject go, Color color)
        {
            var mr = go != null ? go.GetComponent<MeshRenderer>() : null;
            if (mr == null) return;
            mr.material.color = color;
        }
    }
}
