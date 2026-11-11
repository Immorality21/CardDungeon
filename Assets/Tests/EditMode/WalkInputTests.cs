using System.Collections.Generic;
using Assets.Scripts.Rooms;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>The walking keys: held, queued taps (playtest 4 finding 57) and the latch (finding 33).</summary>
    public class WalkInputTests
    {
        private static List<Vector2Int> Drain(WalkInput input)
        {
            var steps = new List<Vector2Int>();
            while (input.QueuedCount > 0 && input.TryNext(out var direction))
            {
                steps.Add(direction);
            }
            return steps;
        }

        /// <summary>A tap: down for one frame, up the next.</summary>
        private static void Tap(WalkInput input, Vector2Int direction, bool stepping)
        {
            input.Read(direction, true, true, stepping);
            input.Read(direction, false, false, stepping);
        }

        [Test]
        public void TryNext_NothingPressed_ReturnsFalse()
        {
            var input = new WalkInput();

            Assert.IsFalse(input.TryNext(out _));
            Assert.IsFalse(input.AnyActive);
        }

        [Test]
        public void Read_HeldKey_IsTheNextStepEveryTime()
        {
            var input = new WalkInput();
            input.Read(Vector2Int.right, true, true, false);

            Assert.IsTrue(input.TryNext(out var first));
            Assert.IsTrue(input.TryNext(out var second));
            Assert.AreEqual(Vector2Int.right, first);
            Assert.AreEqual(Vector2Int.right, second);
        }

        [Test]
        public void Read_TwoHeld_LastPressedWins()
        {
            var input = new WalkInput();
            input.Read(Vector2Int.up, true, true, false);
            input.Read(Vector2Int.left, true, true, false);

            input.TryNext(out var direction);

            Assert.AreEqual(Vector2Int.left, direction);
        }

        [Test]
        public void Read_TapWhileIdle_IsNotQueued()
        {
            // A press while standing starts its own step through the held key; queuing it as well
            // would walk it twice.
            var input = new WalkInput();
            input.Read(Vector2Int.down, true, true, false);

            Assert.AreEqual(0, input.QueuedCount);
        }

        [Test]
        public void Read_TapsDuringAStep_AreKeptInOrder()
        {
            var input = new WalkInput();
            Tap(input, Vector2Int.right, true);
            Tap(input, Vector2Int.up, true);
            Tap(input, Vector2Int.left, true);

            CollectionAssert.AreEqual(new[] { Vector2Int.right, Vector2Int.up, Vector2Int.left }, Drain(input));
            Assert.IsFalse(input.TryNext(out _));
        }

        [Test]
        public void Read_MoreTapsThanTheCap_DropsTheExtra()
        {
            var input = new WalkInput();
            for (int i = 0; i < WalkInput.MaxQueued + 2; i++)
            {
                Tap(input, Vector2Int.right, true);
            }

            Assert.AreEqual(WalkInput.MaxQueued, input.QueuedCount);
        }

        [Test]
        public void TryNext_QueuedTapBeforeHeldKey()
        {
            var input = new WalkInput();
            input.Read(Vector2Int.down, true, true, false);
            Tap(input, Vector2Int.right, true);

            input.TryNext(out var first);
            input.TryNext(out var second);

            Assert.AreEqual(Vector2Int.right, first);
            Assert.AreEqual(Vector2Int.down, second);
        }

        [Test]
        public void Latch_KeyStillDown_IsIgnoredUntilLetGo()
        {
            var input = new WalkInput();
            input.Read(Vector2Int.left, true, true, false);

            input.Latch(Vector2Int.left);
            input.Read(Vector2Int.left, true, false, false);

            Assert.IsFalse(input.TryNext(out _));

            input.Read(Vector2Int.left, false, false, false);
            input.Read(Vector2Int.left, true, true, false);

            Assert.IsTrue(input.TryNext(out var direction));
            Assert.AreEqual(Vector2Int.left, direction);
        }

        [Test]
        public void Latch_DropsTheQueue()
        {
            var input = new WalkInput();
            Tap(input, Vector2Int.up, true);

            input.Latch(Vector2Int.left);

            Assert.AreEqual(0, input.QueuedCount);
        }

        [Test]
        public void ClearQueue_KeepsHeldKeys()
        {
            var input = new WalkInput();
            input.Read(Vector2Int.up, true, true, false);
            Tap(input, Vector2Int.right, true);

            input.ClearQueue();

            Assert.AreEqual(0, input.QueuedCount);
            Assert.IsTrue(input.AnyHeld);
        }
    }
}
