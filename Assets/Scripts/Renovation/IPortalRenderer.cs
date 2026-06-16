using System.Threading;
using System.Threading.Tasks;

namespace SuperRealEstate.Renovation
{
    /// <summary>
    /// The AR-layer contract for showing a removed wall as a portal into a
    /// pre-scanned adjacent space (see docs/Diminished-Reality.md). The concrete
    /// implementation is platform-specific:
    ///
    ///  • visionOS (PolySpatial): place an opaque/stencilled surface over the
    ///    wall aperture showing the revealed capture; rely on scene depth so
    ///    real foreground occludes it. Bounded by Apple's passthrough privacy
    ///    model (occlude the wall, don't sample arbitrary camera pixels).
    ///  • Android XR / Quest: passthrough + scene mesh; same approach, with more
    ///    latitude for stencil/depth compositing.
    ///
    /// This is portal *compositing* (we know what's behind from the scan), not
    /// live inpainting — which is why it's feasible today.
    /// </summary>
    public interface IPortalRenderer
    {
        /// <summary>
        /// Reveal <paramref name="portal"/>'s adjacent capture across the given
        /// wall aperture. Returns when the portal is anchored and rendering.
        /// </summary>
        Task ShowPortalAsync(RemovedWallPortal portal, WallAperture aperture, CancellationToken ct = default);

        /// <summary>Hide a portal (toggle back to the existing/real wall).</summary>
        Task HidePortalAsync(string portalId, CancellationToken ct = default);
    }
}
