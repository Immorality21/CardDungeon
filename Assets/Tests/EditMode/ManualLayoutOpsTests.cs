using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Dungeon;
using Assets.Scripts.Rooms;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Assets.Tests.EditMode
{
    public class ManualLayoutOpsTests
    {
        private static Vector2Int V(int x, int y)
        {
            return new Vector2Int(x, y);
        }

        [Test]
        public void WithWallRows_NoWallRows_ChangesNothing()
        {
            var positions = new List<Vector2Int> { V(0, 0), V(0, 4) };
            var sizes = new List<Vector2Int> { V(4, 4), V(4, 4) };

            var placed = ManualLayoutOps.WithWallRows(positions, sizes, new List<Vector2Int> { V(0, 1) }, 0, 0);

            CollectionAssert.AreEqual(positions, placed);
        }

        [Test]
        public void WithWallRows_SideDoor_KeepsTheOffset()
        {
            var positions = new List<Vector2Int> { V(0, 0), V(4, 1) };
            var sizes = new List<Vector2Int> { V(4, 4), V(5, 2) };

            var placed = ManualLayoutOps.WithWallRows(positions, sizes, new List<Vector2Int> { V(0, 1) }, 0, 1);

            CollectionAssert.AreEqual(positions, placed);
        }

        [Test]
        public void WithWallRows_NorthDoor_MovesTheRoomAboveUpOneRow()
        {
            var positions = new List<Vector2Int> { V(0, 0), V(0, 4) };
            var sizes = new List<Vector2Int> { V(4, 4), V(4, 4) };

            var placed = ManualLayoutOps.WithWallRows(positions, sizes, new List<Vector2Int> { V(0, 1) }, 0, 1);

            Assert.AreEqual(V(0, 5), placed[1]);
        }

        [Test]
        public void WithWallRows_SouthDoor_MovesTheRoomBelowDownOneRow()
        {
            var positions = new List<Vector2Int> { V(0, 0), V(0, -4) };
            var sizes = new List<Vector2Int> { V(4, 4), V(4, 4) };

            var placed = ManualLayoutOps.WithWallRows(positions, sizes, new List<Vector2Int> { V(0, 1) }, 0, 1);

            Assert.AreEqual(V(0, -5), placed[1]);
        }

        [Test]
        public void WithWallRows_RoomsPastADoor_MoveWithIt()
        {
            // Start, a room above it, a room beside that one, and another above it.
            var positions = new List<Vector2Int> { V(0, 0), V(0, 4), V(4, 4), V(0, 8) };
            var sizes = new List<Vector2Int> { V(4, 4), V(4, 4), V(4, 4), V(4, 4) };
            var doors = new List<Vector2Int> { V(0, 1), V(1, 2), V(1, 3) };

            var placed = ManualLayoutOps.WithWallRows(positions, sizes, doors, 0, 1);

            Assert.AreEqual(V(0, 5), placed[1]);
            Assert.AreEqual(V(4, 5), placed[2]);
            Assert.AreEqual(V(0, 10), placed[3]);
        }

        [Test]
        public void WithWallRows_StartIsNotTheFirstRoom_StartStaysPut()
        {
            var positions = new List<Vector2Int> { V(0, 4), V(0, 0) };
            var sizes = new List<Vector2Int> { V(4, 4), V(4, 4) };

            var placed = ManualLayoutOps.WithWallRows(positions, sizes, new List<Vector2Int> { V(0, 1) }, 1, 1);

            Assert.AreEqual(V(0, 0), placed[1]);
            Assert.AreEqual(V(0, 5), placed[0]);
        }

        [Test]
        public void WithWallRows_RoomNoDoorReaches_KeepsItsPlace()
        {
            var positions = new List<Vector2Int> { V(0, 0), V(0, 4), V(20, 20) };
            var sizes = new List<Vector2Int> { V(4, 4), V(4, 4), V(4, 4) };

            var placed = ManualLayoutOps.WithWallRows(positions, sizes, new List<Vector2Int> { V(0, 1) }, 0, 1);

            Assert.AreEqual(V(20, 20), placed[2]);
        }

        /// <summary>Every authored layout, as built with a wall row: no rooms overlap, every door fits.</summary>
        [Test]
        public void EveryManualLayout_WithAWallRow_StillFits()
        {
            var layouts = AssetDatabase.FindAssets("t:ManualLevelLayoutSO")
                .Select(guid => AssetDatabase.LoadAssetAtPath<ManualLevelLayoutSO>(AssetDatabase.GUIDToAssetPath(guid)))
                .ToList();
            Assert.IsNotEmpty(layouts);

            foreach (var layout in layouts)
            {
                var sizes = layout.Rooms.Select(r => new Vector2Int(r.RoomTemplate.Width, r.RoomTemplate.Height)).ToList();
                var placed = ManualLayoutOps.WithWallRows(
                    layout.Rooms.Select(r => r.GridPosition).ToList(),
                    sizes,
                    layout.Doors.Select(d => new Vector2Int(d.RoomIndexA, d.RoomIndexB)).ToList(),
                    layout.StartRoomIndex,
                    1);

                for (int a = 0; a < placed.Count; a++)
                {
                    for (int b = a + 1; b < placed.Count; b++)
                    {
                        Assert.IsFalse(Overlap(placed[a], sizes[a], placed[b], sizes[b]),
                            $"{layout.name}: rooms {a} and {b} overlap with a wall row");
                    }
                }

                foreach (var door in layout.Doors)
                {
                    Assert.IsTrue(Touch(placed[door.RoomIndexA], sizes[door.RoomIndexA], placed[door.RoomIndexB], sizes[door.RoomIndexB]),
                        $"{layout.name}: door {door.RoomIndexA}-{door.RoomIndexB} no longer fits with a wall row");
                }
            }
        }

        // A footprint is the floor plus its wall row.
        private static bool Overlap(Vector2Int a, Vector2Int aSize, Vector2Int b, Vector2Int bSize)
        {
            return a.x < b.x + bSize.x && b.x < a.x + aSize.x
                && a.y < b.y + bSize.y + 1 && b.y < a.y + aSize.y + 1;
        }

        private static bool Touch(Vector2Int a, Vector2Int aSize, Vector2Int b, Vector2Int bSize)
        {
            bool xOverlap = a.x < b.x + bSize.x && b.x < a.x + aSize.x;
            bool yOverlap = a.y < b.y + bSize.y && b.y < a.y + aSize.y;
            bool side = (a.x + aSize.x == b.x || b.x + bSize.x == a.x) && yOverlap;
            bool stacked = (a.y + aSize.y + 1 == b.y || b.y + bSize.y + 1 == a.y) && xOverlap;
            return side || stacked;
        }
    }
}
