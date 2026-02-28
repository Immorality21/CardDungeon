using Assets.Scripts.Enemies;
using Assets.Scripts.Heroes;
using ImmoralityGaming.Extensions;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Assets.Scripts.Rooms
{
    public class RoomManager : MonoBehaviour
    {
        [SerializeField]
        private GameObject _tilePrefab;

        [SerializeField]
        private GameObject _roomParentPrefab, _doorPrefab;

        [SerializeField]
        private GameObject _partyPrefab;

        [SerializeField]
        private List<HeroSO> _heroDefinitions;

        [SerializeField]
        private RoomActionUI _roomActionUI;

        [SerializeField]
        private Color _wallColor = new Color(0.15f, 0.1f, 0.08f, 1f);

        [SerializeField]
        private bool _randomGenerateOn;

        [SerializeField]
        private int _roomsToGenerate;

        [SerializeField]
        private int _customSeed = 0;

        [SerializeField]
        private List<RoomSO> _roomSOs;

        [SerializeField, Range(0f, 1f), Tooltip("How likely new rooms attach to leaf nodes vs random nodes. Higher = longer branches.")]
        private float _chainBias = 0.6f;

        [SerializeField, Range(0f, 1f), Tooltip("How likely a room continues in the same direction as its parent. Higher = straighter corridors.")]
        private float _momentumBias = 0.5f;

        private Party _party;

        private List<Room> _spawnedRooms = new List<Room>();
        private List<Door> _spawnedDoors = new List<Door>();
        private HashSet<Vector2Int> _occupiedTiles = new HashSet<Vector2Int>();
        private List<(RoomNode, RoomNode)> _placementPairs = new List<(RoomNode, RoomNode)>();

        private void Start()
        {
            if (_randomGenerateOn)
            {
                SpawnDungeon();
            }
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.G))
            {
                SpawnDungeon();   
            }
        }


        [ContextMenu("Spawn Dungeon")]
        private void SpawnDungeon()
        {
            var seed = _customSeed;

            if (seed == 0)
            {
                var random = Random.Range(int.MinValue, int.MaxValue);

                Debug.Log(random);

                seed = random;
            }

            Random.InitState(seed);

            _spawnedRooms.DestroyAndClear(true);
            _spawnedDoors.DestroyAndClear(true);
            _occupiedTiles.Clear();

            EnemyManager.Instance.CleanupEnemies();

            if (_party != null)
            {
                Destroy(_party.gameObject);
            }

            var graph = GenerateGraph(_roomsToGenerate);

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

            // Place walls around rooms (after doors so we can skip door edges)
            var wallGen = new WallGenerator(_wallColor);
            wallGen.PlaceWalls(_spawnedRooms);

            // Spawn party in a random room
            SpawnParty();

            // Spawn enemies in some rooms (not the party's room)
            EnemyManager.Instance.SpawnEnemies(_spawnedRooms, _party.CurrentRoom);
        }

        private List<RoomNode> GenerateGraph(int count)
        {
            List<RoomNode> graph = new List<RoomNode>();

            // First room (start)
            var first = new RoomNode
            {
                roomData = _roomSOs.TakeRandom(),
                position = Vector2Int.zero
            };

            graph.Add(first);

            for (int i = 1; i < count; i++)
            {
                var node = new RoomNode
                {
                    roomData = _roomSOs.TakeRandom(),
                    position = Vector2Int.zero
                };

                RoomNode parent;

                // Chain bias: prefer leaf nodes (1 connection) to create longer branches
                if (Random.Range(0f, 1f) < _chainBias)
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
            if (parent.placedDirection != Vector2Int.zero && Random.Range(0f, 1f) < _momentumBias)
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
                candidate += new Vector2Int(0, parent.Height);
            }
            else if (direction == Vector2Int.down)
            {
                candidate += new Vector2Int(0, -child.Height);
            }

            // Add a random slide offset along the shared edge so rooms aren't always corner-aligned.
            // The offset is clamped so at least 1 tile of overlap remains (required for door placement).
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

        private void CreateDoor(RoomNode a, RoomNode b)
        {
            // Determine which axis the rooms are adjacent on by checking for a shared edge.
            // Rooms may be offset along the shared edge, so we compute the actual overlap.
            Vector2Int doorA = Vector2Int.zero;
            Vector2Int doorB = Vector2Int.zero;
            bool found = false;

            int aRight = a.position.x + a.roomData.Width;
            int bRight = b.position.x + b.roomData.Width;
            int aTop = a.position.y + a.roomData.Height;
            int bTop = b.position.y + b.roomData.Height;

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
            if (!found && aTop == b.position.y)
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
            if (!found && bTop == a.position.y)
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
                return;
            }

            Vector2 doorPos = ((Vector2)doorA + (Vector2)doorB) / 2f;
            var doorObj = Instantiate(_doorPrefab, doorPos, Quaternion.identity, transform);
            var door = doorObj.GetComponent<Door>();

            door.RoomA = a.room;
            door.RoomB = b.room;
            door.PositionInA = (Vector2)doorA;
            door.PositionInB = (Vector2)doorB;

            a.room.Doors.Add(door);
            b.room.Doors.Add(door);
            _spawnedDoors.Add(door);
        }

        private bool CanPlaceRoom(RoomSO room, Vector2Int startPos)
        {
            for (int w = 0; w < room.Width; w++)
            {
                for (int h = 0; h < room.Height; h++)
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

        private Room PlaceRoom(RoomNode roomNode, Vector2Int startPos, Transform parent)
        {
            var roomObj = Instantiate(_roomParentPrefab, parent);

            var roomBehaviour = roomObj.GetComponent<Room>();
            roomNode.room = roomBehaviour;

            roomBehaviour.RoomSO = roomNode.roomData;
            roomBehaviour.GridPosition = startPos;

            _spawnedRooms.Add(roomBehaviour);

            for (int w = 0; w < roomNode.roomData.Width; w++)
            {
                for (int h = 0; h < roomNode.roomData.Height; h++)
                {
                    var tilePos = startPos + new Vector2Int(w, h);
                    var obj = Instantiate(_tilePrefab, new Vector3(tilePos.x, tilePos.y, 0), Quaternion.identity, roomObj.transform);
                    obj.GetComponent<SpriteRenderer>().color = roomNode.roomData.Color;
                    _occupiedTiles.Add(tilePos);
                }
            }

            return roomBehaviour;
        }

        private void SpawnParty()
        {
            if (_partyPrefab == null)
            {
                return;
            }

            var startRoom = _spawnedRooms[Random.Range(0, _spawnedRooms.Count)];
            var partyObj = Instantiate(_partyPrefab, transform);
            _party = partyObj.GetComponent<Party>();
            _party.Initialize(_heroDefinitions);
            _party.PlaceInRoom(startRoom);

            GameManager.Instance.Initialize(_party, _roomActionUI);
            GameManager.Instance.EnterRoom(startRoom);
        }

    }
}