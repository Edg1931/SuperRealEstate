using System;
using UnityEngine;

namespace SuperRealEstate.Renovation
{
    /// <summary>How the AR layer presents a removed wall on passthrough hardware.</summary>
    public enum PortalRenderMode
    {
        /// <summary>Render the revealed (scanned) adjacent room as an opaque surface
        /// covering the real wall. Simplest; the real wall sits behind it.</summary>
        OpaqueOverlay,

        /// <summary>Stencil the wall's silhouette and show the revealed room only
        /// inside it (depth-composited with real foreground). Cleaner edges.</summary>
        StencilCutout
    }

    /// <summary>
    /// The vertical quad a wall occupies in world space — the "aperture" through
    /// which a removed wall reveals what's behind. Corners are ordered
    /// bottom-start, bottom-end, top-end, top-start.
    /// </summary>
    public readonly struct WallAperture
    {
        public readonly Vector3[] Corners; // length 4
        public readonly Vector3 Center;
        public readonly Vector3 Normal;    // horizontal, perpendicular to the wall run
        public readonly float AreaM2;

        public WallAperture(Vector3[] corners, Vector3 center, Vector3 normal, float areaM2)
        {
            Corners = corners;
            Center = center;
            Normal = normal;
            AreaM2 = areaM2;
        }
    }

    /// <summary>
    /// A "removed wall" presented as a portal into an already-scanned adjacent
    /// space. Because we have a prior capture of what's behind the wall, this is
    /// portal compositing — NOT live diminished-reality inpainting. See
    /// docs/Diminished-Reality.md.
    /// </summary>
    [Serializable]
    public sealed class RemovedWallPortal
    {
        public string Id;
        public string WallId;                 // the wall being virtually removed
        public string RevealedCaptureId;      // scanned model/room shown behind it
        public PortalRenderMode Mode = PortalRenderMode.StencilCutout;

        /// <summary>Whether the real wall is still physically present (see-through
        /// only) vs. an empty shell you can walk through.</summary>
        public bool PhysicalWallStillPresent = true;
    }

    /// <summary>Computes the world-space aperture quad of a wall. Pure + testable.</summary>
    public static class WallApertureBuilder
    {
        public static WallAperture Build(Wall wall, float floorY = 0f)
        {
            if (wall == null) throw new ArgumentNullException(nameof(wall));

            Vector3 a = new Vector3(wall.Start.x, floorY, wall.Start.y);
            Vector3 b = new Vector3(wall.End.x, floorY, wall.End.y);
            float h = Mathf.Max(0f, wall.HeightM);
            Vector3 up = new Vector3(0f, h, 0f);

            var corners = new[]
            {
                a,          // bottom-start
                b,          // bottom-end
                b + up,     // top-end
                a + up,     // top-start
            };

            Vector3 center = (a + b) + (up * 0.5f);
            center = new Vector3((a.x + b.x) * 0.5f, floorY + h * 0.5f, (a.z + b.z) * 0.5f);

            Vector3 run = (b - a);
            Vector3 normal = run.sqrMagnitude > 1e-8f
                ? Vector3.Normalize(Vector3.Cross(Vector3.up, run))
                : Vector3.forward;

            return new WallAperture(corners, center, normal, wall.LengthM * h);
        }
    }
}
