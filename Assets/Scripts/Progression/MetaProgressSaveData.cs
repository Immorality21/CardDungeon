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
        // Legacy: Essence-bought global magic slots, retired when the sphere grid took slot growth
        // over (MagicSlot nodes, per hero). Kept only so old saves deserialize; MetaProgressManager
        // refunds the Essence on load and zeroes it. Never read anywhere else.
        public int BonusSlots;

        // Party slots bought on top of PartySlots.BaseCap - how many heroes can be fielded at once.
        // A Gold sink rather than an Essence one: it buys roster width, which is the same axis the
        // tavern sells into, and Essence is reserved for magic.
        public int BonusPartySlots;

        // The merchant's current gear stock (item keys). Persisted so the shop is stable across
        // sessions and can't be free-rerolled by reopening; refilled when empty or on paid restock.
        public List<string> ShopStock = new List<string>();

        // The tavern's current recruitment offer (hero save keys), persisted for the same reason as
        // ShopStock: reopening the screen must not re-roll it for free.
        public List<string> TavernStock = new List<string>();

        // Permanent discovery record (survives death). A magic is discovered when some hero first
        // learns it on their sphere grid; a combo when first triggered in combat. Drives the Forge's
        // collection grid. (Pre-2026-09-04 a magic was discovered by drawing it from an enemy;
        // what an enemy has been *seen to cast* is BestiaryEntry.ObservedSpellKeys now.)
        public List<string> DiscoveredMagicKeys = new List<string>();
        public List<string> DiscoveredComboKeys = new List<string>();

        // What the player has learned about each enemy type, keyed by EnemySO.SaveKey: kills,
        // which elements have been tried on it, whether it has been seen to attack, which spells it
        // has been seen to cast, and which loot it has actually dropped. Written from the damage path as things are observed, read by the
        // in-combat Inspect window and the hub bestiary. See BestiaryEntry for why the resistance
        // *values* are deliberately not stored here.
        public List<BestiaryEntry> Bestiary = new List<BestiaryEntry>();

        // Run keys the player has cleared to the end (survives death, like all meta progress).
        // Gates the main menu: a non-repeatable run - the tutorial - cannot be started again.
        public List<string> CompletedRunKeys = new List<string>();

        public string GetFileName()
        {
            return "Meta";
        }
    }
}
