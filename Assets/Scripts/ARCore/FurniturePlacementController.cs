using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using SuperRealEstate.Staging;
using SuperRealEstate.Capture;

namespace SuperRealEstate.ARCore
{
    /// <summary>
    /// Place a captured (or catalog) piece of furniture in a real room to-scale and
    /// instantly see whether it fits — the payoff of the whole capture feature.
    /// Each placement renders a to-scale box (the furniture's real W×H×D), colored
    /// by the <see cref="FitChecker"/> verdict against the room outline: green = fits
    /// with clearance, amber = tight / past a wall. Drop the real mesh in later by
    /// swapping the box for the asset's model — the fit logic is unchanged.
    ///
    /// Setup: give it the room's floor outline (from a scan/measure) via
    /// <see cref="SetRoomOutline"/>, add furniture to the library (e.g. from a
    /// capture via <see cref="AddFromCapture"/>), select one, and place it at a
    /// gazed floor point with <see cref="PlaceAt"/>.
    /// </summary>
    public sealed class FurniturePlacementController : MonoBehaviour
    {
        [Tooltip("Required clearance (m) to the nearest wall before it reads as 'tight'.")]
        [SerializeField] private float wallClearanceM = 0.05f;

        [Serializable] public sealed class FitEvent : UnityEvent<string> { }
        [Serializable] public sealed class BoolEvent : UnityEvent<bool> { }

        [Tooltip("Human-readable fit result for the HUD.")]
        public FitEvent OnFitText = new FitEvent();
        [Tooltip("True when the last placement fits.")]
        public BoolEvent OnFits = new BoolEvent();

        private readonly Dictionary<string, FurnitureAsset> _library = new Dictionary<string, FurnitureAsset>();
        private readonly List<GameObject> _placed = new List<GameObject>();
        private List<Vector3> _roomOutline;
        private FurnitureAsset _selected;
        private Transform _root;
        private Material _material;

        /// <summary>The room's floor polygon (world XZ) used for the fit check.</summary>
        public void SetRoomOutline(List<Vector3> outline) => _roomOutline = outline;

        /// <summary>Add/replace a furniture asset in the library.</summary>
        public void AddAsset(FurnitureAsset asset)
        {
            if (asset == null || string.IsNullOrEmpty(asset.Id)) return;
            _library[asset.Id] = asset;
        }

        /// <summary>Map a finished capture into the library (to-scale from its bounds).</summary>
        public FurnitureAsset AddFromCapture(FurnitureCaptureResult result)
        {
            if (result == null || string.IsNullOrEmpty(result.AssetId)) return null;
            CaptureBounds b = result.Bounds;
            var asset = new FurnitureAsset
            {
                Id = result.AssetId,
                Name = result.Name,
                Size = new Vector3(b.WidthM, b.HeightM, b.DepthM), // x=width, y=height, z=depth
                ModelUrl = result.ModelUrl,
                ThumbnailUrl = result.ThumbnailUrl,
            };
            AddAsset(asset);
            return asset;
        }

        /// <summary>Choose which library item the next <see cref="PlaceAt"/> drops.</summary>
        public bool Select(string assetId)
        {
            if (assetId != null && _library.TryGetValue(assetId, out FurnitureAsset a)) { _selected = a; return true; }
            return false;
        }

        /// <summary>
        /// Place the selected item at a floor point, render it to-scale, and fit-check
        /// it against the room. Returns the fit verdict.
        /// </summary>
        public FitResult PlaceAt(Vector3 floorPoint, float yawDegrees = 0f)
        {
            if (_selected == null)
            {
                OnFitText.Invoke("pick a piece of furniture first");
                return new FitResult(false, false, 0f, "No furniture selected.");
            }

            var placement = new Placement(_selected.Id, new Vector3(floorPoint.x, 0f, floorPoint.z), yawDegrees);

            FitResult fit = _roomOutline != null && _roomOutline.Count >= 3
                ? FitChecker.FootprintFitsInRoom(_roomOutline, placement, _selected, wallClearanceM)
                : new FitResult(true, true, 0f, "Placed (no room outline to check against).");

            SpawnBox(_selected, placement, fit.Fits);

            OnFits.Invoke(fit.Fits);
            OnFitText.Invoke(fit.Fits
                ? (fit.MinWallGapM > 0f ? $"Fits — {fit.MinWallGapM:0.0} m to the nearest wall." : "Fits.")
                : fit.Reason);
            return fit;
        }

        /// <summary>Remove all placed furniture.</summary>
        public void Clear()
        {
            EnsureRoot();
            foreach (GameObject go in _placed) if (go != null) DestroySafe(go);
            _placed.Clear();
        }

        // --- rendering (to-scale placeholder box; swap for the real mesh later) ---

        private void SpawnBox(FurnitureAsset asset, Placement placement, bool fits)
        {
            EnsureRoot();
            Vector3 size = asset.Size;
            if (size.x <= 0f || size.y <= 0f || size.z <= 0f) size = new Vector3(0.6f, 0.6f, 0.6f);

            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = $"Furniture_{asset.Name}";
            var col = go.GetComponent<Collider>();
            if (col != null) DestroySafe(col); // don't intercept the gaze raycast

            go.transform.SetParent(_root, worldPositionStays: false);
            go.transform.localPosition = new Vector3(placement.Position.x, size.y * 0.5f, placement.Position.z);
            go.transform.localRotation = Quaternion.Euler(0f, placement.YawDegrees, 0f);
            go.transform.localScale = size;

            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                // Translucent green when it fits, amber when it doesn't.
                Color c = fits ? new Color(0.36f, 0.75f, 0.54f, 0.55f) : new Color(0.88f, 0.66f, 0.31f, 0.6f);
                mr.sharedMaterial = TintedMaterial(c);
            }
            _placed.Add(go);
        }

        private Material TintedMaterial(Color color)
        {
            if (_material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard") ?? Shader.Find("Sprites/Default");
                _material = new Material(shader) { name = "FurnitureFit" };
            }
            return new Material(_material) { color = color };
        }

        private void EnsureRoot()
        {
            if (_root != null) return;
            _root = new GameObject("PlacedFurniture").transform;
            _root.SetParent(transform, worldPositionStays: false);
        }

        private static void DestroySafe(UnityEngine.Object o)
        {
            if (Application.isPlaying) Destroy(o); else DestroyImmediate(o);
        }

        private void OnDestroy()
        {
            Clear();
            if (_material != null) DestroySafe(_material);
        }
    }
}
