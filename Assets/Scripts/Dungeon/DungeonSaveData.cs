using System;
using System.Collections.Generic;
using Assets.Scripts.IO;
using Assets.Scripts.Resources;
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
        public List<ResourceSaveData> Resources = new List<ResourceSaveData>();

        public string GetFileName()
        {
            return $"Dungeon_{Seed}";
        }
    }
}
