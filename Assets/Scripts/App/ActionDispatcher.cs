using System;
using System.Threading;
using System.Threading.Tasks;
using SuperRealEstate.Platform;
using SuperRealEstate.UI;
using SuperRealEstate.Voice;

namespace SuperRealEstate.App
{
    public readonly struct DispatchResult
    {
        public readonly bool Dispatched;     // was an action actually run?
        public readonly AppFeature? Feature; // the feature it needed (if any)
        public readonly bool Available;      // is that feature available on this device?
        public readonly string Reason;       // why blocked / "available" / "no action"

        public DispatchResult(bool dispatched, AppFeature? feature, bool available, string reason)
        {
            Dispatched = dispatched; Feature = feature; Available = available; Reason = reason;
        }
    }

    /// <summary>
    /// The one place that turns intent (a voice command, or a tool selection)
    /// into an app action — after gating it through <see cref="FeatureAvailability"/>
    /// for the current device. So "remove this wall" on optical glasses is
    /// blocked with a reason (and the voice reply can say why) instead of failing.
    /// Pure routing + gating; unit-tested with a fake <see cref="IAppActions"/>.
    /// </summary>
    public sealed class ActionDispatcher
    {
        private readonly IAppActions _actions;

        public ActionDispatcher(IAppActions actions)
        {
            _actions = actions ?? throw new ArgumentNullException(nameof(actions));
        }

        /// <summary>Which feature a voice action needs (null = pure conversation).</summary>
        public static AppFeature? FeatureFor(VoiceActionType type) => type switch
        {
            VoiceActionType.MeasureRoom => AppFeature.RoomMeasurement,
            VoiceActionType.IdentifyPlant => AppFeature.PlantIdentification,
            VoiceActionType.EstimateMaterial => AppFeature.LandscapeCalculators,
            VoiceActionType.RecognizeFinish => AppFeature.SurfaceFinishRecognition,
            VoiceActionType.RemoveWall => AppFeature.WallRemovalPortal,
            VoiceActionType.StageFurniture => AppFeature.VirtualStaging,
            VoiceActionType.AutoStage => AppFeature.VirtualStaging,
            VoiceActionType.ShowComps => AppFeature.PropertyOverlays,
            _ => null,
        };

        /// <summary>Which feature a radial-menu tool maps to.</summary>
        public static AppFeature? FeatureFor(ToolId tool) => tool switch
        {
            ToolId.Measure => AppFeature.RoomMeasurement,
            ToolId.Identify => AppFeature.SurfaceFinishRecognition,
            ToolId.Finish => AppFeature.SurfaceFinishRecognition,
            ToolId.Stage => AppFeature.VirtualStaging,
            ToolId.RemoveWall => AppFeature.WallRemovalPortal,
            ToolId.Landscape => AppFeature.LandscapeCalculators,
            ToolId.Notes => null,
            _ => null,
        };

        /// <summary>Check whether a tool can be opened on this device (no side effects).</summary>
        public static DispatchResult ResolveTool(ToolId tool, XrCapabilities caps)
        {
            AppFeature? feature = FeatureFor(tool);
            if (feature == null) return new DispatchResult(false, null, true, "available");
            bool ok = FeatureAvailability.IsAvailable(caps, feature.Value);
            return new DispatchResult(false, feature, ok, FeatureAvailability.Reason(caps, feature.Value));
        }

        /// <summary>
        /// Run a voice action if its feature is available; otherwise return a
        /// blocked result with the reason (so the reply can explain).
        /// </summary>
        public async Task<DispatchResult> DispatchAsync(VoiceAction action, XrCapabilities caps, CancellationToken ct = default)
        {
            if (action == null || action.Type == VoiceActionType.None || action.Type == VoiceActionType.Unknown)
                return new DispatchResult(false, null, true, "no action");

            AppFeature? feature = FeatureFor(action.Type);
            if (feature != null && !FeatureAvailability.IsAvailable(caps, feature.Value))
                return new DispatchResult(false, feature, false, FeatureAvailability.Reason(caps, feature.Value));

            switch (action.Type)
            {
                case VoiceActionType.MeasureRoom: await _actions.MeasureRoomAsync(ct); break;
                case VoiceActionType.IdentifyPlant: await _actions.IdentifyPlantAsync(ct); break;
                case VoiceActionType.EstimateMaterial: await _actions.EstimateMaterialAsync(action.Material, action.Params, ct); break;
                case VoiceActionType.RecognizeFinish: await _actions.RecognizeFinishAsync(ct); break;
                case VoiceActionType.RemoveWall: await _actions.RemoveWallAsync(action.Target, ct); break;
                case VoiceActionType.StageFurniture: await _actions.StageFurnitureAsync(action.Target, ct); break;
                case VoiceActionType.AutoStage: await _actions.AutoStageAsync(action.Target, ct); break;
                case VoiceActionType.ShowComps: await _actions.ShowCompsAsync(ct); break;
            }
            return new DispatchResult(true, feature, true, "available");
        }
    }
}
