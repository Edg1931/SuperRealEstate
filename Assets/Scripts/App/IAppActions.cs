using System.Threading;
using System.Threading.Tasks;

namespace SuperRealEstate.App
{
    /// <summary>
    /// The set of things the app can actually do — implemented by the AR/scene
    /// layer. The <see cref="ActionDispatcher"/> routes voice commands (and tool
    /// selections) here after checking the feature is available on the device,
    /// so the voice loop, the radial menu, and any other trigger share one path.
    /// </summary>
    public interface IAppActions
    {
        Task MeasureRoomAsync(CancellationToken ct = default);
        Task IdentifyPlantAsync(CancellationToken ct = default);
        Task EstimateMaterialAsync(string material, string parameters, CancellationToken ct = default);
        Task RecognizeFinishAsync(CancellationToken ct = default);
        Task RemoveWallAsync(string wallId, CancellationToken ct = default);
        Task StageFurnitureAsync(string item, CancellationToken ct = default);

        /// <summary>
        /// AI-stage the whole room: the staging director proposes a layout from
        /// the catalog + the user's own captured furniture, validated against the
        /// measured room geometry. <paramref name="style"/> is a free-text brief
        /// ("warm modern", "family friendly"); empty means a neutral default.
        /// </summary>
        Task AutoStageAsync(string style, CancellationToken ct = default);

        Task ShowCompsAsync(CancellationToken ct = default);
    }
}
