using System;
using System.Collections.Generic;
using Assets.Scripts.Cards;
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
        public List<MagicSlotSaveData> EquippedMagic = new List<MagicSlotSaveData>();

        /// <summary>
        /// Buffs and debuffs room events hung on the party for the rest of the level. Saved so that
        /// quitting to the menu is not a cure for a curse.
        /// </summary>
        public List<Rooms.Events.LevelAffliction> Afflictions = new List<Rooms.Events.LevelAffliction>();

        public string GetFileName()
        {
            return $"Dungeon_{Seed}";
        }
    }
}
