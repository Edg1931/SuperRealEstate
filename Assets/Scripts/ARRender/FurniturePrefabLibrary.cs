using System.Collections.Generic;
using UnityEngine;

namespace SuperRealEstate.ARRender
{
    /// <summary>
    /// The drop-in point for real furniture art: a project asset mapping catalog
    /// item ids / furniture-asset ids → prefabs. Create one via
    /// <b>Assets → Create → SuperRealEstate → Furniture Prefab Library</b>, add
    /// entries (the id must match the vendor <c>vendor_catalog_items.id</c> or a
    /// user's <c>furniture_assets.id</c>), and assign it on
    /// <see cref="StagedSceneRenderer"/>. Placements then render the real model
    /// instead of the placeholder box — no code change. Until art lands, leave it
    /// empty and the renderer falls back to placeholders.
    /// </summary>
    [CreateAssetMenu(fileName = "FurniturePrefabLibrary", menuName = "SuperRealEstate/Furniture Prefab Library")]
    public sealed class FurniturePrefabLibrary : ScriptableObject
    {
        [System.Serializable]
        public struct Entry
        {
            [Tooltip("Catalog item id or furniture-asset id this prefab represents.")]
            public string id;
            public GameObject prefab;
        }

        [Tooltip("Each maps a placement id to the prefab to instantiate for it.")]
        [SerializeField] private List<Entry> entries = new List<Entry>();

        /// <summary>Copy this library's id→prefab entries into a lookup map.</summary>
        public void PopulateInto(Dictionary<string, GameObject> map)
        {
            if (map == null) return;
            foreach (Entry e in entries)
            {
                if (string.IsNullOrEmpty(e.id) || e.prefab == null) continue;
                map[e.id] = e.prefab;
            }
        }

        /// <summary>Number of usable (id + prefab) entries.</summary>
        public int Count
        {
            get
            {
                int n = 0;
                foreach (Entry e in entries)
                    if (!string.IsNullOrEmpty(e.id) && e.prefab != null) n++;
                return n;
            }
        }
    }
}
