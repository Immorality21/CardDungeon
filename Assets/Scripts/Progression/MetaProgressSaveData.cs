using System;
using System.Collections.Generic;
using Assets.Scripts.Heroes;
using Assets.Scripts.IO;

namespace Assets.Scripts.Progression
{
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

    /// <summary>One run's revisit history: the best fear it has been cleared at, and how often.</summary>
    [Serializable]
    public class RevisitRecord
    {
        public string RunKey;
        public int BestFear;
        public int Clears;
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
    /// investment currency, paid by revisits and spent on Essence-priced sphere-grid
    /// nodes and on combo upgrades at the Forge (docs/plans/HUB.md §3c).
    /// </summary>
    [Serializable]
    public class MetaProgressSaveData : IWriteable
    {
        public int Gold;
        public int Essence;
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

        // Revisits cleared, per run: the best fear and how many times (docs/plans/REVISITS.md).
        public List<RevisitRecord> RevisitRecords = new List<RevisitRecord>();

        // The guided first hour (docs/TUTORIAL.md). Only its two ends are stored - which step the
        // save is on is read off the save itself (TutorialOps.CurrentStep), so it can never drift.
        // A save written before the tutorial existed has neither, and is never dragged into it:
        // only a New Game sets TutorialStarted.
        public bool TutorialStarted;
        public bool TutorialFinished;

        // The story map has warned once that a challenge run is open (docs/TUTORIAL.md, "The
        // challenge warning"). Not derivable from the rest of the save, so it is stored.
        public bool ChallengeWarned;

        // The tutorial's return to the Hall of Progression after the gear step is done (docs/TUTORIAL.md).
        // Visiting is the whole step - nothing has to be bought - so it is stored, not derived.
        public bool TutorialHallRevisited;

        // Potion potency steps bought at the Merchant (PotionBeltSO.PotencyTiers). The belt's size
        // lives with the other carry caps in ResourceMaximums.json.
        public int PotionPotency;

        // The typed potion belt (Items.PotionBeltState): slots bought (0 = the starting count), the
        // type in each slot as chosen at the campfire, the recipes bought, and every non-healing
        // type's capacity and potency. Healing keeps its old homes (above, and ResourceMaximums.json).
        public int BeltSlots;
        public List<string> BeltLoadout = new List<string>();
        public List<string> UnlockedPotionTypes = new List<string>();
        public List<PotionTypeProgress> PotionTypes = new List<PotionTypeProgress>();

        // Party wipes, ever (survives death by definition). The death screen explains dying once,
        // on the first; nothing else reads it yet.
        public int PartyDeaths;

        /// <summary>How many enemies the bestiary had recorded when it was last opened - the hub's
        /// "something new here" badge shows on the Bestiary while more have been met since.</summary>
        public int BestiaryViewedCount;

        /// <summary>How many recipes were known when the Forge was last opened - the Forge's "something
        /// new here" badge shows while more have been found since. -1 until the Forge is first opened, so
        /// a save that never looked badges the recipes the Forge teaches.</summary>
        public int RecipesViewedCount = -1;

        public string GetFileName()
        {
            return "Meta";
        }
    }

    /// <summary>One potion type's bought steps (<c>Items.PotionBeltState</c>).</summary>
    [System.Serializable]
    public class PotionTypeProgress
    {
        public string Key;

        // Potions a slot carries; 0 = never raised, the type's base capacity.
        public int Capacity;

        // Potency steps bought.
        public int Potency;
    }
}
