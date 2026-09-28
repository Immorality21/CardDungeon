using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.Rooms
{
    /// <summary>
    /// Where something stands in a room. Pure, so the spreading rule is testable without a scene.
    ///
    /// <para>Every occupant - an enemy, the stairs, a cache's chest, a captive - takes a whole floor
    /// tile, and the tile chosen is the free one <b>farthest from everything already there</b>: the
    /// doors, the spots the party stands on when it walks in, and every earlier occupant. That puts
    /// the first thing in a room on the side away from the doors and spreads the rest around it.
    /// Ties go to the tile nearest the room's centre, then to the lowest row and column, so the
    /// choice is a pure function of the room - it draws nothing from the seeded RNG stream the
    /// other content passes share.</para>
    ///
    /// <para>It replaced a random pick that insets one tile from every edge. Most rooms are two to
    /// four tiles across, so that inset left one or two tiles, or none - and "none" fell back to the
    /// centre, which is how four enemies, the stairs and a captive all ended up drawn on one point
    /// (playtest 2026-09-28). A room with more occupants than tiles still places everyone: the rule
    /// degrades to "least crowded", never to "stacked on the centre".</para>
    /// </summary>
    public static class RoomSpotOps
    {
        /// <summary>
        /// The tile for the next occupant. <paramref name="tiles"/> are the room's floor tiles;
        /// <paramref name="avoid"/> is everything to keep clear of (doors, party standing spots,
        /// earlier occupants). Returns <paramref name="center"/> only when the room has no tiles.
        /// </summary>
        public static Vector2 Choose(IReadOnlyList<Vector2> tiles, IReadOnlyList<Vector2> avoid, Vector2 center)
        {
            if (tiles == null || tiles.Count == 0)
            {
                return center;
            }

            Vector2 best = tiles[0];
            float bestClearance = float.NegativeInfinity;
            float bestCentreDistance = float.PositiveInfinity;

            foreach (var tile in tiles)
            {
                float clearance = Clearance(tile, avoid);
                float centreDistance = (tile - center).sqrMagnitude;

                bool better = clearance > bestClearance + 0.0001f
                    || (Mathf.Abs(clearance - bestClearance) <= 0.0001f && IsBetterTie(tile, centreDistance, best, bestCentreDistance));
                if (better)
                {
                    best = tile;
                    bestClearance = clearance;
                    bestCentreDistance = centreDistance;
                }
            }

            return best;
        }

        /// <summary>The floor tiles of a room at <paramref name="gridPosition"/>, one per cell.</summary>
        public static List<Vector2> Tiles(Vector2Int gridPosition, int width, int height)
        {
            var tiles = new List<Vector2>(Mathf.Max(0, width * height));
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    tiles.Add(new Vector2(gridPosition.x + x, gridPosition.y + y));
                }
            }
            return tiles;
        }

        /// <summary>
        /// Where the party stands after walking in through a door: just inside it, stepped toward the
        /// room's centre. Shared by <c>Party.PlaceAtDoor</c> and the spot allocator, so contents keep
        /// clear of exactly the point the party will be drawn at.
        /// </summary>
        public static Vector2 EntrySpot(Vector2 doorPositionInRoom, Vector2 center)
        {
            var inward = center - doorPositionInRoom;
            if (inward.sqrMagnitude < 0.0001f)
            {
                return doorPositionInRoom;
            }
            return doorPositionInRoom + inward.normalized * EntryStep;
        }

        /// <summary>How far inside a door the party stands.</summary>
        public const float EntryStep = 0.75f;

        private static float Clearance(Vector2 tile, IReadOnlyList<Vector2> avoid)
        {
            // Nothing to keep clear of: every tile ties and the centre decides. Not infinity -
            // infinity minus infinity is NaN, and the tie test would never pass.
            if (avoid == null || avoid.Count == 0)
            {
                return 0f;
            }

            float min = float.PositiveInfinity;
            foreach (var point in avoid)
            {
                float d = (tile - point).sqrMagnitude;
                if (d < min)
                {
                    min = d;
                }
            }
            return min;
        }

        private static bool IsBetterTie(Vector2 tile, float centreDistance, Vector2 best, float bestCentreDistance)
        {
            if (centreDistance < bestCentreDistance - 0.0001f)
            {
                return true;
            }
            if (centreDistance > bestCentreDistance + 0.0001f)
            {
                return false;
            }
            if (tile.y != best.y)
            {
                return tile.y < best.y;
            }
            return tile.x < best.x;
        }
    }
}
