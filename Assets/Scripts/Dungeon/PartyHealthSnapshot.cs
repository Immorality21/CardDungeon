using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.Dungeon
{
    /// <summary>
    /// The rules for carrying hero health across a mid-level quit and resume.
    ///
    /// <para>Health is the level's scarce resource - it refills only on entering a fresh dungeon, so
    /// every fight and every room event inside a level spends from one bar. Without this the bar was
    /// rebuilt full on resume (<c>Party.Initialize</c> derives a hero at full health), which made
    /// quitting to the menu and resuming a free heal, and quietly undid every event's damage as
    /// well.</para>
    ///
    /// <para>Pure on purpose: it works on save records and numbers rather than live
    /// <c>Hero</c> components, so the clamp and the missing-hero fallback are unit-testable without
    /// a dungeon. The two-line glue that reads and writes the live party stays at the call sites -
    /// <c>DungeonSaveManager.Save</c> and <c>DungeonManager.RestoreSavedState</c>.</para>
    /// </summary>
    public static class PartyHealthSnapshot
    {
        /// <summary>
        /// The health a hero resumes at.
        ///
        /// <para>A hero with no record resumes <b>full</b>. That is not a fallback for missing data,
        /// it is the rule: the only way to be absent from the file is to have joined the party after
        /// it was written - a captive freed later in the level - and <c>Party.AddHero</c> already
        /// says a rescue arrives at full health, having been in none of the fights that wore the
        /// party down.</para>
        ///
        /// <para>A record is clamped into the bar. <paramref name="maxHealth"/> is the hero's
        /// <i>effective</i> maximum, so re-deriving a hero whose bar grew (gear, a node bought at
        /// the hub between sessions) restores the saved value rather than the new ceiling, and one
        /// whose bar shrank cannot resume above it. Zero is preserved: a hero downed in an earlier
        /// room stays down until the next level, exactly as they do without a quit.</para>
        /// </summary>
        public static int HealthFor(IReadOnlyList<HeroHealthSaveData> saved, string heroKey, int maxHealth)
        {
            int max = Mathf.Max(0, maxHealth);
            if (saved == null || string.IsNullOrEmpty(heroKey))
            {
                return max;
            }

            for (int i = 0; i < saved.Count; i++)
            {
                var record = saved[i];
                if (record != null && record.HeroKey == heroKey)
                {
                    return Mathf.Clamp(record.Health, 0, max);
                }
            }

            return max;
        }

        /// <summary>
        /// Builds the record list from key/health pairs. Heroes with no key are skipped - they cannot
        /// be resolved on the way back in, and writing them would only make the file ambiguous.
        /// </summary>
        public static List<HeroHealthSaveData> Capture(IReadOnlyList<KeyValuePair<string, int>> heroes)
        {
            var records = new List<HeroHealthSaveData>();
            if (heroes == null)
            {
                return records;
            }

            foreach (var entry in heroes)
            {
                if (string.IsNullOrEmpty(entry.Key))
                {
                    continue;
                }

                records.Add(new HeroHealthSaveData
                {
                    HeroKey = entry.Key,
                    Health = Mathf.Max(0, entry.Value)
                });
            }

            return records;
        }
    }
}
