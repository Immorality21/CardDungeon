using System.Collections.Generic;
using Assets.Scripts.Rooms;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    public class TileWalkOpsTests
    {
        private static readonly RectInt Floor = new RectInt(0, 0, 4, 3);

        [Test]
        public void Step_OnTheFloor_Moves()
        {
            var step = TileWalkOps.Step(new Vector2Int(1, 1), Vector2Int.right, Floor, null, null);

            Assert.AreEqual(WalkStepKind.Move, step.Kind);
            Assert.AreEqual(new Vector2Int(2, 1), step.To);
        }

        [Test]
        public void Step_IntoAWallWithNoDoor_IsBlocked()
        {
            var step = TileWalkOps.Step(new Vector2Int(0, 1), Vector2Int.left, Floor, new List<RoomDoorway>(), new List<bool>());

            Assert.AreEqual(WalkStepKind.Blocked, step.Kind);
            Assert.AreEqual(new Vector2Int(0, 1), step.To);
        }

        [Test]
        public void Step_FromTheDoorTileTheWayItLeads_WalksThroughIt()
        {
            var doors = new List<RoomDoorway>
            {
                new RoomDoorway(new Vector2Int(3, 0), Vector2Int.right),
                new RoomDoorway(new Vector2Int(2, 2), Vector2Int.up),
            };

            var step = TileWalkOps.Step(new Vector2Int(2, 2), Vector2Int.up, Floor, doors, new List<bool> { true, true });

            Assert.AreEqual(WalkStepKind.Door, step.Kind);
            Assert.AreEqual(1, step.DoorIndex);
        }

        [Test]
        public void Step_ThroughAClosedDoor_IsBlocked()
        {
            var doors = new List<RoomDoorway> { new RoomDoorway(new Vector2Int(3, 0), Vector2Int.right) };

            var step = TileWalkOps.Step(new Vector2Int(3, 0), Vector2Int.right, Floor, doors, new List<bool> { false });

            Assert.AreEqual(WalkStepKind.Blocked, step.Kind);
        }

        [Test]
        public void Step_ToTheSideOfADoorTile_StaysInTheRoom()
        {
            // Standing on a door tile does not make every edge a door: only the way it leads.
            var doors = new List<RoomDoorway> { new RoomDoorway(new Vector2Int(3, 0), Vector2Int.right) };

            var down = TileWalkOps.Step(new Vector2Int(3, 0), Vector2Int.down, Floor, doors, new List<bool> { true });
            var up = TileWalkOps.Step(new Vector2Int(3, 0), Vector2Int.up, Floor, doors, new List<bool> { true });

            Assert.AreEqual(WalkStepKind.Blocked, down.Kind);
            Assert.AreEqual(WalkStepKind.Move, up.Kind);
        }

        [Test]
        public void Path_GoesAcrossThenAlong_AndIsEmptyOnTheSameTile()
        {
            var steps = TileWalkOps.Path(new Vector2Int(3, 0), new Vector2Int(1, 1));

            CollectionAssert.AreEqual(new[] { Vector2Int.left, Vector2Int.left, Vector2Int.up }, steps);
            Assert.IsEmpty(TileWalkOps.Path(new Vector2Int(2, 2), new Vector2Int(2, 2)));
        }

        [Test]
        public void NearestFloorTile_SnapsAnEvenRoomsCentreOntoTheFloor()
        {
            var tile = TileWalkOps.NearestFloorTile(Floor, new Vector2(1.5f, 1f));

            Assert.IsTrue(Floor.Contains(tile));
            Assert.AreEqual(new Vector2Int(2, 1), tile);
        }

        [Test]
        public void NearestFloorTile_ClampsAPointOutsideTheRoom()
        {
            Assert.AreEqual(new Vector2Int(3, 2), TileWalkOps.NearestFloorTile(Floor, new Vector2(9f, 9f)));
        }
    }
}
