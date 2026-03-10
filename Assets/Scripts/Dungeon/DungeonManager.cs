using Assets.Scripts.Enemies;
using Assets.Scripts.Heroes;
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

        private LevelDefinitionSO _level;
        private Party _party;

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

            if (_party != null)
            {
                Destroy(_party.gameObject);
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

            // Step 4: Spawn enemies
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
            _party = partyObj.GetComponent<Party>();
            _party.Initialize(_heroDefinitions);
            _party.PlaceInRoom(startRoom);
            GameManager.Instance.Initialize(_party, _roomActionUI);

            // Hide all rooms (fog of war), then reveal the starting room
            foreach (var room in rooms)
            {
                room.Hide();
            }
            startRoom.Reveal();

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
            _party = partyObj.GetComponent<Party>();
            _party.Initialize(_heroDefinitions);
            _party.PlaceInRoom(currentRoom);
            GameManager.Instance.Initialize(_party, _roomActionUI);

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

            DungeonSaveManager.Instance.Initialize(saveData.Seed, _level.Key, rooms);
            GameManager.Instance.EnterRoom(currentRoom);
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
