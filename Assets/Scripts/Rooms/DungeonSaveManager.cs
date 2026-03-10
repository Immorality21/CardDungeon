using System.Linq;
using System.Collections.Generic;
using Assets.Scripts.IO;
using ImmoralityGaming.Fundamentals;

namespace Assets.Scripts.Rooms
{
    public class DungeonSaveManager : SingletonBehaviour<DungeonSaveManager>
    {
        private FileHandler _fileHandler;
        private int _seed;

        protected override void Awake()
        {
            base.Awake();
            _fileHandler = new FileHandler();
        }

        public void Initialize(int seed)
        {
            _seed = seed;
        }

        public void Save(Room currentRoom)
        {
            var data = new DungeonSaveData
            {
                Seed = _seed,
                CurrentRoomIndex = currentRoom.RoomIndex
            };

            foreach (var room in RoomManager.Instance.SpawnedRooms)
            {
                data.Rooms.Add(new RoomSaveData
                {
                    RoomIndex = room.RoomIndex,
                    IsExplored = room.IsExplored,
                    EnemyCount = room.Enemies.Count(e => e != null && e.IsAlive)
                });
            }

            _fileHandler.Save(data);
        }

        public DungeonSaveData Load()
        {
            return _fileHandler.Load<DungeonSaveData>();
        }

        public bool HasSave()
        {
            var data = Load();
            return data.Seed != 0;
        }

        public void Delete()
        {
            _fileHandler.Delete(new DungeonSaveData());
        }
    }
}
