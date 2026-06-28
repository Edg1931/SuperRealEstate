using System.Collections.Generic;
using UnityEngine;

namespace SuperRealEstate.Renovation
{
    /// <summary>
    /// A vertical surface captured during an on-device room scan — one detected
    /// wall plane, described in world space. <see cref="Center"/> is the plane
    /// center, <see cref="RightAxis"/> runs along the wall (its long, horizontal
    /// axis), <see cref="Width"/> is the extent along that axis, and
    /// <see cref="Height"/> is the vertical extent. AR Foundation's
    /// <c>ARPlane</c> maps directly: center = transform.position, right axis =
    /// transform.right, width = size.x, height = size.y.
    /// </summary>
    public readonly struct ScannedSurface
    {
        public readonly Vector3 Center;
        public readonly Vector3 RightAxis;
        public readonly float Width;
        public readonly float Height;

        public ScannedSurface(Vector3 center, Vector3 rightAxis, float width, float height)
        {
            Center = center;
            RightAxis = rightAxis;
            Width = width;
            Height = height;
        }
    }

    /// <summary>
    /// Pure conversion from scanned vertical surfaces into an editable
    /// <see cref="BuildingModel"/> — the geometry side of an on-device room scan
    /// (the "walk the room, capture the walls" capture path that complements the
    /// CubiCasa/Matterport importers). Each surface becomes a <see cref="Wall"/>
    /// whose endpoints are the plane's horizontal extent projected onto the floor
    /// plane (x = east, z → plan y = north), matching the plan-view convention the
    /// importers and <see cref="WallApertureBuilder"/> use.
    ///
    /// Network-free and Unity-AR-free (takes plain surface descriptors), so it's
    /// unit-tested without a device. The thin AR glue that reads
    /// <c>ARPlaneManager</c> and feeds this lives in the ARCore assembly.
    /// </summary>
    public static class WallScanBuilder
    {
        /// <summary>Minimum wall length (m) to keep — filters tiny stray planes.</summary>
        public const float MinWallLengthM = 0.4f;

        /// <summary>
        /// Build an editable model from scanned surfaces. Skips degenerate/short
        /// surfaces. Endpoints are on the floor plane at <paramref name="floorY"/>;
        /// each wall's <see cref="Wall.HeightM"/> is the surface's vertical extent.
        /// </summary>
        public static BuildingModel BuildModel(IReadOnlyList<ScannedSurface> surfaces, float floorY = 0f, string id = null)
        {
            var model = new BuildingModel
            {
                Id = id,
                SourceType = "ar_scan",
                Walls = new List<Wall>(),
                Rooms = new List<RoomDef>(),
            };
            if (surfaces == null) return model;

            int n = 0;
            foreach (ScannedSurface s in surfaces)
            {
                if (TryBuildWall(s, floorY, $"w{n}", out Wall wall))
                {
                    model.Walls.Add(wall);
                    n++;
                }
            }
            return model;
        }

        /// <summary>
        /// Convert one surface to a wall on the floor plane. Returns false for
        /// surfaces shorter than <see cref="MinWallLengthM"/> or with a degenerate
        /// horizontal axis.
        /// </summary>
        public static bool TryBuildWall(ScannedSurface s, float floorY, string id, out Wall wall)
        {
            wall = null;

            // Horizontal component of the wall's long axis (drop any tilt).
            Vector3 right = new Vector3(s.RightAxis.x, 0f, s.RightAxis.z);
            if (right.sqrMagnitude < 1e-6f || s.Width < MinWallLengthM)
                return false;

            right = right.normalized;
            float half = s.Width * 0.5f;

            Vector3 a = s.Center - right * half;
            Vector3 b = s.Center + right * half;

            wall = new Wall
            {
                Id = id,
                Start = new Vector2(a.x, a.z),
                End = new Vector2(b.x, b.z),
                HeightM = s.Height > 0.1f ? s.Height : 2.5f,
                ThicknessM = 0.1f,
                IsExterior = false,
                Openings = new List<Opening>(),
            };
            return true;
        }
    }
}
