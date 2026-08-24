using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.Dungeon
{
    [CreateAssetMenu(menuName = "SO/Manual Level Layout")]
    public class ManualLevelLayoutSO : ScriptableObject
    {
        public string Key;
        public Color WallColor = new Color(0.15f, 0.1f, 0.08f, 1f);
        public List<ManualRoomEntry> Rooms = new List<ManualRoomEntry>();
        public List<ManualDoorEntry> Doors = new List<ManualDoorEntry>();
        public int StartRoomIndex;
        public int ExitRoomIndex;

        public int GetDeterministicSeed()
        {
            return string.IsNullOrEmpty(Key) ? name.GetHashCode() : Key.GetHashCode();
        }

        /// <summary>
        /// Whether two authored rooms share an edge with at least one overlapping tile - the same
        /// adjacency <c>RoomManager.CreateDoor</c> requires to actually place a door. Authored door
        /// pairs that fail this are silently dropped at build time, so validation has to mirror it
        /// exactly: a room template resized after the layout was authored is enough to break it.
        /// </summary>
        public static bool AreRoomsAdjacent(ManualRoomEntry a, ManualRoomEntry b)
        {
            if (a?.RoomTemplate == null || b?.RoomTemplate == null)
            {
                return false;
            }

            int aRight = a.GridPosition.x + a.RoomTemplate.Width;
            int bRight = b.GridPosition.x + b.RoomTemplate.Width;
            int aTop = a.GridPosition.y + a.RoomTemplate.Height;
            int bTop = b.GridPosition.y + b.RoomTemplate.Height;

            // Shared vertical edge (one room directly left/right of the other).
            if (aRight == b.GridPosition.x || bRight == a.GridPosition.x)
            {
                int overlapMin = Mathf.Max(a.GridPosition.y, b.GridPosition.y);
                int overlapMax = Mathf.Min(aTop, bTop);
                if (overlapMax > overlapMin)
                {
                    return true;
                }
            }

            // Shared horizontal edge (one room directly above/below the other).
            if (aTop == b.GridPosition.y || bTop == a.GridPosition.y)
            {
                int overlapMin = Mathf.Max(a.GridPosition.x, b.GridPosition.x);
                int overlapMax = Mathf.Min(aRight, bRight);
                if (overlapMax > overlapMin)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// True when the authored door's indices are in range and its two rooms are edge-adjacent,
        /// i.e. the door will actually exist when the level is built.
        /// </summary>
        public bool IsDoorPlaceable(ManualDoorEntry door)
        {
            if (door == null ||
                door.RoomIndexA < 0 || door.RoomIndexA >= Rooms.Count ||
                door.RoomIndexB < 0 || door.RoomIndexB >= Rooms.Count ||
                door.RoomIndexA == door.RoomIndexB)
            {
                return false;
            }
            return AreRoomsAdjacent(Rooms[door.RoomIndexA], Rooms[door.RoomIndexB]);
        }

        /// <summary>
        /// Indices into <see cref="Doors"/> of authored doors that cannot be placed. Empty on a
        /// healthy layout.
        /// </summary>
        public List<int> GetUnplaceableDoorIndices()
        {
            var invalid = new List<int>();
            for (int i = 0; i < Doors.Count; i++)
            {
                if (!IsDoorPlaceable(Doors[i]))
                {
                    invalid.Add(i);
                }
            }
            return invalid;
        }

        /// <summary>
        /// Room indices unreachable from <see cref="StartRoomIndex"/> walking only placeable doors.
        /// This is the invariant that actually matters at runtime: an unreachable exit room means
        /// the level can never be completed. Empty on a healthy layout.
        /// </summary>
        public List<int> GetUnreachableRoomIndices()
        {
            var unreachable = new List<int>();
            if (Rooms.Count == 0 || StartRoomIndex < 0 || StartRoomIndex >= Rooms.Count)
            {
                for (int i = 0; i < Rooms.Count; i++)
                {
                    unreachable.Add(i);
                }
                return unreachable;
            }

            var visited = new HashSet<int> { StartRoomIndex };
            var queue = new Queue<int>();
            queue.Enqueue(StartRoomIndex);

            while (queue.Count > 0)
            {
                int current = queue.Dequeue();
                foreach (var door in Doors)
                {
                    if (!IsDoorPlaceable(door))
                    {
                        continue;
                    }

                    int other;
                    if (door.RoomIndexA == current)
                    {
                        other = door.RoomIndexB;
                    }
                    else if (door.RoomIndexB == current)
                    {
                        other = door.RoomIndexA;
                    }
                    else
                    {
                        continue;
                    }

                    if (visited.Add(other))
                    {
                        queue.Enqueue(other);
                    }
                }
            }

            for (int i = 0; i < Rooms.Count; i++)
            {
                if (!visited.Contains(i))
                {
                    unreachable.Add(i);
                }
            }
            return unreachable;
        }
    }
}
