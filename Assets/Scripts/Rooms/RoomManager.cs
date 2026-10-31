using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Dungeon;
using ImmoralityGaming.Extensions;
using UnityEngine;

namespace Assets.Scripts.Rooms
{
    public class RoomManager : MonoBehaviour
    {
        [SerializeField]
        private GameObject _roomParentPrefab, _doorPrefab;

        private LevelDefinitionSO _currentLevel;

        public List<Room> SpawnedRooms { get; private set; } = new List<Room>();

        // Clear of the floor's last room, and well inside the backdrop's 40-unit margin.
        private const int SideRoomGap = 20;

        private List<Room> _sideRooms = new List<Room>();
        private List<Door> _spawnedDoors = new List<Door>();
        private HashSet<Vector2Int> _occupiedTiles = new HashSet<Vector2Int>();
        private List<(RoomNode, RoomNode)> _placementPairs = new List<(RoomNode, RoomNode)>();
        private GameObject _backdrop;

        // Every room has one row of north wall face above its floor; see Room.WallRows.
        private const int WallRowsPerRoom = 1;

        private BiomeSO _biome;

        /// <summary>The biome the current floor was built with; part of what a save must match.</summary>
        public string BiomeKey => _biome != null ? _biome.Key : null;

        /// <summary>
        /// Builds an authored level. <paramref name="level"/> is the run entry's template - a manual
        /// layout replaces the room *graph*, not the level it belongs to, and the template still
        /// supplies the wall texture and everything downstream reads off <c>CurrentLevel</c>.
        /// Optional, so a layout can still be built standalone from the editor window.
        /// <paramref name="biome"/> and <paramref name="decorSeed"/> work as in <see cref="GenerateDungeon"/>.
        /// </summary>
        public List<Room> BuildManualDungeon(ManualLevelLayoutSO layout, LevelDefinitionSO level = null, int decorSeed = 0, BiomeSO biome = null)
        {
            // Set before anything reads it: the wall pass below used to dereference whatever
            // GenerateDungeon had left here, which on the manual path is null.
            _currentLevel = level;
            _biome = ResolveBiome(biome, level);

            SpawnedRooms.DestroyAndClear(true);
            _sideRooms.DestroyAndClear(true);
            _spawnedDoors.DestroyAndClear(true);
            _occupiedTiles.Clear();

            // Build RoomNodes from manual entries. The asset holds floors-only positions.
            var positions = ManualLayoutOps.WithWallRows(
                layout.Rooms.Select(entry => entry.GridPosition).ToList(),
                layout.Rooms.Select(entry => new Vector2Int(entry.RoomTemplate.Width, entry.RoomTemplate.Height)).ToList(),
                layout.Doors.Select(door => new Vector2Int(door.RoomIndexA, door.RoomIndexB)).ToList(),
                layout.StartRoomIndex,
                WallRowsPerRoom);

            var nodes = new List<RoomNode>();
            for (int i = 0; i < layout.Rooms.Count; i++)
            {
                var node = new RoomNode
                {
                    roomData = layout.Rooms[i].RoomTemplate,
                    position = positions[i]
                };
                PlaceRoom(node, positions[i], transform);
                nodes.Add(node);
            }

            // Create doors from manual connections. Unlike the procedural generator, an authored
            // door that cannot be placed is a data bug (typically a room template resized after the
            // layout was authored), and a dropped one can orphan the exit room - so it is an error,
            // not a silent skip.
            foreach (var door in layout.Doors)
            {
                if (door.RoomIndexA >= 0 && door.RoomIndexA < nodes.Count &&
                    door.RoomIndexB >= 0 && door.RoomIndexB < nodes.Count)
                {
                    if (!CreateDoor(nodes[door.RoomIndexA], nodes[door.RoomIndexB]))
                    {
                        Debug.LogError(
                            $"Manual layout '{layout.name}': authored door {door.RoomIndexA} <-> " +
                            $"{door.RoomIndexB} could not be placed - the rooms do not share an " +
                            "edge. Fix the layout (Tools > Level Layout Editor); " +
                            "part of the level may be unreachable.");
                    }
                }
                else
                {
                    Debug.LogError(
                        $"Manual layout '{layout.name}': authored door {door.RoomIndexA} <-> " +
                        $"{door.RoomIndexB} references a room index outside the layout's " +
                        $"{nodes.Count} rooms.");
                }
            }

            new RoomTilePainter(_biome).Paint(SpawnedRooms, decorSeed);
            PlaceBackdrop();

            return SpawnedRooms.ToList();
        }

