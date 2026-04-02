using Assets.Scripts.Cards;
using Assets.Scripts.Enemies;
using Assets.Scripts.Heroes;
using Assets.Scripts.Resources;
using Assets.Scripts.Rooms;
using ImmoralityGaming.Fundamentals;
using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.Dungeon
{
    public class DungeonManager : SingletonBehaviour<DungeonManager>
    {
        [SerializeField]
        private RoomManager _roomManager;

        [SerializeField]
        private GameObject _partyPrefab;

        [SerializeField]
        private List<HeroSO> _heroDefinitions;

        [SerializeField]
        private RoomActionUI _roomActionUI;

        [SerializeField]
        private bool _randomGenerateOn;

        [SerializeField]
        private int _customSeed = 0;

        [SerializeField]
        private LevelDefinitionSO _testLevel;

        public static int? SeedToLoad;
        public static LevelDefinitionSO LevelToLoad;
        public static RunDefinitionSO ActiveRun;
        public static int RunLevelIndex;
        public Party Party { get; private set; }
        public DungeonDeckState DeckState { get; private set; }

        private LevelDefinitionSO _level;

        /// <summary>
        /// Returns true if all heroes have at least one card assigned, or if the party has no cards at all
        /// (valid attack-only build). Returns false only if some heroes have cards and others don't.
        /// </summary>
        public bool IsDeckConfigured(Party party)
        {
            if (!CardCollectionManager.HasInstance)
            {
                return true;
            }

            var allCards = CardCollectionManager.Instance.GetAllCards();
            if (allCards.Count == 0)
            {
                return true;
            }

            return true;
        }

        private void Start()
        {
            _level = LevelToLoad != null ? LevelToLoad : _testLevel;
            LevelToLoad = null;

            if (SeedToLoad.HasValue)
            {
                var seed = SeedToLoad.Value;
                SeedToLoad = null;
                LoadSavedDungeon(seed);
            }
            else if (_randomGenerateOn)
            {
                SpawnDungeon();
            }
        }

        [ContextMenu("Spawn Dungeon")]
        private void SpawnDungeon()
        {
            SpawnDungeon(null);
        }

        private void SpawnDungeon(DungeonSaveData saveData)
        {
            var seed = _customSeed;

            if (saveData != null)
            {
                seed = saveData.Seed;
            }
            else if (seed == 0)
            {
                seed = System.Guid.NewGuid().GetHashCode();
                Debug.Log(seed);
            }

            Random.InitState(seed);

            EnemyManager.Instance.CleanupEnemies();

            if (Party != null)
            {
                Destroy(Party.gameObject);
            }

            // Step 1: Generate rooms, doors, and walls
            var rooms = _roomManager.GenerateDungeon(_level);

            // Step 2: Assign stable room indices
            for (int i = 0; i < rooms.Count; i++)
            {
                rooms[i].RoomIndex = i;
            }

            // Step 3: Pick starting room (deterministic from seed)
            var startRoom = rooms[Random.Range(0, rooms.Count)];

            // Step 4: Designate exit room (farthest from start via BFS)
            DesignateExitRoom(rooms, startRoom);

            // Step 5: Spawn enemies
            EnemyManager.Instance.SpawnEnemies(rooms, startRoom);

            if (saveData != null)
            {
                RestoreSavedState(saveData, rooms);
            }
            else
            {
                SpawnFreshDungeon(seed, rooms, startRoom);
            }
        }

        private void SpawnFreshDungeon(int seed, List<Room> rooms, Room startRoom)
        {
            // Spawn party in the chosen starting room
            var partyObj = Instantiate(_partyPrefab, transform);
            Party = partyObj.GetComponent<Party>();
            Party.Initialize(_heroDefinitions);
            Party.HealAll();
            Party.PlaceInRoom(startRoom);
            GameManager.Instance.Initialize(Party, _roomActionUI);

            // Hide all rooms (fog of war), then reveal the starting room
            foreach (var room in rooms)
            {
                room.Hide();
            }
            startRoom.Reveal();

            // Initialize card deck state for this dungeon
            if (CardCollectionManager.HasInstance)
            {
                DeckState = new DungeonDeckState();
                DeckState.Initialize(Party.Heroes, CardCollectionManager.Instance);
            }

            // Replenish party resources to their maximums for the new dungeon
            if (PartyResourceManager.Instance != null)
            {
                PartyResourceManager.Instance.ReplenishAll();
            }

            // Initialize save manager and persist initial state
            DungeonSaveManager.Instance.Initialize(seed, _level.Key, rooms);
            DungeonSaveManager.Instance.Save(startRoom);

            GameManager.Instance.EnterRoom(startRoom);
        }

        private void RestoreSavedState(DungeonSaveData saveData, List<Room> rooms)
        {
            // Remove killed enemies based on saved counts
            foreach (var roomData in saveData.Rooms)
            {
                if (roomData.RoomIndex < 0 || roomData.RoomIndex >= rooms.Count)
                {
                    continue;
                }

                var room = rooms[roomData.RoomIndex];

                while (room.Enemies.Count > roomData.EnemyCount)
                {
                    var last = room.Enemies[room.Enemies.Count - 1];
                    room.Enemies.RemoveAt(room.Enemies.Count - 1);
                    if (last != null)
                    {
                        Destroy(last.gameObject);
                    }
                }
            }

            // Spawn party in the saved current room
            var currentRoom = rooms[saveData.CurrentRoomIndex];
            var partyObj = Instantiate(_partyPrefab, transform);
            Party = partyObj.GetComponent<Party>();
            Party.Initialize(_heroDefinitions);
            Party.PlaceInRoom(currentRoom);
            GameManager.Instance.Initialize(Party, _roomActionUI);

            // Hide all rooms, then reveal explored ones
            foreach (var room in rooms)
            {
                room.Hide();
            }

            foreach (var roomData in saveData.Rooms)
            {
                if (roomData.IsExplored && roomData.RoomIndex >= 0 && roomData.RoomIndex < rooms.Count)
                {
                    rooms[roomData.RoomIndex].Reveal();
                }
            }

            // Restore card deck state from save
            if (CardCollectionManager.HasInstance)
            {
                DeckState = new DungeonDeckState();
                DeckState.Initialize(Party.Heroes, CardCollectionManager.Instance);
                DeckState.RestoreUsedCards(saveData.UsedCards);
            }

            // Restore party resource state from save
            if (PartyResourceManager.Instance != null)
            {
                PartyResourceManager.Instance.RestoreFromSave(saveData.Resources);
            }

            DungeonSaveManager.Instance.Initialize(saveData.Seed, _level.Key, rooms);
            GameManager.Instance.EnterRoom(currentRoom);
        }

        private void DesignateExitRoom(List<Room> rooms, Room startRoom)
        {
            var distance = new Dictionary<Room, int>();
            var queue = new Queue<Room>();

            distance[startRoom] = 0;
            queue.Enqueue(startRoom);

            Room farthest = startRoom;
            int maxDist = 0;

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();

                foreach (var door in current.Doors)
                {
                    var neighbor = door.GetOtherRoom(current);
                    if (neighbor != null && !distance.ContainsKey(neighbor))
                    {
                        var dist = distance[current] + 1;
                        distance[neighbor] = dist;
                        queue.Enqueue(neighbor);

                        if (dist > maxDist)
                        {
                            maxDist = dist;
                            farthest = neighbor;
                        }
                    }
                }
            }

            farthest.IsExit = true;
        }

        public void LoadSavedDungeon(int seed)
        {
            var saveData = DungeonSaveManager.Instance.Load(seed);
            if (saveData.Seed != 0)
            {
                SpawnDungeon(saveData);
            }
        }
    }
}
