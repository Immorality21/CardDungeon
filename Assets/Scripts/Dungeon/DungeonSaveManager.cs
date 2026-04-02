using Assets.Scripts.Cards;
using Assets.Scripts.IO;
using Assets.Scripts.Resources;
using Assets.Scripts.Rooms;
using ImmoralityGaming.Fundamentals;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Assets.Scripts.Dungeon
{
    public class DungeonSaveManager : SingletonBehaviour<DungeonSaveManager>
    {
        private FileHandler _fileHandler;
        private int _seed;
        private string _levelKey;
        private List<Room> _rooms;

        protected override void Awake()
        {
            base.Awake();
            _fileHandler = new FileHandler();
        }

        public void Initialize(int seed, string levelKey, List<Room> rooms)
        {
            _seed = seed;
            _levelKey = levelKey;
            _rooms = rooms;
        }

        public void Save(Room currentRoom)
        {
            var data = new DungeonSaveData
            {
                Seed = _seed,
                LevelKey = _levelKey,
                CurrentRoomIndex = currentRoom.RoomIndex
            };

            foreach (var room in _rooms)
            {
                data.Rooms.Add(new RoomSaveData
                {
                    RoomIndex = room.RoomIndex,
                    IsExplored = room.IsExplored,
                    EnemyCount = room.Enemies.Count(e => e != null && e.IsAlive)
                });
            }

            if (PartyResourceManager.Instance != null)
            {
                data.Resources = PartyResourceManager.Instance.GetSaveData();
            }

            if (DungeonManager.HasInstance && DungeonManager.Instance.DeckState != null)
            {
                data.UsedCards = DungeonManager.Instance.DeckState.GetSaveData();
            }

            _fileHandler.Save(data);
        }

        public DungeonSaveData Load(int seed)
        {
            return _fileHandler.LoadFromFile<DungeonSaveData>($"Dungeon_{seed}");
        }

        public List<DungeonSaveData> LoadAll()
        {
            var results = new List<DungeonSaveData>();
            var files = _fileHandler.FindFiles("Dungeon_");

            foreach (var filePath in files)
            {
                var fileName = Path.GetFileNameWithoutExtension(filePath);
                var data = _fileHandler.LoadFromFile<DungeonSaveData>(fileName);
                if (data.Seed != 0)
                {
                    results.Add(data);
                }
            }

            return results;
        }

        public bool HasSave(int seed)
        {
            var data = Load(seed);
            return data.Seed != 0;
        }

        public void DeleteCurrentSave()
        {
            if (_seed != 0)
            {
                Delete(_seed);
            }
        }

        public void Delete(int seed)
        {
            _fileHandler.Delete(new DungeonSaveData { Seed = seed });
        }
    }
}