        /// <summary>
        /// Generates a floor from <paramref name="level"/> on the current <c>UnityEngine.Random</c>
        /// state. <paramref name="biome"/> overrides the level's own (<see cref="LevelDefinitionSO.Biome"/>).
        /// The floor is drawn on Tilemaps and decorated from <paramref name="decorSeed"/>, a stream of its
        /// own, so props never move the content.
        /// </summary>
        public List<Room> GenerateDungeon(LevelDefinitionSO level, int decorSeed = 0, BiomeSO biome = null)
        {
            _currentLevel = level;
            _biome = ResolveBiome(biome, level);

            SpawnedRooms.DestroyAndClear(true);
            _sideRooms.DestroyAndClear(true);
            _spawnedDoors.DestroyAndClear(true);
            _occupiedTiles.Clear();

            var graph = GenerateGraph(level.RoomsToGenerate);

            // Place the first room manually
            var start = graph[0];
            PlaceRoom(start, Vector2Int.zero, transform);
            start.position = Vector2Int.zero;

            // Layout and connect the rest
            _placementPairs.Clear();
            LayoutGraph(start);

            // Only create doors between rooms that were actually placed next to each other
            foreach (var (parent, child) in _placementPairs)
            {
                CreateDoor(parent, child);
            }

            // Paint the rooms (after doors so the walls can open for them)
            new RoomTilePainter(_biome).Paint(SpawnedRooms, decorSeed);
            PlaceBackdrop();

            return SpawnedRooms;
        }

        /// <summary>The override, or the level's own. BiomeAssetTests holds every level to having one.</summary>
        private static BiomeSO ResolveBiome(BiomeSO biome, LevelDefinitionSO level)
        {
            var resolved = biome != null ? biome : level != null ? level.Biome : null;
            if (resolved == null)
            {
                throw new System.InvalidOperationException(
                    $"Level '{(level != null ? level.name : "none")}' has no biome, so its floor cannot be drawn.");
            }
            return resolved;
        }

        /// <summary>Replaces the previous floor's backdrop with one sized to this floor.</summary>
        private void PlaceBackdrop()
        {
            if (_backdrop != null)
            {
                Destroy(_backdrop);
            }
            _backdrop = DungeonBackdrop.Place(SpawnedRooms, transform, _currentLevel);
        }

        private List<RoomNode> GenerateGraph(int count)
        {
            List<RoomNode> graph = new List<RoomNode>();

            // First room (start)
            var first = new RoomNode
            {
                roomData = _currentLevel.RoomPool.TakeRandom(),
                position = Vector2Int.zero
            };

            graph.Add(first);

            for (int i = 1; i < count; i++)
            {
                var node = new RoomNode
                {
                    roomData = _currentLevel.RoomPool.TakeRandom(),
                    position = Vector2Int.zero
                };

                RoomNode parent;

                // Chain bias: prefer leaf nodes (1 connection) to create longer branches
                if (Random.Range(0f, 1f) < _currentLevel.ChainBias)
                {
                    var leaves = graph.FindAll(n => n.connections.Count <= 1);
                    parent = leaves[Random.Range(0, leaves.Count)];
                }
                else
                {
                    parent = graph[Random.Range(0, graph.Count)];
                }

                parent.connections.Add(node);
                node.connections.Add(parent);

                graph.Add(node);
            }

            return graph;
        }

