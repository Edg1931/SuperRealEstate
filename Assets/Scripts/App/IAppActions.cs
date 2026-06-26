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
        Task ShowCompsAsync(CancellationToken ct = default);
    }
}
