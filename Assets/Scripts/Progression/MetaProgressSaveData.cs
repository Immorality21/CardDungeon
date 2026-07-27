using System;
using System.Collections.Generic;
using Assets.Scripts.IO;

namespace Assets.Scripts.Progression
{
    [Serializable]
    public class MagicUpgradeEntry
    {
        public string MagicKey;
        public int Level;
    }

    /// <summary>
    /// Persistent meta-progression that survives runs and party death.
    /// Gold is the flow currency (spent at the merchant); Essence is the
    /// investment currency (spent upgrading magic and buying extra slots).
    /// Magic upgrade levels are tracked per magic key (per magic type).
    /// </summary>
    [Serializable]
    public class MetaProgressSaveData : IWriteable
    {
        public int Gold;
        public int Essence;
        public List<MagicUpgradeEntry> MagicUpgrades = new List<MagicUpgradeEntry>();
        public int BonusSlots;

        public string GetFileName()
        {
            return "Meta";
        }
    }
}
