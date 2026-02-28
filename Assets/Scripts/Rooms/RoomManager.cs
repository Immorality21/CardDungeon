using Assets.Scripts.Enemies;
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
        private GameObject _playerPrefab;

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

        private Player _player;

        private List<Room> _spawnedRooms = new List<Room>();
        private List<Door> _spawnedDoors = new List<Door>();
        private HashSet<Vector2Int> _occupiedTiles = new HashSet<Vector2Int>();

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

            if (_player != null)
            {
                Destroy(_player.gameObject);
            }

            var graph = GenerateGraph(_roomsToGenerate);

            // Place the first room manually
            var start = graph[0];
            PlaceRoom(start, Vector2Int.zero, transform);
            start.position = Vector2Int.zero;

            // Layout and connect the rest
            LayoutGraph(start);

            // Add doors
            foreach (var node in graph
                .Where(x => x.room))
            {
                foreach (var conn in node.connections
                    .Where(x => x.room))
                {
                    if (graph.IndexOf(node) < graph.IndexOf(conn)) // avoid duplicates
                    {
                        CreateDoor(node, conn);
                    }
                }
            }

            // Place walls around rooms (after doors so we can skip door edges)
            var wallGen = new WallGenerator(_wallColor);
            wallGen.PlaceWalls(_spawnedRooms);

            // Spawn player in a random room
            SpawnPlayer();

            // Spawn enemies in some rooms (not the player's room)
            EnemyManager.Instance.SpawnEnemies(_spawnedRooms, _player.CurrentRoom);
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

                // Connect to a random existing node (tree-like)
                var parent = graph[Random.Range(0, graph.Count)];
                parent.connections.Add(node);
                node.connections.Add(parent);

                graph.Add(node);
            }

            return graph;
        }

        private void LayoutGraph(RoomNode start)
        {
            Queue<RoomNode> queue = new Queue<RoomNode>();
            HashSet<RoomNode> visited = new HashSet<RoomNode>();

            queue.Enqueue(start);
            visited.Add(start);

            List<RoomNode> placed = new List<RoomNode> { start };

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();

                foreach (var child in current.connections)
                {
                    if (visited.Contains(child))
                    {
                        continue;
                    }

                    TryPlaceChild(current, child, placed);

                    visited.Add(child);
                    placed.Add(child);
                    queue.Enqueue(child);
                }
            }
        }

        private void TryPlaceChild(RoomNode current, RoomNode child, List<RoomNode> allPlaced)
        {
            // Step 1: Try to place relative to the intended parent (current)
            if (TryPlaceAdjacent(current, child))
                return;

            // Step 2: If failed, try other already placed nodes that are connected to the child
            foreach (var altParent in child.connections)
            {
                if (allPlaced.Contains(altParent) && TryPlaceAdjacent(altParent, child))
                {
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

            // Try each direction once
            foreach (var dir in directions)
            {
                var candidate = GetAdjacentPlacement(parent.roomData, child.roomData, parent.position, dir);

                if (CanPlaceRoom(child.roomData, candidate))
                {
                    child.position = candidate;
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
                Debug.LogWarning($"No shared edge found between {a.roomData.name} and {b.roomData.name}, skipping door.");
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

        private void SpawnPlayer()
        {
            if (_playerPrefab == null) return;

            var startRoom = _spawnedRooms[Random.Range(0, _spawnedRooms.Count)];
            var playerObj = Instantiate(_playerPrefab, transform);
            _player = playerObj.GetComponent<Player>();
            _player.PlaceInRoom(startRoom);

            GameManager.Instance.Initialize(_player, _roomActionUI);
            GameManager.Instance.EnterRoom(startRoom);
        }

    }
}