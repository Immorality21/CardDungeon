using System.Collections.Generic;
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
    /// in one place.</para>
    /// </summary>
    public static class TileWalkOps
    {
        public static WalkStep Step(
            Vector2Int from,
            Vector2Int direction,
            RectInt floor,
            IReadOnlyList<RoomDoorway> doors,
            IReadOnlyList<bool> open)
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
            if (floor.Contains(to))
            {
                return new WalkStep { Kind = WalkStepKind.Move, To = to, DoorIndex = -1 };
            }
            return new WalkStep { Kind = WalkStepKind.Blocked, To = from, DoorIndex = -1 };
        }

        /// <summary>
        /// The steps from one tile to another on the same floor: across, then along. Nothing on a
        /// floor blocks a step yet, so the straight way is always a way; when something does, this is
        /// where the search goes.
        /// </summary>
        public static List<Vector2Int> Path(Vector2Int from, Vector2Int to)
        {
            var steps = new List<Vector2Int>();
            var across = to.x > from.x ? Vector2Int.right : Vector2Int.left;
            for (int i = Mathf.Abs(to.x - from.x); i > 0; i--)
            {
                steps.Add(across);
            }
            var along = to.y > from.y ? Vector2Int.up : Vector2Int.down;
            for (int i = Mathf.Abs(to.y - from.y); i > 0; i--)
            {
                steps.Add(along);
            }
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
