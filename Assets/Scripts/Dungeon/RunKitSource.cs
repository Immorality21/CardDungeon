using System.Collections.Generic;
using Assets.Scripts.Cards;
using Assets.Scripts.IO;

namespace Assets.Scripts.Dungeon
{
    /// <summary>
    /// Where the hub reads a run's equipped slots from, so the Storehouse and the campfire show what
    /// the run actually holds instead of the hub loadout (playtest 2 findings 1 and 2).
    ///
    /// <para>The truth lives in one of two files. While a floor is paused (<c>ActiveDungeonSeed</c>
    /// set) it is that floor's save, which is newer than <c>Run.json</c> and on the run's opening floor
    /// the only copy. Between floors it is <c>Run.json</c>. Before the first floor, or with no run,
    /// there is nothing, and the hub shows the plain loadout.</para>
    /// </summary>
    public static class RunKitSource
    {
        /// <summary>Every hero's saved slots for the run underway, or null when none is.</summary>
        public static List<MagicSlotSaveData> Load(FileHandler files)
        {
            if (files == null)
            {
                return null;
            }
            var run = files.Load<RunSaveData>();
            if (!CampaignOps.LocksParty(run))
            {
                return null;
            }
            if (run.ActiveDungeonSeed != 0)
            {
                var floor = files.LoadFromFile<DungeonSaveData>($"Dungeon_{run.ActiveDungeonSeed}");
                if (floor != null && floor.Seed != 0)
                {
                    return floor.EquippedMagic ?? new List<MagicSlotSaveData>();
                }
            }
            return run.EquippedMagic ?? new List<MagicSlotSaveData>();
        }

        /// <summary>One hero's entry in <paramref name="entries"/>, or null.</summary>
        public static MagicSlotSaveData EntryFor(List<MagicSlotSaveData> entries, string heroKey)
        {
            if (entries == null || string.IsNullOrEmpty(heroKey))
            {
                return null;
            }
            foreach (var entry in entries)
            {
                if (entry != null && entry.HeroKey == heroKey)
                {
                    return entry;
                }
            }
            return null;
        }
    }
}
