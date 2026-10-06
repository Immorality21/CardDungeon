using System.Collections.Generic;
using Assets.Scripts.Cards;
using Assets.Scripts.IO;

namespace Assets.Scripts.Dungeon
{
    /// <summary>
    /// Carries an ability's awakening (<c>SphereNodeKind.MagicAwaken</c>, docs/plans/HUB.md §3c) into
    /// the saves that name abilities by key, so the awakened version takes the base's place
    /// everywhere at once: the hub loadout (<c>MagicLoadout.json</c>) and, when a run is underway,
    /// the run's slots (<c>Run.json</c> and the paused floor's <c>Dungeon_*.json</c>).
    ///
    /// <para>Rewriting the keys rather than teaching every reader an alias: the known list already
    /// names the awakened key (<c>SphereGridOps.KnownMagicForNodes</c>), so a stored choice or slot
    /// still naming the base would be dropped as "no longer known" - or worse, carried beside the
    /// awakened one. Charges are left as they are: the slot is the same slot.</para>
    /// </summary>
    public static class AwakeningSaves
    {
        public static void Apply(FileHandler files, string heroKey, string baseKey, string awakenedKey)
        {
            if (files == null || string.IsNullOrEmpty(heroKey)
                || string.IsNullOrEmpty(baseKey) || string.IsNullOrEmpty(awakenedKey))
            {
                return;
            }

            var loadout = files.Load<MagicLoadoutSaveData>();
            foreach (var entry in loadout.Heroes)
            {
                if (entry != null && entry.HeroKey == heroKey && Replace(entry.EquippedKeys, baseKey, awakenedKey))
                {
                    files.Save(loadout);
                    break;
                }
            }

            var run = files.Load<RunSaveData>();
            if (ReplaceInSlots(run.EquippedMagic, heroKey, baseKey, awakenedKey))
            {
                files.Save(run);
            }

            if (run.ActiveDungeonSeed != 0)
            {
                var floor = files.LoadFromFile<DungeonSaveData>($"Dungeon_{run.ActiveDungeonSeed}");
                if (floor != null && floor.Seed != 0
                    && ReplaceInSlots(floor.EquippedMagic, heroKey, baseKey, awakenedKey))
                {
                    files.Save(floor);
                }
            }
        }

        /// <summary>Renames <paramref name="baseKey"/> in one hero's slots. True when anything changed.</summary>
        public static bool ReplaceInSlots(
            List<MagicSlotSaveData> entries, string heroKey, string baseKey, string awakenedKey)
        {
            bool changed = false;
            if (entries == null)
            {
                return false;
            }
            foreach (var entry in entries)
            {
                if (entry == null || entry.HeroKey != heroKey || entry.Slots == null)
                {
                    continue;
                }
                foreach (var slot in entry.Slots)
                {
                    if (slot != null && slot.MagicKey == baseKey)
                    {
                        slot.MagicKey = awakenedKey;
                        changed = true;
                    }
                }
            }
            return changed;
        }

        /// <summary>Renames <paramref name="baseKey"/> in a key list. True when anything changed.</summary>
        public static bool Replace(List<string> keys, string baseKey, string awakenedKey)
        {
            if (keys == null)
            {
                return false;
            }
            bool changed = false;
            for (int i = 0; i < keys.Count; i++)
            {
                if (keys[i] == baseKey)
                {
                    keys[i] = awakenedKey;
                    changed = true;
                }
            }
            return changed;
        }
    }
}
