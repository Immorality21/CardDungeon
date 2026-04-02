using System;
using Assets.Scripts.IO;

namespace Assets.Scripts.Dungeon
{
    [Serializable]
    public class RunSaveData : IWriteable
    {
        public string RunKey;
        public int CurrentLevelIndex;
        public int ActiveDungeonSeed;

        public string GetFileName()
        {
            return "Run";
        }
    }
}
