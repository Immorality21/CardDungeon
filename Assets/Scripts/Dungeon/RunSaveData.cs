using System;
using System.Collections.Generic;
using Assets.Scripts.Cards;
using Assets.Scripts.IO;

namespace Assets.Scripts.Dungeon
{
    [Serializable]
    public class RunSaveData : IWriteable
    {
        public string RunKey;
        public int CurrentLevelIndex;
        public int ActiveDungeonSeed;

        // Equipped magic carried across levels of the run (lost on party death when this file is wiped).
        public List<MagicSlotSaveData> EquippedMagic = new List<MagicSlotSaveData>();

        public string GetFileName()
        {
            return "Run";
        }
    }
}
