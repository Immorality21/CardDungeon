using System;
using System.Collections.Generic;
using Assets.Scripts.IO;
using Assets.Scripts.Rooms;

namespace Assets.Scripts.Dungeon
{
    [Serializable]
    public class DungeonSaveData : IWriteable
    {
        public int Seed;
        public string LevelKey;
        public int CurrentRoomIndex;
        public List<RoomSaveData> Rooms = new List<RoomSaveData>();

        public string GetFileName()
        {
            return $"Dungeon_{Seed}";
        }
    }
}