        private void LayoutGraph(RoomNode start)
        {
            List<RoomNode> queue = new List<RoomNode>();
            HashSet<RoomNode> visited = new HashSet<RoomNode>();

            queue.Add(start);
            visited.Add(start);

            List<RoomNode> placed = new List<RoomNode> { start };

            while (queue.Count > 0)
            {
                // Prioritize connector rooms that only have 1 placed neighbor (1 door).
                // This ensures hallways get a second connection before other rooms are processed.
                int bestIndex = 0;
                for (int i = 1; i < queue.Count; i++)
                {
                    if (ShouldPrioritize(queue[i], placed) && !ShouldPrioritize(queue[bestIndex], placed))
                    {
                        bestIndex = i;
                    }
                }

                var current = queue[bestIndex];
                queue.RemoveAt(bestIndex);

                foreach (var child in current.connections)
                {
                    if (visited.Contains(child))
                    {
                        continue;
                    }

                    TryPlaceChild(current, child, placed);

                    visited.Add(child);

                    if (child.room != null)
                    {
                        placed.Add(child);
                        queue.Add(child);
                    }
                }
            }
        }

        private bool ShouldPrioritize(RoomNode node, List<RoomNode> placed)
        {
            if (!node.roomData.IsConnectorRoom)
            {
                return false;
            }

            // Count how many of this connector's neighbors are already placed (i.e. will become doors)
            int placedNeighborCount = 0;
            foreach (var conn in node.connections)
            {
                if (placed.Contains(conn))
                {
                    placedNeighborCount++;
                }
            }

            // Prioritize if it only has 1 placed neighbor — it needs more connections
            return placedNeighborCount <= 1;
        }

        private void TryPlaceChild(RoomNode current, RoomNode child, List<RoomNode> allPlaced)
        {
            // Step 1: Try to place relative to the intended parent (current)
            if (TryPlaceAdjacent(current, child))
            {
                _placementPairs.Add((current, child));
                return;
            }

            // Step 2: If failed, try other already placed nodes that are connected to the child
            foreach (var altParent in child.connections)
            {
                if (allPlaced.Contains(altParent) && TryPlaceAdjacent(altParent, child))
                {
                    _placementPairs.Add((altParent, child));
                    return;
                }
            }

            // Step 3: As a last resort, skip the room (or force-place somewhere else)
            Debug.LogWarning($"Failed to place {child.roomData.name}, skipping...");
        }

        private bool TryPlaceAdjacent(RoomNode parent, RoomNode child)
        {
            var directions = new List<Vector2Int>
            {
                Vector2Int.up,
                Vector2Int.down,
                Vector2Int.left,
                Vector2Int.right
            };

            // Shuffle directions
            for (int i = 0; i < directions.Count; i++)
            {
                var tmp = directions[i];
                int swapIndex = Random.Range(i, directions.Count);
                directions[i] = directions[swapIndex];
                directions[swapIndex] = tmp;
            }

            // Momentum: if the parent was placed in a direction, try that direction first
            if (parent.placedDirection != Vector2Int.zero && Random.Range(0f, 1f) < _currentLevel.MomentumBias)
            {
                directions.Remove(parent.placedDirection);
                directions.Insert(0, parent.placedDirection);
            }

            // Try each direction once
            foreach (var dir in directions)
            {
                var candidate = GetAdjacentPlacement(parent.roomData, child.roomData, parent.position, dir);

                if (CanPlaceRoom(child.roomData, candidate))
                {
                    child.position = candidate;
                    child.placedDirection = dir;
                    PlaceRoom(child, candidate, transform);
                    return true;
                }
            }

            return false; // No valid placement
        }

