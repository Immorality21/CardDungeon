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

    [Serializable]
    public class ComboUpgradeEntry
    {
        public string ComboKey;
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
        public List<ComboUpgradeEntry> ComboUpgrades = new List<ComboUpgradeEntry>();
        public int BonusSlots;

        // The merchant's current gear stock (item keys). Persisted so the shop is stable across
        // sessions and can't be free-rerolled by reopening; refilled when empty or on paid restock.
        public List<string> ShopStock = new List<string>();

        // The tavern's current recruitment offer (hero save keys), persisted for the same reason as
        // ShopStock: reopening the screen must not re-roll it for free.
        public List<string> TavernStock = new List<string>();

        // Permanent discovery record (survives death). A magic is discovered when first drawn;
        // a combo when first triggered in combat. Drives the Forge's collection grid.
        public List<string> DiscoveredMagicKeys = new List<string>();
        public List<string> DiscoveredComboKeys = new List<string>();

        public string GetFileName()
        {
            return "Meta";
        }
    }
}
