using System.Collections.Generic;
using Assets.Scripts.Dungeon;
using Assets.Scripts.Rooms;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// Guard rail for hand-authored level layouts. A <c>ManualLevelLayoutSO</c> stores door
    /// connections as room-index pairs, but <c>RoomManager.CreateDoor</c> only places a door when the
    /// two rooms actually share an edge - so resizing a room template after a layout was authored can
    /// silently sever a connection and orphan part of the level (worst case: the exit room, making
    /// the level uncompletable; this happened to the tutorial). These tests sweep every layout asset
    /// in the project so that regression fails a test instead of shipping.
    /// </summary>
    public class ManualLayoutValidationTests
    {
        private static List<ManualLevelLayoutSO> LoadAllLayouts()
        {
            var layouts = new List<ManualLevelLayoutSO>();
            foreach (var guid in AssetDatabase.FindAssets("t:ManualLevelLayoutSO"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var layout = AssetDatabase.LoadAssetAtPath<ManualLevelLayoutSO>(path);
                if (layout != null)
                {
                    layouts.Add(layout);
                }
            }
            return layouts;
        }

        [Test]
        public void Project_ContainsAtLeastOneManualLayout()
        {
            Assert.IsNotEmpty(LoadAllLayouts(),
                "No ManualLevelLayoutSO assets found - the sweep tests below would pass vacuously.");
        }

        [Test]
        public void AllLayouts_StartAndExitIndices_AreInRange()
        {
            foreach (var layout in LoadAllLayouts())
            {
                Assert.That(layout.StartRoomIndex, Is.InRange(0, layout.Rooms.Count - 1),
                    $"Layout '{layout.name}': StartRoomIndex is out of range.");
                Assert.That(layout.ExitRoomIndex, Is.InRange(0, layout.Rooms.Count - 1),
                    $"Layout '{layout.name}': ExitRoomIndex is out of range.");
            }
        }

        [Test]
        public void AllLayouts_AuthoredDoors_ArePlaceable()
        {
            foreach (var layout in LoadAllLayouts())
            {
                var broken = layout.GetUnplaceableDoorIndices();
                Assert.IsEmpty(broken,
                    $"Layout '{layout.name}': door(s) {string.Join(", ", broken)} connect rooms that " +
                    "do not share an edge, so they will not exist in-game. Fix the layout in " +
                    "Tools > Dungeon > Manual Level Layout Editor.");
            }
        }

        [Test]
        public void AllLayouts_EveryRoom_IsReachableFromStart()
        {
            foreach (var layout in LoadAllLayouts())
            {
                var orphaned = layout.GetUnreachableRoomIndices();
                Assert.IsEmpty(orphaned,
                    $"Layout '{layout.name}': room(s) {string.Join(", ", orphaned)} cannot be reached " +
                    "from the start room" +
                    (orphaned.Contains(layout.ExitRoomIndex)
                        ? " - including the exit, so the level cannot be completed."
                        : "."));
            }
        }

        // --- Pure adjacency-rule tests (no assets) -------------------------------------------------

        private static ManualRoomEntry MakeRoom(int x, int y, int width, int height)
        {
            var template = ScriptableObject.CreateInstance<RoomSO>();
            template.Width = width;
            template.Height = height;
            return new ManualRoomEntry
            {
                RoomTemplate = template,
                GridPosition = new Vector2Int(x, y)
            };
        }

        [Test]
        public void AreRoomsAdjacent_TouchingEdgesWithOverlap_IsTrue()
        {
            var left = MakeRoom(0, 0, 3, 3);
            var right = MakeRoom(3, 1, 4, 1);
            Assert.IsTrue(ManualLevelLayoutSO.AreRoomsAdjacent(left, right));
            Assert.IsTrue(ManualLevelLayoutSO.AreRoomsAdjacent(right, left));
        }

        [Test]
        public void AreRoomsAdjacent_OneTileGap_IsFalse()
        {
            // The exact shape of the tutorial bug: a 2-wide room at x=4 next to a hallway at x=7.
            var narrow = MakeRoom(4, 0, 2, 3);
            var hallway = MakeRoom(7, 1, 4, 1);
            Assert.IsFalse(ManualLevelLayoutSO.AreRoomsAdjacent(narrow, hallway));
        }

        [Test]
        public void AreRoomsAdjacent_TouchingEdgesWithoutOverlap_IsFalse()
        {
            // Edges on the same line but the rooms only meet at a corner - no wall tile is shared.
            var lower = MakeRoom(0, 0, 3, 3);
            var upper = MakeRoom(3, 3, 3, 3);
            Assert.IsFalse(ManualLevelLayoutSO.AreRoomsAdjacent(lower, upper));
        }
    }
}
