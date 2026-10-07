using System;
using System.Collections.Generic;
using Assets.Scripts.IO;

namespace Assets.Scripts.Progression
{
    /// <summary>How a run attempt ended. Serialized by ordinal: append, never insert.</summary>
    public enum RunOutcome
    {
        InProgress = 0,
        Cleared = 1,
        Fell = 2
    }

    /// <summary>
    /// One attempt at a run, from its first floor to a wipe or a clear (docs/plans/POLISH_CONTENT.md
    /// §15). Leaving the dungeon does not end an attempt - the run stands and the floor restarts - so
    /// it is counted as a retreat instead.
    ///
    /// <para>Two kinds of number, and the difference matters. <b>What happened</b> (kills, fights,
    /// rooms, retreats) counts the moment it happens, because a wipe on floor three still happened.
    /// <b>What was kept</b> (XP, gold, Essence, items) only counts once a floor is banked: a floor's
    /// share waits in the <c>Floor*</c> fields and is folded in on its clear or dropped on its
    /// forfeit, the same rule the game's own saves follow (Events guide, "Provisional vs kept").</para>
    /// </summary>
    [Serializable]
    public class RunRecord
    {
        public string RunKey;
        public string RunName;
        public int TotalFloors;
        public bool Revisit;
        public int FearLevel;

        /// <summary>UTC, ISO 8601 (JsonUtility cannot serialize DateTime).</summary>
        public string Started;
        public string Ended;

        public RunOutcome Outcome;

        /// <summary>The floor the attempt is on, or ended on (0-based), and its name.</summary>
        public int FloorIndex;
        public string FloorName;
        public int FloorsCleared;

        public List<string> HeroKeys = new List<string>();

        public int Kills;
        public int BossKills;
        public int FightsWon;
        public int RoomsEntered;
        public int Retreats;

        public int XpKept;
        public int GoldKept;
        public int EssenceKept;
        public int ItemsKept;

        /// <summary>The foes still standing when the party fell. Empty unless the attempt fell.</summary>
        public List<string> FellTo = new List<string>();

        // The current floor's provisional share - folded in on its clear, dropped on its forfeit.
        public int FloorXp;
        public int FloorItems;
    }

    /// <summary>
    /// <c>RunHistory.json</c>: the attempt underway (if any) and the most recent finished ones, newest
    /// first, capped at <see cref="RunHistoryOps.MaxRecords"/>. Kept out of <c>Meta.json</c>, which
    /// already does several jobs.
    /// </summary>
    [Serializable]
    public class RunHistorySaveData : IWriteable
    {
        public bool HasCurrent;
        public RunRecord Current = new RunRecord();
        public List<RunRecord> Records = new List<RunRecord>();

        public string GetFileName()
        {
            return "RunHistory";
        }
    }

    /// <summary>One run's history at a glance, for the story map.</summary>
    public class RunAttemptSummary
    {
        public int Attempts;
        public int Clears;
        public int Falls;

        /// <summary>The deepest floor any attempt reached (1-based), 0 when never attempted.</summary>
        public int DeepestFloor;

        /// <summary>The most recent finished attempt, or null.</summary>
        public RunRecord Last;
    }

    /// <summary>
    /// The pure rules of the run history. <c>RunHistoryRecorder</c> feeds it from the game's events and
    /// saves the result; everything here is plain data in and out so it is tested without Unity.
    /// </summary>
    public static class RunHistoryOps
    {
        public const int MaxRecords = 30;

