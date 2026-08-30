using System.Collections.Generic;
using UnityEngine;

namespace ImmoralityGaming.Menu
{
    /// <summary>
    /// "Which one is that way?" - the one piece of maths behind every arrow-key cursor in the game:
    /// menu buttons, campaign and sphere-grid nodes, and the doors of a room.
    ///
    /// <para>Coordinates are taken in whatever space the caller works in, and the direction must be
    /// given in that same space. UI Toolkit's y grows <i>downward</i> (so "up" is <c>(0,-1)</c>);
    /// Unity world space grows upward (so "up" is <c>(0,1)</c>). The picker never flips anything on
    /// the caller's behalf - it has no way to know which space it was handed.</para>
    /// </summary>
    public static class DirectionalNav
    {
        /// <summary>
        /// How far off-axis a candidate may sit and still count as lying "that way", as a multiple of
        /// how far along the axis it is. 2 is a ~63 degree cone either side of the arrow, which is wide
        /// enough that a door set a little off north still answers Up, and narrow enough that the door
        /// due east never does.
        /// </summary>
        private const float SpreadTolerance = 2f;

        /// <summary>
        /// How much sideways drift costs compared to distance along the arrow. Above 1 the picker
        /// prefers the candidate that is *straightest* ahead over the one that is merely nearest, which
        /// is what makes repeated Down presses walk a column instead of zig-zagging between two.
        /// </summary>
        private const float AcrossPenalty = 2f;

        /// <summary>Anything this close to dead sideways is not in the direction pressed at all.</summary>
        private const float MinAlong = 0.001f;

        /// <summary>
        /// The index of the point that lies in <paramref name="direction"/> from
        /// <paramref name="from"/>, or -1 when nothing does. Never returns <paramref name="from"/>.
        /// </summary>
        public static int PickInDirection(IReadOnlyList<Vector2> points, int from, Vector2 direction)
        {
            if (points == null || from < 0 || from >= points.Count)
            {
                return -1;
            }
            return PickInDirection(points, points[from], direction, from);
        }

        /// <summary>
        /// The index of the point that lies in <paramref name="direction"/> from an arbitrary
        /// <paramref name="origin"/> - used when the cursor has no point of its own yet, such as the
        /// first arrow press in a room, which measures from where the party is standing.
        /// </summary>
        public static int PickInDirection(IReadOnlyList<Vector2> points, Vector2 origin, Vector2 direction, int exclude = -1)
        {
            if (points == null || points.Count == 0)
            {
                return -1;
            }

            var dir = direction.normalized;
            if (dir.sqrMagnitude < 0.5f)
            {
                return -1;
            }

            int best = -1;
            float bestScore = float.MaxValue;

            for (int i = 0; i < points.Count; i++)
            {
                if (i == exclude)
                {
                    continue;
                }

                var delta = points[i] - origin;
                float along = Vector2.Dot(delta, dir);
                if (along <= MinAlong)
                {
                    continue;
                }

                // Distance from the arrow's axis - the 2D cross product against a unit vector.
                float across = Mathf.Abs(delta.x * dir.y - delta.y * dir.x);
                if (across > along * SpreadTolerance)
                {
                    continue;
                }

                float score = along + across * AcrossPenalty;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = i;
                }
            }

            return best;
        }
    }
}
