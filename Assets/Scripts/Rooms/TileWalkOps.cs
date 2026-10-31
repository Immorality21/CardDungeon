using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Assets.Scripts.Rooms
{
    /// <summary>What one step of the party does.</summary>
    public enum WalkStepKind
    {
        Blocked = 0,
        Move = 1,
        Door = 2,
    }

    /// <summary>The outcome of a step: where it lands, or which door it walks through.</summary>
    public struct WalkStep
    {
        public WalkStepKind Kind;
        public Vector2Int To;

        /// <summary>For <see cref="WalkStepKind.Door"/>, the index into the doorways passed in.</summary>
        public int DoorIndex;
    }

    /// <summary>
    /// Walking the party a tile at a time, FF style (<c>docs/plans/DUNGEON_ROOMS.md</c>). Pure, so the
    /// rule is testable without a scene.
    ///
    /// <para>A step stays on the room's floor. The only way off it is <b>through a door</b>: standing
    /// on the door's tile and stepping the way it leads. Walls are never walkable, the wall face above
    /// a biome floor included - a north door's archway is crossed in that one step, not stood in. Two
    /// rooms side by side have floors that touch, which is exactly why "is the next cell floor" is not
    /// enough: the next cell must be floor <i>of this room</i>.</para>
    ///
    /// <para>Whether a door may be used at all is the caller's: it passes <c>open</c>. That keeps the
    /// one rule the room panel already enforces - doors are live only while it is safe to leave -
    /// in one place. What stands on a floor is solid (<see cref="Solid"/>): a step into it is refused
    /// and the party is left facing it, which is how a chest gets opened.</para>
    /// </summary>
    public static class TileWalkOps
    {
        private static readonly Vector2Int[] Directions =
        {
            Vector2Int.left, Vector2Int.right, Vector2Int.up, Vector2Int.down,
        };

        public static WalkStep Step(
            Vector2Int from,
            Vector2Int direction,
            RectInt floor,
            IReadOnlyList<RoomDoorway> doors,
            IReadOnlyList<bool> open,
            ICollection<Vector2Int> blocked = null)
        {
            if (direction == Vector2Int.zero)
            {
                return new WalkStep { Kind = WalkStepKind.Blocked, To = from, DoorIndex = -1 };
            }

            if (doors != null)
            {
                for (int i = 0; i < doors.Count; i++)
                {
                    bool isOpen = open == null || (i < open.Count && open[i]);
                    if (isOpen && doors[i].Tile == from && doors[i].Direction == direction)
                    {
                        return new WalkStep { Kind = WalkStepKind.Door, To = from + direction, DoorIndex = i };
                    }
                }
            }

            var to = from + direction;
            if (floor.Contains(to) && (blocked == null || !blocked.Contains(to)))
            {
                return new WalkStep { Kind = WalkStepKind.Move, To = to, DoorIndex = -1 };
            }
            return new WalkStep { Kind = WalkStepKind.Blocked, To = from, DoorIndex = -1 };
        }

        /// <summary>
        /// The shortest steps from one tile to another on a floor, around <paramref name="blocked"/>:
        /// across before along when both are as short. Empty on the same tile, null when there is no way.
        /// </summary>
        public static List<Vector2Int> Path(Vector2Int from, Vector2Int to, RectInt floor, ICollection<Vector2Int> blocked = null)
        {
            if (from == to)
            {
                return new List<Vector2Int>();
            }

            var cameFrom = new Dictionary<Vector2Int, Vector2Int> { { from, from } };
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(from);
            while (queue.Count > 0)
            {
                var tile = queue.Dequeue();
                foreach (var direction in Directions)
                {
                    var next = tile + direction;
                    if (cameFrom.ContainsKey(next) || !floor.Contains(next)
                        || (next != to && blocked != null && blocked.Contains(next)))
                    {
                        continue;
                    }
                    cameFrom[next] = tile;
                    if (next == to)
                    {
                        return Trace(cameFrom, from, to);
                    }
                    queue.Enqueue(next);
                }
            }
            return null;
        }

        /// <summary>
        /// The steps to the free tile next to <paramref name="target"/> that is nearest, for walking up
        /// to something solid. Null when no side of it can be reached.
        /// </summary>
        public static List<Vector2Int> PathNextTo(Vector2Int from, Vector2Int target, RectInt floor, ICollection<Vector2Int> blocked)
        {
            List<Vector2Int> best = null;
            foreach (var direction in Directions)
            {
                var side = target + direction;
                if (!floor.Contains(side) || (blocked != null && blocked.Contains(side)))
                {
                    continue;
                }
                var path = Path(from, side, floor, blocked);
                if (path != null && (best == null || path.Count < best.Count))
                {
                    best = path;
                }
            }
            return best;
        }

        /// <summary>
        /// Which of <paramref name="occupied"/> actually block a step: each one in turn, as long as every
        /// tile of <paramref name="mustReach"/> (the doors, where the party stands) can still reach the
        /// others. A room is small and its contents are placed to spread out, not to leave a way past, so
        /// something in a cramped room stays walkable rather than shutting the party in. A tile the party
        /// must reach is never solid itself. Each of <paramref name="mustUse"/> (the stairs, a chest, a
        /// tile on the wall) likewise stays within reach, on it or beside it: walking up to it is the
        /// only way to use it, so stairs boxed in by a chest would end the floor.
        /// </summary>
        public static HashSet<Vector2Int> Solid(
            RectInt floor,
            IEnumerable<Vector2Int> occupied,
            IReadOnlyList<Vector2Int> mustReach,
            IReadOnlyList<Vector2Int> mustUse = null)
        {
            var solid = new HashSet<Vector2Int>();
            foreach (var tile in occupied)
            {
                if (mustReach.Contains(tile) || !floor.Contains(tile))
                {
                    continue;
                }
                solid.Add(tile);
                if (!AllConnected(floor, solid, mustReach) || !AllUsable(floor, solid, mustReach[0], mustUse))
                {
                    solid.Remove(tile);
                }
            }
            return solid;
        }

        private static bool AllConnected(RectInt floor, ICollection<Vector2Int> blocked, IReadOnlyList<Vector2Int> tiles)
        {
            for (int i = 1; i < tiles.Count; i++)
            {
                if (Path(tiles[0], tiles[i], floor, blocked) == null)
                {
                    return false;
                }
            }
            return true;
        }

        private static bool AllUsable(RectInt floor, ICollection<Vector2Int> blocked, Vector2Int from, IReadOnlyList<Vector2Int> tiles)
        {
            if (tiles == null)
            {
                return true;
            }
            foreach (var tile in tiles)
            {
                bool walkable = floor.Contains(tile) && !blocked.Contains(tile);
                var path = walkable ? Path(from, tile, floor, blocked) : PathNextTo(from, tile, floor, blocked);
                if (path == null)
                {
                    return false;
                }
            }
            return true;
        }

        private static List<Vector2Int> Trace(Dictionary<Vector2Int, Vector2Int> cameFrom, Vector2Int from, Vector2Int to)
        {
            var steps = new List<Vector2Int>();
            for (var tile = to; tile != from; tile = cameFrom[tile])
            {
                steps.Add(tile - cameFrom[tile]);
            }
            steps.Reverse();
            return steps;
        }

        /// <summary>
        /// The floor tile nearest <paramref name="point"/>: where to stand someone whose position is
        /// not on a tile (a room's centre is between tiles when its side is even).
        /// </summary>
        public static Vector2Int NearestFloorTile(RectInt floor, Vector2 point)
        {
            int x = Mathf.Clamp(Mathf.FloorToInt(point.x + 0.5f), floor.xMin, floor.xMax - 1);
            int y = Mathf.Clamp(Mathf.FloorToInt(point.y + 0.5f), floor.yMin, floor.yMax - 1);
            return new Vector2Int(x, y);
        }
    }
}
