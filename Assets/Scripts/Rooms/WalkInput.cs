using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.Rooms
{
    /// <summary>
    /// What the walking keys ask for, read once a frame: the keys held (the last one pressed wins, so a
    /// corner can be turned without letting go) and a short queue of taps made while a step was
    /// still being drawn. Pure, so <see cref="PartyWalk"/> only feeds it the keyboard.
    ///
    /// <para><b>The queue</b> (playtest 4 finding 57): a step takes <see cref="PartyWalk.StepSeconds"/>,
    /// and a tap shorter than that used to be gone by the time the step ended, so "down right up left"
    /// typed quickly lost all but the last key. Taps during a step now wait their turn, in order, up to
    /// <see cref="MaxQueued"/> - enough for a quick flurry, not so many that the party keeps walking
    /// long after the player stopped.</para>
    ///
    /// <para><b>The latch</b> (playtest 4 finding 33): a key that has just done something else - the
    /// arrow that fled a fight - stays down a moment longer than the action needed, and read as a walk
    /// it carried the party a step past where Flee put it. <see cref="Latch"/> ignores a key until it
    /// is let go.</para>
    /// </summary>
    public sealed class WalkInput
    {
        /// <summary>Taps kept while a step is drawn; further taps are dropped.</summary>
        public const int MaxQueued = 3;

        private readonly List<Vector2Int> _held = new List<Vector2Int>();
        private readonly List<Vector2Int> _latched = new List<Vector2Int>();
        private readonly Queue<Vector2Int> _queued = new Queue<Vector2Int>();

        /// <summary>Whether any walking key is held (latched keys do not count).</summary>
        public bool AnyHeld => _held.Count > 0;

        /// <summary>Taps waiting for the step in progress to end.</summary>
        public int QueuedCount => _queued.Count;

        /// <summary>Whether the player is asking for a step, held or queued: they have taken over from a click.</summary>
        public bool AnyActive => _held.Count > 0 || _queued.Count > 0;

        /// <summary>
        /// One key's state this frame. <paramref name="pressed"/> is true on the frame it went down;
        /// <paramref name="stepping"/> whether a step is still being drawn, which is when a tap has to
        /// wait rather than start a step of its own.
        /// </summary>
        public void Read(Vector2Int direction, bool down, bool pressed, bool stepping)
        {
            if (direction == Vector2Int.zero)
            {
                return;
            }
            if (!down)
            {
                _held.Remove(direction);
                _latched.Remove(direction);
                return;
            }
            if (_latched.Contains(direction))
            {
                return;
            }
            if (!_held.Contains(direction))
            {
                _held.Add(direction);
            }
            if (pressed && stepping && _queued.Count < MaxQueued)
            {
                _queued.Enqueue(direction);
            }
        }

        /// <summary>
        /// The next step to take: the oldest queued tap, else the key held last. False when nothing
        /// is asked for.
        /// </summary>
        public bool TryNext(out Vector2Int direction)
        {
            if (_queued.Count > 0)
            {
                direction = _queued.Dequeue();
                return true;
            }
            if (_held.Count > 0)
            {
                direction = _held[_held.Count - 1];
                return true;
            }
            direction = Vector2Int.zero;
            return false;
        }

        /// <summary>Ignores <paramref name="direction"/> until its key is let go, and drops the queue.</summary>
        public void Latch(Vector2Int direction)
        {
            if (direction == Vector2Int.zero)
            {
                return;
            }
            _held.Remove(direction);
            if (!_latched.Contains(direction))
            {
                _latched.Add(direction);
            }
            _queued.Clear();
        }

        /// <summary>Drops the queued taps - the walk was stopped (a window, a fight, a room change).</summary>
        public void ClearQueue()
        {
            _queued.Clear();
        }
    }
}
