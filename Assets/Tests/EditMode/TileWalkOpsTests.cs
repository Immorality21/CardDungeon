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
            var steps = TileWalkOps.Path(new Vector2Int(3, 0), new Vector2Int(1, 1), Floor);

            CollectionAssert.AreEqual(new[] { Vector2Int.left, Vector2Int.left, Vector2Int.up }, steps);
            Assert.IsEmpty(TileWalkOps.Path(new Vector2Int(2, 2), new Vector2Int(2, 2), Floor));
        }

        [Test]
        public void Path_GoesAroundWhatIsSolid()
        {
            var solid = new HashSet<Vector2Int> { new Vector2Int(1, 0) };

            var steps = TileWalkOps.Path(new Vector2Int(0, 0), new Vector2Int(2, 0), Floor, solid);

            Assert.AreEqual(4, steps.Count);
        }

        [Test]
        public void Path_WithNoWay_IsNull()
        {
            var wall = new HashSet<Vector2Int> { new Vector2Int(1, 0), new Vector2Int(1, 1), new Vector2Int(1, 2) };

            Assert.IsNull(TileWalkOps.Path(new Vector2Int(0, 0), new Vector2Int(3, 0), Floor, wall));
        }

        [Test]
        public void PathNextTo_StopsBesideTheTarget()
        {
            var target = new Vector2Int(3, 2);
            var solid = new HashSet<Vector2Int> { target };

            var steps = TileWalkOps.PathNextTo(new Vector2Int(0, 2), target, Floor, solid);

            CollectionAssert.AreEqual(new[] { Vector2Int.right, Vector2Int.right }, steps);
        }

        [Test]
        public void Step_IntoSomethingSolid_IsBlocked()
        {
            var solid = new HashSet<Vector2Int> { new Vector2Int(2, 1) };

            var step = TileWalkOps.Step(new Vector2Int(1, 1), Vector2Int.right, Floor, null, null, solid);

            Assert.AreEqual(WalkStepKind.Blocked, step.Kind);
        }

        [Test]
        public void Solid_LeavesWalkableWhatWouldCutADoorOff()
        {
            // A 2x2 room, doors in opposite corners: both free tiles occupied would part them.
            var floor = new RectInt(0, 0, 2, 2);
            var doors = new List<Vector2Int> { new Vector2Int(0, 0), new Vector2Int(1, 1) };
            var occupied = new[] { new Vector2Int(1, 0), new Vector2Int(0, 1) };

            var solid = TileWalkOps.Solid(floor, occupied, doors);

            CollectionAssert.AreEquivalent(new[] { new Vector2Int(1, 0) }, solid);
        }

        [Test]
        public void Solid_NeverIncludesATileThatMustBeReached()
        {
            var door = new Vector2Int(0, 0);

            var solid = TileWalkOps.Solid(Floor, new[] { door, new Vector2Int(2, 1) }, new List<Vector2Int> { door });

            CollectionAssert.AreEquivalent(new[] { new Vector2Int(2, 1) }, solid);
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
