using System.Collections.Generic;
using Assets.Scripts.Dungeon;
using Assets.Scripts.Enemies;
using Assets.Scripts.Heroes;
using Assets.Scripts.Progression;
using Assets.Scripts.Rooms;
using UnityEngine;

namespace Assets.Scripts.Balance
{
    /// <summary>
    /// One level of a run, modelled end to end. The important property of this game's structure is
    /// that <c>Party.HealAll()</c> only fires when a fresh dungeon is entered, so within a level HP is
    /// a consumable resource: the meaningful question is not "can the party beat one room" but "does
    /// the party survive every room in the level on one health bar plus its potion belt".
    /// </summary>
    public class LevelCurve
    {
        public int Index;
        public string Name = "";
        public LevelDefinitionSO Template;
        public ManualLevelLayoutSO Layout;
        public EnemySO Boss;

        public List<RoomEncounter> Rooms = new List<RoomEncounter>();

        public float ExpectedCombatRooms;
        public float ExpectedEnemyCount;

        /// <summary>Party HP the whole level is expected to consume.</summary>
        public float ExpectedHealthCost;

        /// <summary>Fraction of the party's HP + potion pool the level consumes. Above 1 = unclearable.</summary>
        public float AttritionLoad;

        public float AttritionMargin => 1f - AttritionLoad;

        public float PeakRoomDanger;
        public float PeakWorstCaseDanger;
        public float AverageRoomDanger;

        public float BossDanger;
        public float BossToTrashRatio;

        public float ExpectedXp;
        public float ExpectedGold;

        /// <summary>Heroes in the party entering this level. Roster growth makes this vary per level.</summary>
        public int PartySize;

        /// <summary>HP + healing the party brings into this level — the denominator of AttritionLoad.</summary>
        public int SustainPool;

        /// <summary>Hero acquired during this level, if the run entry defines a rescue.</summary>
        public HeroSO RescuedHere;

        public bool IsBossLevel => Boss != null;

        /// <summary>
        /// The single number the run curve is drawn from. Attrition load is the right choice because
        /// it is the metric that actually ends runs, and it composes across rooms.
        /// </summary>
        public float DifficultyScore => AttritionLoad;

        public string LayoutKind => Layout != null ? "Manual" : "Generated";

        /// <summary>
        /// How to find this level in the inspector: <c>Index</c> is its position in
        /// <see cref="RunDefinitionSO.Levels"/>, so findings quote it as <c>Levels[2] Test2</c>. Level
        /// names are frequently duplicated (or empty) across a run, so the name alone cannot identify
        /// which entry a finding is about.
        /// </summary>
        public string Reference => $"Levels[{Index}] {Name}";
    }

    /// <summary>A whole <see cref="RunDefinitionSO"/> modelled level by level, plus the shape of its curve.</summary>
    public class RunCurve
    {
        public RunDefinitionSO Run;
        public string Name = "";
        public List<LevelCurve> Levels = new List<LevelCurve>();

        public float TotalExpectedXp;
        public float TotalExpectedGold;

        /// <summary>Gold and Essence the run banks from level clears (see MetaProgressManager).</summary>
        public int ClearGold;
        public int ClearEssence;

        /// <summary>Level-to-level growth in difficulty score; entry i is the jump from level i to i+1.</summary>
        public List<float> DifficultyJumps = new List<float>();

