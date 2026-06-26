using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;
using SuperRealEstate.App;
using SuperRealEstate.Insights;
using SuperRealEstate.Landscape;
using SuperRealEstate.MaterialCost;
using SuperRealEstate.RoomMeasure;

namespace SuperRealEstate.ARCore
{
    /// <summary>
    /// Scene-layer implementation of <see cref="IAppActions"/>: the glue that
    /// turns voice/tool commands (routed through the App dispatcher) into actual
    /// AR scene behavior — measure a room, identify a plant, recognize a finish,
    /// estimate material quantities, etc.
    ///
    /// The heavy lifting lives in already-tested assemblies (RoomMeasure,
    /// Insights, Landscape, MaterialCost); this MonoBehaviour wires them to the
    /// AR camera + plane controllers and re-emits results as <see cref="UnityEvent"/>s
    /// the HUD/voice layer can subscribe to.
    ///
    /// Editor / runtime setup:
    ///  - Assign <see cref="roomMeasure"/> in the inspector.
    ///  - Call <see cref="Configure"/> from the bootstrap layer to inject the
    ///    (optional) scene analyzer, plant identifier, and camera frame provider.
    /// </summary>
    public sealed class SceneAppActions : MonoBehaviour, IAppActions
    {
        [Header("Scene controllers")]
        [Tooltip("Room-measurement controller used by MeasureRoom / EstimateMaterial.")]
        [SerializeField] private RoomMeasureController roomMeasure;

        [Header("Events for the HUD / voice layer")]
        [Tooltip("Raised when a room is measured.")]
        public RoomMeasuredEvent OnMeasured = new RoomMeasuredEvent();

        [Tooltip("Raised when plants are identified (human-readable summary).")]
        public StringEvent OnPlants = new StringEvent();

        [Tooltip("Raised when surface finishes are recognized (human-readable summary).")]
        public StringEvent OnFinishes = new StringEvent();

        [Tooltip("Generic status / spoken-style feedback for the user.")]
        public StringEvent OnInfo = new StringEvent();

        [Serializable] public sealed class RoomMeasuredEvent : UnityEvent<RoomMeasurements> { }
        [Serializable] public sealed class StringEvent : UnityEvent<string> { }

        // Injected collaborators (all optional — methods degrade gracefully).
        private EdgeFunctionSceneAnalyzer _sceneAnalyzer;
        private IPlantIdentifier _plantIdentifier;
        private Func<byte[]> _frameProvider;

        // Last successful measurement, so EstimateMaterial can reuse the floor area.
        private RoomMeasurements? _lastMeasurements;

        /// <summary>
        /// Inject the optional analysis collaborators. Wired by the bootstrap /
        /// camera-capture layer once the Supabase config and AR camera are ready.
        /// Any argument may be null; the corresponding action then reports a
        /// friendly "not available" message instead of throwing.
        /// </summary>
        /// <param name="sceneAnalyzer">Scene analyzer (concrete type so finish
        /// recognition can call <c>AnalyzeSceneAsync</c> for surfaces).</param>
        /// <param name="plantIdentifier">Plant identifier.</param>
        /// <param name="frameProvider">Returns a JPEG snapshot from the AR camera.</param>
        public void Configure(
            EdgeFunctionSceneAnalyzer sceneAnalyzer,
            IPlantIdentifier plantIdentifier,
            Func<byte[]> frameProvider)
        {
            _sceneAnalyzer = sceneAnalyzer;
            _plantIdentifier = plantIdentifier;
            _frameProvider = frameProvider;
        }

