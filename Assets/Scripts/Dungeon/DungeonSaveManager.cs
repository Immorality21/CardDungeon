using System.Collections.Generic;
using System.IO;
using System.Linq;
using Assets.Scripts.Cards;
using Assets.Scripts.IO;
using Assets.Scripts.Rooms;
using ImmoralityGaming.Fundamentals;

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
                    EnemyCount = room.Enemies.Count(e => e != null && e.IsAlive),
                    EventConsumed = room.EventConsumed,
                    EventKey = room.RoomEvent != null ? room.RoomEvent.SaveKey : null,
                    EventOptionIndex = room.EventOptionIndex,
                    EventOutcomeIndex = room.EventOutcomeIndex,
                    EventSucceeded = room.EventSucceeded,
                    Kind = (int)room.Kind,
                    KindConsumed = room.KindConsumed
                });
            }

            if (DungeonManager.HasInstance && DungeonManager.Instance.MagicState != null)
            {
                data.EquippedMagic = DungeonManager.Instance.MagicState.GetSaveData();
            }

            if (DungeonManager.HasInstance && DungeonManager.Instance.Afflictions != null)
            {
                data.Afflictions = DungeonManager.Instance.Afflictions.GetSaveData();
            }

            // Health is level-scoped (HealAll only fires on a fresh dungeon), so it belongs in the
            // dungeon save alongside the afflictions. Every caller of Save() is a point where it
            // just changed: entering a room, finishing a fight, resolving a room event.
            if (DungeonManager.HasInstance && DungeonManager.Instance.Party != null)
            {
                var live = new List<KeyValuePair<string, int>>();
                foreach (var hero in DungeonManager.Instance.Party.Heroes)
                {
                    if (hero != null && hero.Stats != null)
                    {
                        live.Add(new KeyValuePair<string, int>(hero.HeroKey, hero.Stats.Health));
                    }
                }
                data.HeroHealth = PartyHealthSnapshot.Capture(live);
            }

            // The potion belt is the other half of the level's sustain pool, and it is deferred
            // inventory - so what the level spent has to travel with the dungeon, not the item file.
            if (Items.InventoryManager.HasInstance)
            {
                data.ConsumablesSpent = Items.InventoryManager.Instance.GetDungeonConsumption();
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
