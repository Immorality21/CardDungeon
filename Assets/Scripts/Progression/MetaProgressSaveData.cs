using System;
using System.Collections.Generic;
using Assets.Scripts.Heroes;
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
    /// One hub lot the player has placed, and how far it is upgraded. Progress lives here rather
    /// than on <c>BuildingSO</c> for the same reason <c>CompletedRunKeys</c> does: the authored
    /// town stays pure content and one town reads differently per save.
    /// </summary>
    [Serializable]
    public class BuildingProgress
    {
        public string Key;
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

        // How the campfire divides a run's XP, and who the Mentor mode favours. A redistribution,
        // never a bonus - see XpSplitMode. Which modes are legal is the campfire's building level,
        // so this is only the player's choice; CampfireOps.EffectiveMode decides what it means.
        public XpSplitMode XpSplitMode = XpSplitMode.Even;
        public string XpFocusHeroKey = "";

        // The merchant's current gear stock (item keys). Persisted so the shop is stable across
        // sessions and can't be free-rerolled by reopening; refilled when empty or on paid restock.
        public List<string> ShopStock = new List<string>();

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

        // Hub lots the player has placed, and their level. A building the town marks
        // PlacedByDefault (the campfire) needs no entry - BuildingOps.LevelOf reads it as level 1
        // with nothing here, so a fresh save writes nothing to own a working hub.
        public List<BuildingProgress> Buildings = new List<BuildingProgress>();

        // Run keys the player has cleared to the end (survives death, like all meta progress).
        // Gates the main menu: a non-repeatable run - the tutorial - cannot be started again.
        public List<string> CompletedRunKeys = new List<string>();

        public string GetFileName()
        {
            return "Meta";
        }
    }
}
