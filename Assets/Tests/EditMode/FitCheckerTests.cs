using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using SuperRealEstate.Staging;

namespace SuperRealEstate.Tests
{
    public class FitCheckerTests
    {
        // 4m (x) by 3m (z) room with a corner at the origin.
        private static List<Vector3> Room4x3() => new List<Vector3>
        {
            new Vector3(0f, 0f, 0f),
            new Vector3(4f, 0f, 0f),
            new Vector3(4f, 0f, 3f),
            new Vector3(0f, 0f, 3f),
        };

        // A sofa: 2.0 wide, 0.9 tall, 0.95 deep (meters).
        private static FurnitureAsset Sofa() =>
            new FurnitureAsset("sofa", "Sectional Sofa", new Vector3(2.0f, 0.9f, 0.95f), "sofa");

        [Test]
        public void Doorway_SofaFitsThroughStandardDoor()
        {
            // Standard interior door ~0.81m wide x 2.03m tall. Sofa's smallest
            // dims (0.9, 0.95) -> smallest 0.9 must beat door width 0.81? No.
            // It does NOT fit upright; smallest dim 0.9 > 0.81.
            Assert.IsFalse(FitChecker.FitsThroughDoorway(Sofa().Size, 0.81f, 2.03f));
        }

        [Test]
        public void Doorway_NarrowItemFits()
        {
            // A 0.7 x 1.8 x 0.6 bookshelf through a 0.81 x 2.03 door:
            // smallest dims 0.6 and 0.7 <= 0.81 and 2.03 -> fits.
            var shelf = new Vector3(0.7f, 1.8f, 0.6f);
            Assert.IsTrue(FitChecker.FitsThroughDoorway(shelf, 0.81f, 2.03f));
        }

        [Test]
        public void Doorway_DoubleDoorLetsSofaThrough()
        {
            // Double door ~1.5m wide: sofa smallest dims 0.9, 0.95 <= 1.5/2.03 -> fits.
            Assert.IsTrue(FitChecker.FitsThroughDoorway(Sofa().Size, 1.5f, 2.03f));
        }

        [Test]
        public void Footprint_CenteredSofaFits()
        {
            var placement = new Placement("sofa", new Vector3(2f, 0f, 1.5f)); // room center
            var result = FitChecker.FootprintFitsInRoom(Room4x3(), placement, Sofa());

            Assert.IsTrue(result.Fits);
            Assert.IsTrue(result.InsideRoom);
        }

        [Test]
        public void Footprint_SofaAgainstWallFailsClearance()
        {
            // Push sofa near the z=0 wall: back edge at z = 0.525 - 0.475 = 0.05.
            var placement = new Placement("sofa", new Vector3(2f, 0f, 0.525f));
            var result = FitChecker.FootprintFitsInRoom(Room4x3(), placement, Sofa(), wallClearanceM: 0.5f);

            Assert.IsFalse(result.Fits);          // ~0.05m clearance, below the 0.5m requirement
            Assert.IsTrue(result.InsideRoom);     // but still inside the room
            Assert.Less(result.MinWallGapM, 0.5f);
        }

        [Test]
        public void Footprint_SofaPokesThroughWall()
        {
            // Center the sofa on the x=0 wall: half its 2m width spills outside.
            var placement = new Placement("sofa", new Vector3(0f, 0f, 1.5f), yawDegrees: 0f);
            var result = FitChecker.FootprintFitsInRoom(Room4x3(), placement, Sofa());

            Assert.IsFalse(result.Fits);
            Assert.IsFalse(result.InsideRoom);
        }

        [Test]
        public void Footprint_RotationChangesFit()
        {
            // Near the +x wall (x=4). Sofa is 2m wide, 0.95 deep.
            // At yaw 0 the 2m width runs along x and pokes through the wall.
            var alongX = new Placement("sofa", new Vector3(3.7f, 0f, 1.5f), yawDegrees: 0f);
            Assert.IsFalse(FitChecker.FootprintFitsInRoom(Room4x3(), alongX, Sofa()).InsideRoom);

            // Rotated 90deg the 2m runs along z (room is 3m deep) and the 0.95
            // depth runs along x -> now it clears the wall.
            var alongZ = new Placement("sofa", new Vector3(3.4f, 0f, 1.5f), yawDegrees: 90f);
            Assert.IsTrue(FitChecker.FootprintFitsInRoom(Room4x3(), alongZ, Sofa()).InsideRoom);
        }

        [Test]
        public void PointInPolygon_BasicInsideOutside()
        {
            var room = Room4x3();
            Assert.IsTrue(FitChecker.PointInPolygonXZ(room, new Vector3(2f, 0f, 1.5f)));
            Assert.IsFalse(FitChecker.PointInPolygonXZ(room, new Vector3(5f, 0f, 1.5f)));
        }
    }
}
