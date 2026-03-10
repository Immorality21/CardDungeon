using System;
using System.Collections.Generic;
using Assets.Scripts.IO;

namespace Assets.Scripts.Rooms
{
    [Serializable]
    public class DungeonSaveData : IWriteable
    {
        public int Seed;
        public int CurrentRoomIndex;
        public List<RoomSaveData> Rooms = new List<RoomSaveData>();

        public string GetFileName()
        {
            return "Dungeon";
        }
    }
}