        /// <summary>
        /// A floor started. Starts a new attempt on a run's first floor, or when the attempt on file is
        /// for another run (an old save, or one the game never saw end). A resume, or the same first
        /// floor rebuilt after a rejected save, continues the attempt on file.
        /// </summary>
        public static void FloorStarted(RunHistorySaveData data, string runKey, string runName, int totalFloors,
            int floorIndex, string floorName, bool fresh, bool revisit, int fearLevel, IEnumerable<string> heroKeys,
            string nowUtc)
        {
            if (data == null || string.IsNullOrEmpty(runKey))
            {
                return;
            }

            bool sameRun = data.HasCurrent && data.Current != null && data.Current.RunKey == runKey;
            bool restartOfFirstFloor = sameRun && floorIndex == 0 && data.Current.FloorsCleared == 0;
            if (!sameRun || (fresh && floorIndex == 0 && !restartOfFirstFloor))
            {
                if (data.HasCurrent && data.Current != null && !string.IsNullOrEmpty(data.Current.RunKey))
                {
                    // An attempt the game never saw end (a crash, an older build): keep what it had.
                    Finish(data, data.Current.Outcome == RunOutcome.InProgress ? RunOutcome.Fell : data.Current.Outcome,
                        null, nowUtc);
                }
                data.Current = new RunRecord
                {
                    RunKey = runKey,
                    RunName = runName,
                    TotalFloors = totalFloors,
                    Revisit = revisit,
                    FearLevel = fearLevel,
                    Started = nowUtc,
                    Outcome = RunOutcome.InProgress
                };
                data.HasCurrent = true;
            }

            var current = data.Current;
            current.FloorIndex = floorIndex;
            current.FloorName = floorName;
            if (fresh)
            {
                current.FloorXp = 0;
                current.FloorItems = 0;
            }
            if (heroKeys != null)
            {
                foreach (var key in heroKeys)
                {
                    if (!string.IsNullOrEmpty(key) && !current.HeroKeys.Contains(key))
                    {
                        current.HeroKeys.Add(key);
                    }
                }
            }
        }

        public static void EnemyDefeated(RunHistorySaveData data, bool boss)
        {
            if (!Active(data))
            {
                return;
            }
            data.Current.Kills++;
            if (boss)
            {
                data.Current.BossKills++;
            }
        }

        public static void FightFinished(RunHistorySaveData data, bool won, IEnumerable<string> heroKeys)
        {
            if (!Active(data))
            {
                return;
            }
            if (won)
            {
                data.Current.FightsWon++;
            }
            if (heroKeys != null)
            {
                foreach (var key in heroKeys)
                {
                    if (!string.IsNullOrEmpty(key) && !data.Current.HeroKeys.Contains(key))
                    {
                        data.Current.HeroKeys.Add(key);
                    }
                }
            }
        }

        public static void RoomEntered(RunHistorySaveData data)
        {
            if (Active(data))
            {
                data.Current.RoomsEntered++;
            }
        }

        /// <summary>XP and items are provisional until the floor is banked.</summary>
        public static void XpEarned(RunHistorySaveData data, int amount)
        {
            if (Active(data) && amount > 0)
            {
                data.Current.FloorXp += amount;
            }
        }

        public static void ItemsFound(RunHistorySaveData data, int quantity)
        {
            if (Active(data) && quantity > 0)
            {
                data.Current.FloorItems += quantity;
            }
        }

        /// <summary>Kept currency only: banking, a level clear, the consolation on a wipe.</summary>
        public static void CurrencyKept(RunHistorySaveData data, bool essence, int delta)
        {
            if (!Active(data) || delta <= 0)
            {
                return;
            }
            if (essence)
            {
                data.Current.EssenceKept += delta;
            }
            else
            {
                data.Current.GoldKept += delta;
            }
        }

        /// <summary>A floor banked: its provisional share becomes kept. Ends the attempt on the last one.</summary>
        public static void FloorCleared(RunHistorySaveData data, bool runCompleted, string nowUtc)
        {
            if (!Active(data))
            {
                return;
            }
            var current = data.Current;
            current.FloorsCleared++;
            current.XpKept += current.FloorXp;
            current.ItemsKept += current.FloorItems;
            current.FloorXp = 0;
            current.FloorItems = 0;
            if (runCompleted)
            {
                Finish(data, RunOutcome.Cleared, null, nowUtc);
            }
        }

        /// <summary>The party left the dungeon: the floor's share is dropped and the run stands.</summary>
        public static void Retreated(RunHistorySaveData data)
        {
            if (!Active(data))
            {
                return;
            }
            data.Current.Retreats++;
            data.Current.FloorXp = 0;
            data.Current.FloorItems = 0;
        }

