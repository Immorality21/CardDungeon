using Assets.Scripts.Rooms;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    public class CameraSafeAreaTests
    {
        // A 16x9 view centred on the origin; the panel is the top-left quarter-width, fifth-height
        // corner, i.e. world x -8..-4, y 2.7..4.5.
        private static readonly Vector2 Half = new Vector2(8f, 4.5f);
        private static readonly Rect Hud = Rect.MinMaxRect(0f, 0.8f, 0.25f, 1f);

        [Test]
        public void Nudge_RoomClearOfThePanel_KeepsTheTarget()
        {
            var room = Rect.MinMaxRect(-2f, -2f, 2f, 2f);

            var result = CameraSafeArea.Nudge(room, Vector2.zero, Half, Hud);

            Assert.AreEqual(Vector2.zero, result);
        }

        [Test]
        public void Nudge_RoomUnderThePanel_TakesTheSmallerShift()
        {
            // Clearing sideways needs 2, clearing downward 0.8.
            var room = Rect.MinMaxRect(-6f, -1f, 0f, 3.5f);

            var result = CameraSafeArea.Nudge(room, Vector2.zero, Half, Hud);

            Assert.AreEqual(0f, result.x, 1e-4f);
            Assert.AreEqual(0.8f, result.y, 1e-4f);
            Assert.IsFalse(room.Overlaps(CameraSafeArea.PanelInWorld(result, Half, Hud)));
        }

        [Test]
        public void Nudge_RoomTooTallToMoveDown_ShiftsSideways()
        {
            var room = Rect.MinMaxRect(-5f, -4f, 1f, 4f);

            var result = CameraSafeArea.Nudge(room, Vector2.zero, Half, Hud);

            Assert.AreEqual(-1f, result.x, 1e-4f);
            Assert.AreEqual(0f, result.y, 1e-4f);
        }

        [Test]
        public void Nudge_RoomFillsTheView_KeepsTheTarget()
        {
            var room = Rect.MinMaxRect(-8f, -4.5f, 8f, 4.5f);

            var result = CameraSafeArea.Nudge(room, Vector2.zero, Half, Hud);

            Assert.AreEqual(Vector2.zero, result);
        }

        [Test]
        public void Nudge_MarginCountsAsPartOfTheRoom()
        {
            // Just clear of the panel's bottom edge, until a margin is asked for.
            var room = Rect.MinMaxRect(-6f, -1f, 0f, 2.6f);

            Assert.AreEqual(Vector2.zero, CameraSafeArea.Nudge(room, Vector2.zero, Half, Hud));
            Assert.AreEqual(0.4f, CameraSafeArea.Nudge(room, Vector2.zero, Half, Hud, 0.5f).y, 1e-4f);
        }

        [Test]
        public void Nudge_NoPanel_KeepsTheTarget()
        {
            var room = Rect.MinMaxRect(-6f, -1f, 0f, 3.5f);

            var result = CameraSafeArea.Nudge(room, new Vector2(3f, 2f), Half, new Rect(0f, 0f, 0f, 0f));

            Assert.AreEqual(new Vector2(3f, 2f), result);
        }

        // A bottom-centre bar: world x -4..4, y -4.5..-3.15.
        private static readonly Rect Bar = Rect.MinMaxRect(0.25f, 0f, 0.75f, 0.15f);

        [Test]
        public void Nudge_RoomUnderTheBottomBar_MovesTheCameraDown()
        {
            // The room's bottom row reaches y -4 - under the bar until the camera drops 0.85.
            var room = Rect.MinMaxRect(-2f, -4f, 2f, 1f);

            var result = CameraSafeArea.Nudge(room, Vector2.zero, Half, new[] { Bar, Hud });

            Assert.AreEqual(0f, result.x, 1e-4f);
            Assert.AreEqual(-0.85f, result.y, 1e-4f);
            Assert.IsFalse(room.Overlaps(CameraSafeArea.PanelInWorld(result, Half, Bar)));
            Assert.IsFalse(room.Overlaps(CameraSafeArea.PanelInWorld(result, Half, Hud)));
        }

        [Test]
        public void Nudge_TwoPanels_ClearsBothWhenItCan()
        {
            // Under the HUD at the top and the bar at the bottom: moving down for the bar would push
            // the room further under the HUD, so the shift has to go sideways.
            var room = Rect.MinMaxRect(-6f, -3.5f, -2f, 3f);

            var result = CameraSafeArea.Nudge(room, Vector2.zero, Half, new[] { Bar, Hud });

            Assert.IsFalse(room.Overlaps(CameraSafeArea.PanelInWorld(result, Half, Bar)));
            Assert.IsFalse(room.Overlaps(CameraSafeArea.PanelInWorld(result, Half, Hud)));
        }

        [Test]
        public void Nudge_CannotClearEveryPanel_KeepsTheMoreImportantOneClear()
        {
            // Full width and nearly full height: the HUD cannot be cleared, but a small drop clears the bar.
            var room = Rect.MinMaxRect(-8f, -3.5f, 8f, 4f);

            var result = CameraSafeArea.Nudge(room, Vector2.zero, Half, new[] { Bar, Hud });

            Assert.AreEqual(0f, result.x, 1e-4f);
            Assert.AreEqual(-0.35f, result.y, 1e-4f);
        }
    }
}