        public static RunCurve Build(RunDefinitionSO run, PartyBaseline party, BalanceRulesSO rules)
        {
            var curve = new RunCurve { Run = run };
            if (run == null || party == null || rules == null)
            {
                return curve;
            }

            curve.Name = string.IsNullOrEmpty(run.DisplayName)
                ? (string.IsNullOrEmpty(run.Key) ? run.name : run.Key)
                : run.DisplayName;

            if (run.Levels == null)
            {
                return curve;
            }

            // The party is not fixed across a run: a level can hand over a rescued hero, and each
            // hero added roughly halves per-enemy danger while raising the sustain pool. Measuring
            // every level against the starting party would overstate the back half's difficulty.
            var roster = new List<HeroSO>();
            foreach (var hero in party.Heroes)
            {
                if (hero.Definition != null && !roster.Contains(hero.Definition))
                {
                    roster.Add(hero.Definition);
                }
            }

            for (int i = 0; i < run.Levels.Count; i++)
            {
                var entry = run.Levels[i];
                if (entry == null)
                {
                    continue;
                }

                // Level 0 reuses the caller's baseline so gear/save options are honoured; later
                // levels are rebuilt from the grown roster.
                var levelParty = i == 0
                    ? party
                    : PartyBaseline.Build(roster, rules.ReferenceHeroLevel, null,
                        party.PotionItem, party.PotionCount);

                var level = BuildLevel(i, entry, levelParty, rules);
                level.RescuedHere = entry.RescueHero;
                curve.Levels.Add(level);

                // A hero freed *during* a level only helps for part of it, so they count from the
                // next level on — the conservative reading.
                if (entry.RescueHero != null && !roster.Contains(entry.RescueHero))
                {
                    roster.Add(entry.RescueHero);
                }
            }

            foreach (var level in curve.Levels)
            {
                curve.TotalExpectedXp += level.ExpectedXp;
                curve.TotalExpectedGold += level.ExpectedGold;
            }

            curve.ClearGold = MetaProgressManager.GoldPerLevelCleared * curve.Levels.Count;
            curve.ClearEssence = MetaProgressManager.EssencePerLevelCleared * curve.Levels.Count;

            for (int i = 1; i < curve.Levels.Count; i++)
            {
                float previous = curve.Levels[i - 1].DifficultyScore;
                float current = curve.Levels[i].DifficultyScore;
                curve.DifficultyJumps.Add(previous > 0f ? (current - previous) / previous : current > 0f ? 1f : 0f);
            }

            return curve;
        }

        private static LevelCurve BuildLevel(int index, RunLevelEntry entry, PartyBaseline party, BalanceRulesSO rules)
        {
            var level = new LevelCurve
            {
                Index = index,
                Name = string.IsNullOrEmpty(entry.LevelName) ? $"Level {index + 1}" : entry.LevelName,
                Template = entry.LevelTemplate,
                Layout = entry.ManualLayout,
                Boss = entry.BossEnemy
            };

            if (entry.ManualLayout != null)
            {
                BuildManualRooms(level, entry.ManualLayout, party, rules);
            }
            else if (entry.LevelTemplate != null)
            {
                BuildGeneratedRooms(level, entry.LevelTemplate, party, rules);
            }

            // A boss is guaranteed alone in the exit room, which is cleared of its normal spawns
            // first (see EnemyManager.PlaceBossIfConfigured), so it replaces one room's encounter
            // rather than adding to the level's load.
            if (entry.BossEnemy != null)
            {
                ReplaceExitRoomWithBoss(level, entry, party, rules);
            }

            Aggregate(level, party, rules);
            return level;
        }

        private static void BuildManualRooms(LevelCurve level, ManualLevelLayoutSO layout, PartyBaseline party, BalanceRulesSO rules)
        {
            if (layout.Rooms == null)
            {
                return;
            }

            foreach (var room in layout.Rooms)
            {
                if (room == null || room.RoomTemplate == null)
                {
                    continue;
                }

                var encounter = RoomEncounter.Build(
                    room.RoomTemplate,
                    room.EnemySpawnOverride,
                    room.GuaranteeAllSpawns,
                    party,
                    rules);
                encounter.Occurrences = 1f;
                level.Rooms.Add(encounter);
            }
        }

        private static void BuildGeneratedRooms(LevelCurve level, LevelDefinitionSO template, PartyBaseline party, BalanceRulesSO rules)
        {
            if (template.RoomPool == null || template.RoomPool.Count == 0 || template.RoomsToGenerate <= 0)
            {
                return;
            }

            // RoomManager draws each room uniformly from the pool (List.TakeRandom), so every pool
            // entry is expected to appear RoomsToGenerate / poolSize times.
            float perEntry = (float)template.RoomsToGenerate / template.RoomPool.Count;

            foreach (var room in template.RoomPool)
            {
                if (room == null)
                {
                    continue;
                }

                var encounter = RoomEncounter.Build(room, null, false, party, rules);
                encounter.Occurrences = perEntry;
                level.Rooms.Add(encounter);
            }
        }