        /// <inheritdoc />
        public Task MeasureRoomAsync(CancellationToken ct = default)
        {
            if (roomMeasure == null)
            {
                OnInfo.Invoke("room measurement isn't set up");
                return Task.CompletedTask;
            }

            if (roomMeasure.CaptureRoom(out RoomMeasurements m))
            {
                _lastMeasurements = m;
                OnMeasured.Invoke(m);
                OnInfo.Invoke(
                    $"About {m.FloorAreaSqFt:0} sq ft of floor, {m.CeilingHeightFt:0.0} ft ceilings.");
            }
            else
            {
                OnInfo.Invoke("look around so a floor is detected");
            }

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public async Task IdentifyPlantAsync(CancellationToken ct = default)
        {
            if (_plantIdentifier == null || _frameProvider == null)
            {
                OnInfo.Invoke("plant identification isn't available right now");
                return;
            }

            byte[] frame = CaptureFrame();
            if (frame == null || frame.Length == 0)
            {
                OnInfo.Invoke("point the camera at the plant and try again");
                return;
            }

            try
            {
                IReadOnlyList<PlantIdentification> plants =
                    await _plantIdentifier.IdentifyAsync(frame, ct);

                if (plants == null || plants.Count == 0)
                {
                    OnInfo.Invoke("couldn't identify that plant");
                    return;
                }

                PlantIdentification top = plants[0];
                string summary = DescribePlant(top);
                OnPlants.Invoke(summary);
                OnInfo.Invoke(summary);
            }
            catch (OperationCanceledException)
            {
                // Cancellation is not an error — stay quiet.
            }
            catch (Exception e)
            {
                OnInfo.Invoke("plant lookup failed");
                Debug.LogWarning($"[SceneAppActions] IdentifyPlant failed: {e.Message}");
            }
        }

        /// <inheritdoc />
        public async Task RecognizeFinishAsync(CancellationToken ct = default)
        {
            if (_sceneAnalyzer == null || _frameProvider == null)
            {
                OnInfo.Invoke("finish recognition isn't available right now");
                return;
            }

            byte[] frame = CaptureFrame();
            if (frame == null || frame.Length == 0)
            {
                OnInfo.Invoke("point the camera at the surface and try again");
                return;
            }

            try
            {
                var request = new SceneAnalysisRequest
                {
                    FrameImage = frame,
                    Measurements = _lastMeasurements,
                    CategoryFilter = new[] { InsightCategory.Appliance },
                };

                SceneAnalysis analysis = await _sceneAnalyzer.AnalyzeSceneAsync(request, ct);
                IReadOnlyList<SurfaceFinding> surfaces = analysis?.Surfaces;

                if (surfaces == null || surfaces.Count == 0)
                {
                    OnInfo.Invoke("couldn't recognize the finish");
                    return;
                }

                string summary = DescribeSurfaces(surfaces);
                OnFinishes.Invoke(summary);
                OnInfo.Invoke(summary);
            }
            catch (OperationCanceledException)
            {
                // Cancellation is not an error — stay quiet.
            }
            catch (Exception e)
            {
                OnInfo.Invoke("finish recognition failed");
                Debug.LogWarning($"[SceneAppActions] RecognizeFinish failed: {e.Message}");
            }
        }

        /// <inheritdoc />
        public Task EstimateMaterialAsync(string material, string parameters, CancellationToken ct = default)
        {
            if (_lastMeasurements == null)
            {
                OnInfo.Invoke("measure the area first");
                return Task.CompletedTask;
            }

            float areaSqM = _lastMeasurements.Value.FloorAreaSqM;
            float depthInches = ParseDepthInches(parameters, defaultInches: 3f);
            string kind = (material ?? string.Empty).Trim().ToLowerInvariant();

            string result;
            switch (kind)
            {
                case "mulch":
                case "gravel":
                case "soil":
                case "topsoil":
                {
                    float yards = BulkMaterialCalculator.CubicYards(areaSqM, depthInches);
                    int bags = BulkMaterialCalculator.Bags(areaSqM, depthInches);
                    result = $"About {yards:0.#} cu yd ({bags} bags) of {kind} at {depthInches:0.#}\" deep.";
                    break;
                }
                case "concrete":
                {
                    float yards = BulkMaterialCalculator.ConcreteCubicYards(areaSqM, depthInches);
                    result = $"About {yards:0.#} cu yd of concrete at {depthInches:0.#}\" thick.";
                    break;
                }
                case "sod":
                {
                    float sqft = BulkMaterialCalculator.SodSqFt(areaSqM);
                    result = $"About {sqft:0} sq ft of sod (with waste).";
                    break;
                }
                case "pavers":
                {
                    // Common 12"x12" paver as a sensible default.
                    int pavers = BulkMaterialCalculator.Pavers(areaSqM, 12f, 12f);
                    result = $"About {pavers} pavers (12\" × 12\", with waste).";
                    break;
                }
                case "fence":
                {
                    // Use the room perimeter as the fence run length.
                    FenceTakeoff fence = BulkMaterialCalculator.Fence(_lastMeasurements.Value.PerimeterM);
                    result = $"About {fence.LinearFeet:0} ft of fence: {fence.Panels} panels, {fence.Posts} posts.";
                    break;
                }
                case "paint":
                {
                    // Paint covers walls, not the floor.
                    float wallSqM = _lastMeasurements.Value.WallAreaSqM;
                    int gallons = FinishQuantity.PaintGallons(wallSqM);
                    result = $"About {gallons} gal of paint for {wallSqM * FinishQuantity.SqFtPerSqM:0} sq ft of wall (2 coats).";
                    break;
                }
                case "flooring":
                {
                    // Default 20 ft²/box laminate/hardwood carton.
                    int boxes = FinishQuantity.FlooringBoxes(areaSqM, sqFtPerBox: 20f);
                    result = $"About {boxes} boxes of flooring (with waste).";
                    break;
                }
                default:
                    OnInfo.Invoke(string.IsNullOrWhiteSpace(material)
                        ? "tell me which material to estimate"
                        : $"not sure how to estimate {material} yet");
                    return Task.CompletedTask;
            }

            OnInfo.Invoke(result);
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task RemoveWallAsync(string wallId, CancellationToken ct = default)
        {
            // Placeholder so the dispatcher path is complete end to end; the
            // renovation/mesh-edit layer isn't wired here yet.
            OnInfo.Invoke($"Removing wall {wallId}… (preview coming soon)");
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task StageFurnitureAsync(string item, CancellationToken ct = default)
        {
            // Placeholder — staging layer not wired here yet.
            OnInfo.Invoke($"Staging {item}… (preview coming soon)");
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task ShowCompsAsync(CancellationToken ct = default)
        {
            // Placeholder — comps layer not wired here yet.
            OnInfo.Invoke("Pulling up comparable sales… (preview coming soon)");
            return Task.CompletedTask;
        }

        // --- helpers ---

        private byte[] CaptureFrame()
        {
            Func<byte[]> provider = _frameProvider;
            return provider != null ? provider() : null;
        }

        /// <summary>
        /// Parse a depth like "depth=3in" / "depth=3" / "3in" from the parameter
        /// string. Falls back to <paramref name="defaultInches"/> when absent.
        /// </summary>
        private static float ParseDepthInches(string parameters, float defaultInches)
        {
            if (string.IsNullOrWhiteSpace(parameters)) return defaultInches;

            foreach (string rawToken in parameters.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string token = rawToken.Trim();
                int eq = token.IndexOf('=');
                string value = eq >= 0 ? token.Substring(eq + 1) : token;
                if (eq >= 0 && !token.Substring(0, eq).Trim().Equals("depth", StringComparison.OrdinalIgnoreCase))
                    continue;

                // Strip a trailing unit suffix (in / inch / inches / ").
                value = value.Trim().TrimEnd('"');
                value = StripSuffix(value, "inches");
                value = StripSuffix(value, "inch");
                value = StripSuffix(value, "in");

                if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed) && parsed > 0f)
                    return parsed;
            }

            return defaultInches;
        }

        private static string StripSuffix(string value, string suffix)
            => value.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)
                ? value.Substring(0, value.Length - suffix.Length).Trim()
                : value;

        private static string DescribePlant(PlantIdentification p)
        {
            if (p == null) return "couldn't identify that plant";

            string name = !string.IsNullOrWhiteSpace(p.CommonName)
                ? p.CommonName
                : (!string.IsNullOrWhiteSpace(p.ScientificName) ? p.ScientificName : "an unknown plant");

            string extra = "";
            if (!string.IsNullOrWhiteSpace(p.CareLevel)) extra += $", {p.CareLevel} care";
            if (p.ToxicToPetsOrKids) extra += ", toxic to pets/kids";
            if (p.Invasive) extra += ", invasive";

            return $"Looks like {name}{extra}.";
        }

        private static string DescribeSurfaces(IReadOnlyList<SurfaceFinding> surfaces)
        {
            var parts = new List<string>(surfaces.Count);
            foreach (SurfaceFinding s in surfaces)
            {
                if (s == null) continue;

                string where = !string.IsNullOrWhiteSpace(s.SurfaceKind) ? s.SurfaceKind : "surface";
                string what = !string.IsNullOrWhiteSpace(s.Product)
                    ? s.Product
                    : (!string.IsNullOrWhiteSpace(s.MaterialType) ? s.MaterialType : "finish");

                string brand = !string.IsNullOrWhiteSpace(s.Brand) ? $"{s.Brand} " : "";
                parts.Add($"{where}: {brand}{what}");
            }

            return parts.Count == 0
                ? "couldn't recognize the finish"
                : string.Join("; ", parts) + ".";
        }
    }
}
