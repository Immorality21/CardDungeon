using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Rooms;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    public class RoomTileOpsTests
    {
        private static readonly RectInt Floor = new RectInt(10, 20, 4, 3);

        [Test]
        public void Plan_WithOneWallRow_AddsAFaceRowAboveTheFloor()
        {
            var cells = RoomTileOps.Plan(Floor, 1, new List<RoomDoorway>());

            Assert.AreEqual(4 * 4, cells.Count);
            Assert.IsTrue(cells.Where(c => c.Cell.y == 23).All(c => c.Piece == RoomTilePiece.WallFace));
            Assert.IsTrue(cells.Where(c => c.Cell.y < 23).All(c => c.Piece == RoomTilePiece.Floor));
        }

        [Test]
        public void Plan_NorthDoor_IsAnArchwayAboveTheDoorTile()
        {
            var doors = new List<RoomDoorway> { new RoomDoorway(new Vector2Int(12, 22), Vector2Int.up) };

            var cells = RoomTileOps.Plan(Floor, 1, doors);

            Assert.AreEqual(RoomTilePiece.Archway, cells.Single(c => c.Cell == new Vector2Int(12, 23)).Piece);
            Assert.AreEqual(1, cells.Count(c => c.Piece == RoomTilePiece.Archway));
        }

        [Test]
        public void Plan_ThinWalls_RunAroundTheFootprint_TheCapOnTopOfTheFace()
        {
            var cells = RoomTileOps.Plan(Floor, 1, new List<RoomDoorway>());

            var bottomLeft = cells.Single(c => c.Cell == new Vector2Int(10, 20));
            Assert.AreEqual(RoomTileOps.EdgeLeft | RoomTileOps.EdgeBottom, bottomLeft.Edges);
            var faceRight = cells.Single(c => c.Cell == new Vector2Int(13, 23));
            Assert.AreEqual(RoomTileOps.EdgeRight | RoomTileOps.EdgeTop, faceRight.Edges);
            Assert.IsTrue(cells.Where(c => c.Cell.y < 23).All(c => (c.Edges & RoomTileOps.EdgeTop) == 0));
        }

        [Test]
        public void Plan_Archway_HasNoCapOverIt()
        {
            var doors = new List<RoomDoorway> { new RoomDoorway(new Vector2Int(12, 22), Vector2Int.up) };

            var cells = RoomTileOps.Plan(Floor, 1, doors);

            Assert.AreEqual(0, cells.Single(c => c.Cell == new Vector2Int(12, 23)).Edges & RoomTileOps.EdgeTop);
        }

        [Test]
        public void Plan_SideAndSouthDoors_LeaveAGapInTheThinWall()
        {
            var doors = new List<RoomDoorway>
            {
                new RoomDoorway(new Vector2Int(10, 21), Vector2Int.left),
                new RoomDoorway(new Vector2Int(11, 20), Vector2Int.down),
            };

            var cells = RoomTileOps.Plan(Floor, 1, doors);

            Assert.AreEqual(0, cells.Single(c => c.Cell == new Vector2Int(10, 21)).Edges & RoomTileOps.EdgeLeft);
            Assert.AreEqual(0, cells.Single(c => c.Cell == new Vector2Int(11, 20)).Edges & RoomTileOps.EdgeBottom);
            Assert.AreNotEqual(0, cells.Single(c => c.Cell == new Vector2Int(10, 22)).Edges & RoomTileOps.EdgeLeft);
        }

        [Test]
        public void Plan_WithNoWallRows_ClosesTheTopWithAThinWall()
        {
            var cells = RoomTileOps.Plan(Floor, 0, new List<RoomDoorway>());

            Assert.AreEqual(4 * 3, cells.Count);
            Assert.IsTrue(cells.Where(c => c.Cell.y == 22).All(c => (c.Edges & RoomTileOps.EdgeTop) != 0));
        }

        [Test]
        public void PlaceProps_SameSeed_PlacesTheSameProps()
        {
            var props = Props();
            var first = Decorate(props, 1234);
            var second = Decorate(props, 1234);

            CollectionAssert.AreEqual(first.Select(p => (p.Cell, p.PropIndex)).ToList(),
                second.Select(p => (p.Cell, p.PropIndex)).ToList());
        }

        [Test]
        public void PlaceProps_PutsEachKindOnItsOwnPiece_NeverTwoWallPropsSideBySide_NorOnKeepClear()
        {
            var props = Props();
            var keepClear = new HashSet<Vector2Int> { new Vector2Int(11, 21) };
            for (int seed = 0; seed < 50; seed++)
            {
                var cells = RoomTileOps.Plan(Floor, 1, new List<RoomDoorway>());
                var placed = RoomTileOps.PlaceProps(cells, keepClear, props, 1f, 1f, new System.Random(seed));
                var pieceAt = cells.ToDictionary(c => c.Cell, c => c.Piece);

                foreach (var p in placed)
                {
                    var expected = props[p.PropIndex].Place == BiomePropPlace.Wall ? RoomTilePiece.WallFace : RoomTilePiece.Floor;
                    Assert.AreEqual(expected, pieceAt[p.Cell]);
                    Assert.IsFalse(keepClear.Contains(p.Cell));
                }
                var wallCells = placed.Where(p => props[p.PropIndex].Place == BiomePropPlace.Wall).Select(p => p.Cell).ToList();
                Assert.IsFalse(wallCells.Any(c => wallCells.Contains(c + Vector2Int.right)));
            }
        }

        [Test]
        public void Vary_WithOneTileOfAKind_LeavesTheCommonTile()
        {
            var cells = RoomTileOps.Plan(Floor, 1, new List<RoomDoorway>());

            RoomTileOps.Vary(cells, 1, 1f, 1, 1f, new System.Random(7));

            Assert.IsTrue(cells.All(c => c.Variant == 0));
        }

        [Test]
        public void DecorSeed_DiffersByRoomAndBySeed()
        {
            int a = RoomTileOps.DecorSeed(5, new Vector2Int(0, 0));
            Assert.AreNotEqual(a, RoomTileOps.DecorSeed(5, new Vector2Int(0, 4)));
            Assert.AreNotEqual(a, RoomTileOps.DecorSeed(6, new Vector2Int(0, 0)));
            Assert.AreEqual(a, RoomTileOps.DecorSeed(5, new Vector2Int(0, 0)));
        }

        private static List<RoomPropPlacement> Decorate(List<BiomeProp> props, int seed)
        {
            var cells = RoomTileOps.Plan(Floor, 1, new List<RoomDoorway>());
            var rng = new System.Random(seed);
            RoomTileOps.Vary(cells, 3, 0.3f, 2, 0.3f, rng);
            return RoomTileOps.PlaceProps(cells, new HashSet<Vector2Int>(), props, 0.4f, 0.5f, rng);
        }

        private static List<BiomeProp> Props()
        {
            return new List<BiomeProp>
            {
                new BiomeProp { Place = BiomePropPlace.Floor, Weight = 1f },
                new BiomeProp { Place = BiomePropPlace.Wall, Weight = 2f },
                new BiomeProp { Place = BiomePropPlace.Floor, Weight = 0.5f },
            };
        }
    }
}