        private Vector2Int GetAdjacentPlacement(RoomSO parent, RoomSO child, Vector2Int parentPos, Vector2Int direction)
        {
            // Start with child origin same as parent
            Vector2Int candidate = parentPos;

            if (direction == Vector2Int.right)
            {
                candidate += new Vector2Int(parent.Width, 0);
            }
            else if (direction == Vector2Int.left)
            {
                candidate += new Vector2Int(-child.Width, 0);
            }
            else if (direction == Vector2Int.up)
            {
                candidate += new Vector2Int(0, FootprintHeight(parent));
            }
            else if (direction == Vector2Int.down)
            {
                candidate += new Vector2Int(0, -FootprintHeight(child));
            }

            // Add a random slide offset along the shared edge so rooms aren't always corner-aligned.
            // The offset is clamped so at least 1 tile of overlap remains (required for door placement).
            // Side by side it is the *floors* that must overlap, not the wall faces, so this uses Height.
            if (direction == Vector2Int.right || direction == Vector2Int.left)
            {
                int maxSlide = parent.Height + child.Height - 2; // total range preserving 1-tile overlap
                int slide = Random.Range(0, maxSlide + 1) - (child.Height - 1);
                candidate += new Vector2Int(0, slide);
            }
            else
            {
                int maxSlide = parent.Width + child.Width - 2;
                int slide = Random.Range(0, maxSlide + 1) - (child.Width - 1);
                candidate += new Vector2Int(slide, 0);
            }

            return candidate;
        }

        /// <summary>
        /// Places a door between two rooms sharing an edge. Returns false when the rooms are not
        /// adjacent - legitimate during procedural layout (a connection may have been rerouted via an
        /// alternate parent), but a data bug in a manual layout, so callers decide how loud to be.
        /// </summary>
        private bool CreateDoor(RoomNode a, RoomNode b)
        {
            // Determine which axis the rooms are adjacent on by checking for a shared edge.
            // Rooms may be offset along the shared edge, so we compute the actual overlap.
            Vector2Int doorA = Vector2Int.zero;
            Vector2Int doorB = Vector2Int.zero;
            bool found = false;

            int aRight = a.position.x + a.roomData.Width;
            int bRight = b.position.x + b.roomData.Width;
            // Floors meet side by side; footprints (floor + wall face) meet above and below, where the
            // door is an archway through the lower room's face and the party stands on the floor tile
            // either side of it.
            int aTop = a.position.y + a.roomData.Height;
            int bTop = b.position.y + b.roomData.Height;
            int aFootTop = a.position.y + FootprintHeight(a.roomData);
            int bFootTop = b.position.y + FootprintHeight(b.roomData);

            // Check if b is directly to the right of a (shared vertical edge)
            if (!found && aRight == b.position.x)
            {
                int overlapMin = Mathf.Max(a.position.y, b.position.y);
                int overlapMax = Mathf.Min(aTop, bTop);
                if (overlapMax > overlapMin)
                {
                    int y = Random.Range(overlapMin, overlapMax);
                    doorA = new Vector2Int(aRight - 1, y);
                    doorB = new Vector2Int(b.position.x, y);
                    found = true;
                }
            }

            // Check if b is directly to the left of a
            if (!found && bRight == a.position.x)
            {
                int overlapMin = Mathf.Max(a.position.y, b.position.y);
                int overlapMax = Mathf.Min(aTop, bTop);
                if (overlapMax > overlapMin)
                {
                    int y = Random.Range(overlapMin, overlapMax);
                    doorA = new Vector2Int(a.position.x, y);
                    doorB = new Vector2Int(bRight - 1, y);
                    found = true;
                }
            }

            // Check if b is directly above a (shared horizontal edge)
            if (!found && aFootTop == b.position.y)
            {
                int overlapMin = Mathf.Max(a.position.x, b.position.x);
                int overlapMax = Mathf.Min(aRight, bRight);
                if (overlapMax > overlapMin)
                {
                    int x = Random.Range(overlapMin, overlapMax);
                    doorA = new Vector2Int(x, aTop - 1);
                    doorB = new Vector2Int(x, b.position.y);
                    found = true;
                }
            }

            // Check if b is directly below a
            if (!found && bFootTop == a.position.y)
            {
                int overlapMin = Mathf.Max(a.position.x, b.position.x);
                int overlapMax = Mathf.Min(aRight, bRight);
                if (overlapMax > overlapMin)
                {
                    int x = Random.Range(overlapMin, overlapMax);
                    doorA = new Vector2Int(x, a.position.y);
                    doorB = new Vector2Int(x, bTop - 1);
                    found = true;
                }
            }

            if (!found)
            {
                // Connected rooms may not be adjacent if one was placed via an alternate parent during layout.
                return false;
            }

            Vector2 doorPos = ((Vector2)doorA + (Vector2)doorB) / 2f;
            var doorObj = Instantiate(_doorPrefab, doorPos, Quaternion.identity, transform);
            var door = doorObj.GetComponent<Door>();
            // The doorway is drawn in the tilemap - a gap in the thin wall, an archway in the face - so
            // the door object is only a collider, for the click. A side door straddles two tiles; at
            // full width its collider would take the half of each tile nearest the wall, and a click
            // meant for the floor would leave the room.
            var doorCollider = doorObj.GetComponent<BoxCollider2D>();
            if (doorCollider != null && doorA.y == doorB.y)
            {
                doorCollider.size = new Vector2(0.5f, doorCollider.size.y);
            }

            door.RoomA = a.room;
            door.RoomB = b.room;
            door.PositionInA = (Vector2)doorA;
            door.PositionInB = (Vector2)doorB;

            a.room.Doors.Add(door);
            b.room.Doors.Add(door);
            _spawnedDoors.Add(door);
            return true;
        }

