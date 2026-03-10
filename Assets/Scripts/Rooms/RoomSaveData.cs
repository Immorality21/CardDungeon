using System;

namespace Assets.Scripts.Rooms
{
    [Serializable]
    public class RoomSaveData
    {
        public int RoomIndex;
        public bool IsExplored;
        public int EnemyCount;
    }
}