        private static void ReplaceExitRoomWithBoss(LevelCurve level, RunLevelEntry entry, PartyBaseline party, BalanceRulesSO rules)
        {
            // Take one ordinary combat room back out of the level: the exit room's spawns are wiped
            // before the boss is placed.
            for (int i = level.Rooms.Count - 1; i >= 0; i--)
            {
                var room = level.Rooms[i];
                if (!room.IsCombatRoom)
                {
                    continue;
                }

                if (room.Occurrences <= 1f)
                {
                    level.Rooms.RemoveAt(i);
                }
                else
                {
                    room.Occurrences -= 1f;
                }
                break;
            }

            var bossEncounter = new RoomEncounter
            {
                Room = null,
                RoomName = $"Exit room — {(string.IsNullOrEmpty(entry.BossEnemy.DisplayName) ? entry.BossEnemy.name : entry.BossEnemy.DisplayName)}",
                GuaranteedSpawns = true,
                Occurrences = 1f
            };
            bossEncounter.Expected.Add(entry.BossEnemy, 1f);
            bossEncounter.WorstCase.Add(entry.BossEnemy, 1f);
            bossEncounter.ExpectedDanger = bossEncounter.Expected.DangerIndex(party);
            bossEncounter.WorstCaseDanger = bossEncounter.ExpectedDanger;
            bossEncounter.ExpectedHealthCost = bossEncounter.Expected.ExpectedPartyHealthCost(party);
            bossEncounter.ExpectedXp = bossEncounter.Expected.ExpectedXp;
            bossEncounter.ExpectedGold = bossEncounter.Expected.ExpectedGold;

            level.Rooms.Add(bossEncounter);
            level.BossDanger = bossEncounter.ExpectedDanger;
        }

        private static void Aggregate(LevelCurve level, PartyBaseline party, BalanceRulesSO rules)
        {
            float dangerWeightTotal = 0f;
            float dangerSum = 0f;
            float trashDangerSum = 0f;
            float trashWeight = 0f;

            foreach (var room in level.Rooms)
            {
                if (!room.IsCombatRoom)
                {
                    continue;
                }

                level.ExpectedCombatRooms += room.Occurrences;
                level.ExpectedEnemyCount += room.Occurrences * room.Expected.TotalCount;
                level.ExpectedXp += room.Occurrences * room.ExpectedXp;
                level.ExpectedGold += room.Occurrences * room.ExpectedGold;

                if (!float.IsInfinity(room.ExpectedHealthCost))
                {
                    level.ExpectedHealthCost += room.Occurrences * room.ExpectedHealthCost;
                }

                if (room.ExpectedDanger > level.PeakRoomDanger)
                {
                    level.PeakRoomDanger = room.ExpectedDanger;
                }
                if (room.WorstCaseDanger > level.PeakWorstCaseDanger)
                {
                    level.PeakWorstCaseDanger = room.WorstCaseDanger;
                }

                dangerSum += room.Occurrences * room.ExpectedDanger;
                dangerWeightTotal += room.Occurrences;

                bool isBossRoom = room.Room == null;
                if (!isBossRoom)
                {
                    trashDangerSum += room.Occurrences * room.ExpectedDanger;
                    trashWeight += room.Occurrences;
                }
            }

            level.AverageRoomDanger = dangerWeightTotal > 0f ? dangerSum / dangerWeightTotal : 0f;

            float trashAverage = trashWeight > 0f ? trashDangerSum / trashWeight : 0f;
            level.BossToTrashRatio = level.Boss != null && trashAverage > 0f
                ? level.BossDanger / trashAverage
                : 0f;

            int sustain = party.SustainPool;
            level.PartySize = party.Size;
            level.SustainPool = sustain;
            level.AttritionLoad = sustain > 0 ? level.ExpectedHealthCost / sustain : 0f;
        }
    }
}
