using System;
using System.Collections.Generic;
using Assets.Scripts.IO;

namespace Assets.Scripts.Progression
{
    [Serializable]
    public class CardUpgradeEntry
    {
        public string CardKey;
        public int Level;
    }

    /// <summary>
    /// Persistent meta-progression that survives runs and party death.
    /// Gold is the flow currency (spent at the merchant); Essence is the
    /// investment currency (spent upgrading cards). Card upgrade levels are
    /// tracked per card key (per card type), not per owned copy.
    /// </summary>
    [Serializable]
    public class MetaProgressSaveData : IWriteable
    {
        public int Gold;
        public int Essence;
        public List<CardUpgradeEntry> CardUpgrades = new List<CardUpgradeEntry>();

        public string GetFileName()
        {
            return "Meta";
        }
    }
}
