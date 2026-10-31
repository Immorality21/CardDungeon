using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.Rooms
{
    /// <summary>
    /// Where an authored layout's rooms stand once each has a wall row. Pure, so the rule is testable
    /// without a scene.
    ///
    /// <para>A layout is authored floors-only: a room above another starts on the row the lower floor
    /// ends. With a wall row (<see cref="Room.WallRows"/>) the lower room's face needs that row, so
    /// everything past a north or south door moves one row further out. Rooms joined by a side door
    /// keep their offset, because floors still meet side by side. Assets stay as authored, so the layout
    /// editor needs to know nothing of this.</para>
    /// </summary>
    public static class ManualLayoutOps
    {
        /// <summary>
        /// The rooms' positions with <paramref name="wallRows"/> added, walking the doors (pairs of
        /// room indices) out from <paramref name="start"/>, which stays where it is. A room no door
        /// reaches keeps its authored position. Where doors form a loop the first way in wins, and the
        /// door closing the loop may no longer fit; <c>RoomManager</c> reports that as it does any
        /// unplaceable door.
        /// </summary>
        public static List<Vector2Int> WithWallRows(
            IReadOnlyList<Vector2Int> positions,
            IReadOnlyList<Vector2Int> sizes,
            IReadOnlyList<Vector2Int> doors,
            int start,
            int wallRows)
        {
            var placed = new List<Vector2Int>(positions);
            if (wallRows <= 0 || positions.Count == 0)
            {
                return placed;
            }

            var visited = new bool[positions.Count];
            var queue = new Queue<int>();
            visited[start] = true;
            queue.Enqueue(start);

            while (queue.Count > 0)
            {
                int from = queue.Dequeue();
                foreach (var door in doors)
                {
                    if (door.x != from && door.y != from)
                    {
                        continue;
                    }

                    int to = door.x == from ? door.y : door.x;
                    if (to < 0 || to >= positions.Count || visited[to])
                    {
                        continue;
                    }

                    int rows = positions[to].y - positions[from].y + ExtraRows(
                        positions[from], sizes[from], positions[to], sizes[to], wallRows);
                    placed[to] = new Vector2Int(positions[to].x, placed[from].y + rows);
                    visited[to] = true;
                    queue.Enqueue(to);
                }
            }

            return placed;
        }

        /// <summary>
        /// What a door between these two rooms adds to their row offset: nothing through a side wall,
        /// otherwise a wall row away from <paramref name="from"/>. Side walls are checked first, in
        /// the order <c>RoomManager.CreateDoor</c> checks them.
        /// </summary>
        private static int ExtraRows(Vector2Int from, Vector2Int fromSize, Vector2Int to, Vector2Int toSize, int wallRows)
        {
            bool sideBySide = from.x + fromSize.x == to.x || to.x + toSize.x == from.x;
            bool rowsOverlap = from.y < to.y + toSize.y && to.y < from.y + fromSize.y;
            if (sideBySide && rowsOverlap)
            {
                return 0;
            }

            if (from.y + fromSize.y == to.y)
            {
                return wallRows;
            }

            return to.y + toSize.y == from.y ? -wallRows : 0;
        }
    }
}