        /// <summary>A wipe ends the attempt; the floor's share is lost with it.</summary>
        public static void Fell(RunHistorySaveData data, IEnumerable<string> fellTo, string nowUtc)
        {
            if (!Active(data))
            {
                return;
            }
            data.Current.FloorXp = 0;
            data.Current.FloorItems = 0;
            Finish(data, RunOutcome.Fell, fellTo, nowUtc);
        }

        private static void Finish(RunHistorySaveData data, RunOutcome outcome, IEnumerable<string> fellTo, string nowUtc)
        {
            var record = data.Current;
            record.Outcome = outcome;
            record.Ended = nowUtc;
            if (fellTo != null)
            {
                record.FellTo = new List<string>(fellTo);
            }
            data.Records.Insert(0, record);
            if (data.Records.Count > MaxRecords)
            {
                data.Records.RemoveRange(MaxRecords, data.Records.Count - MaxRecords);
            }
            data.Current = new RunRecord();
            data.HasCurrent = false;
        }

        private static bool Active(RunHistorySaveData data)
        {
            return data != null && data.HasCurrent && data.Current != null && !string.IsNullOrEmpty(data.Current.RunKey);
        }

        /// <summary>Every finished attempt at one run, rolled up for the story map.</summary>
        public static RunAttemptSummary Summarize(RunHistorySaveData data, string runKey)
        {
            var summary = new RunAttemptSummary();
            if (data == null || string.IsNullOrEmpty(runKey))
            {
                return summary;
            }
            foreach (var record in data.Records)
            {
                if (record == null || record.RunKey != runKey)
                {
                    continue;
                }
                summary.Attempts++;
                if (record.Outcome == RunOutcome.Cleared)
                {
                    summary.Clears++;
                }
                else if (record.Outcome == RunOutcome.Fell)
                {
                    summary.Falls++;
                }
                summary.DeepestFloor = Math.Max(summary.DeepestFloor, record.FloorIndex + 1);
                if (summary.Last == null)
                {
                    summary.Last = record;
                }
            }
            return summary;
        }

        /// <summary>"Fell on floor 3 of 5 (The Weeping Causeway) to Mirefather and 2 Bog Shamans".</summary>
        public static string DescribeEnd(RunRecord record, bool withFloorName = true)
        {
            if (record == null)
            {
                return string.Empty;
            }
            string floor = $"floor {record.FloorIndex + 1} of {record.TotalFloors}"
                           + (!withFloorName || string.IsNullOrEmpty(record.FloorName) ? string.Empty : $" ({record.FloorName})");
            switch (record.Outcome)
            {
                case RunOutcome.Cleared:
                    return "Cleared every floor";
                case RunOutcome.Fell:
                    string foes = DescribeFoes(record.FellTo);
                    return string.IsNullOrEmpty(foes) ? $"Fell on {floor}" : $"Fell on {floor} to {foes}";
                default:
                    return $"On {floor}";
            }
        }

        /// <summary>Groups repeats: "Mirefather and 2 Bog Shamans".</summary>
        public static string DescribeFoes(IList<string> names)
        {
            if (names == null || names.Count == 0)
            {
                return string.Empty;
            }
            var order = new List<string>();
            var counts = new Dictionary<string, int>();
            foreach (var name in names)
            {
                if (string.IsNullOrEmpty(name))
                {
                    continue;
                }
                if (!counts.ContainsKey(name))
                {
                    counts[name] = 0;
                    order.Add(name);
                }
                counts[name]++;
            }
            var parts = new List<string>();
            foreach (var name in order)
            {
                int n = counts[name];
                parts.Add(n == 1 ? name : $"{n} {Plural(name)}");
            }
            if (parts.Count == 1)
            {
                return parts[0];
            }
            return string.Join(", ", parts.GetRange(0, parts.Count - 1)) + " and " + parts[parts.Count - 1];
        }

        private static string Plural(string name)
        {
            if (name.EndsWith("s") || name.EndsWith("x") || name.EndsWith("sh") || name.EndsWith("ch"))
            {
                return name + "es";
            }
            if (name.EndsWith("y") && name.Length > 1 && "aeiou".IndexOf(name[name.Length - 2]) < 0)
            {
                return name.Substring(0, name.Length - 1) + "ies";
            }
            return name + "s";
        }
    }
}