        private bool CanPlaceRoom(RoomSO room, Vector2Int startPos)
        {
            for (int w = 0; w < room.Width; w++)
            {
                for (int h = 0; h < FootprintHeight(room); h++)
                {
                    var tile = startPos + new Vector2Int(w, h);
                    if (_occupiedTiles.Contains(tile))
                    {
                        return false; // Overlap detected
                    }
                }
            }
            return true;
        }

        /// <summary>
        /// Builds a room off to the side of the floor, past its last room but still over the backdrop,
        /// with no doors and no place in <see cref="SpawnedRooms"/>: the map, the save and the floor's
        /// counts never see it. Hidden until the party is brought there.
        /// </summary>
        public Room BuildSideRoom(RoomSO template, int decorSeed)
        {
            int x = SideRoomGap;
            foreach (var room in SpawnedRooms)
            {
                x = Mathf.Max(x, room.GridPosition.x + room.RoomSO.Width + SideRoomGap);
            }
            x += _sideRooms.Count * SideRoomGap;
            int y = SpawnedRooms.Count > 0 ? SpawnedRooms[0].GridPosition.y : 0;

            var side = PlaceRoom(new RoomNode { roomData = template }, new Vector2Int(x, y), transform);
            SpawnedRooms.Remove(side);
            _sideRooms.Add(side);
            new RoomTilePainter(_biome).Paint(new List<Room> { side }, decorSeed);
            side.Hide();
            return side;
        }

        private Room PlaceRoom(RoomNode roomNode, Vector2Int startPos, Transform parent)
        {
            var roomObj = Instantiate(_roomParentPrefab, parent);

            var roomBehaviour = roomObj.GetComponent<Room>();
            roomNode.room = roomBehaviour;

            roomBehaviour.RoomSO = roomNode.roomData;
            roomBehaviour.GridPosition = startPos;
            roomBehaviour.WallRows = WallRowsPerRoom;

            SpawnedRooms.Add(roomBehaviour);

            for (int w = 0; w < roomNode.roomData.Width; w++)
            {
                for (int h = 0; h < FootprintHeight(roomNode.roomData); h++)
                {
                    _occupiedTiles.Add(startPos + new Vector2Int(w, h));
                }
            }

            return roomBehaviour;
        }

        /// <summary>The grid rows a room takes on this floor: its floor plus the wall face above it.</summary>
        private int FootprintHeight(RoomSO room)
        {
            return room.Height + WallRowsPerRoom;
        }
    }
}
