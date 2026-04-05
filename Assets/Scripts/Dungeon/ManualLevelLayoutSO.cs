using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.Dungeon
{
    [CreateAssetMenu(menuName = "SO/Manual Level Layout")]
    public class ManualLevelLayoutSO : ScriptableObject
    {
        public string Key;
        public Color WallColor = new Color(0.15f, 0.1f, 0.08f, 1f);
        public List<ManualRoomEntry> Rooms = new List<ManualRoomEntry>();
        public List<ManualDoorEntry> Doors = new List<ManualDoorEntry>();
        public int StartRoomIndex;
        public int ExitRoomIndex;

        public int GetDeterministicSeed()
        {
            return string.IsNullOrEmpty(Key) ? name.GetHashCode() : Key.GetHashCode();
        }
    }
}
