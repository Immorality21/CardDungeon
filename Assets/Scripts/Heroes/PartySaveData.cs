using System;
using System.Collections.Generic;
using Assets.Scripts.IO;

namespace Assets.Scripts.Heroes
{
    [Serializable]
    public class PartySaveData : IWriteable
    {
        public List<HeroSaveData> Heroes = new List<HeroSaveData>();

        public string GetFileName()
        {
            return "Party";
        }
    }
}
