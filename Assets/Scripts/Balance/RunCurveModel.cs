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

        /// <summary>The run entry this level was built from, so consumers can re-read its spawn sources.</summary>
        public RunLevelEntry Entry;

        /// <summary>
        /// What this level does to its enemies. An EnemySO is a template shared across the campaign,
        /// so this is where the numbers a fight here actually uses come from.
        /// </summary>
        public LevelEnemyTuning Tuning;

        public List<RoomEncounter> Rooms = new List<RoomEncounter>();

        /// <summary>
        /// Every room event this level's rooms can offer, with how often it turns up and what
        /// engaging with it costs. See <see cref="RoomEventModel"/> — events are gambles that spend
        /// HP, potions and the occasional woken enemy, so they belong in the attrition curve.
        /// </summary>
        public List<RoomEventEncounter> Events = new List<RoomEventEncounter>();

        public float ExpectedCombatRooms;
        public float ExpectedEnemyCount;

        /// <summary>Expected rooms in this level that offer an Action, i.e. hold a placed event.</summary>
        public float ExpectedEventRooms;

        /// <summary>Party HP the level's fights are expected to consume.</summary>
        public float ExpectedCombatHealthCost;

        /// <summary>
        /// Sustain the level's events are expected to consume: their damage, the healing lost with a
        /// spent consumable, and the fights they wake. Scaled by
        /// <c>BalanceRulesSO.EventEngagementRate</c>.
        /// </summary>
        public float ExpectedEventHealthCost;

        /// <summary>Gold the level's events are expected to pay, on top of what its enemies drop.</summary>
        public float ExpectedEventGold;

        /// <summary>Level afflictions the events are expected to hang on the party. Counted, not priced.</summary>
        public float ExpectedAfflictions;

        /// <summary>Share of the level's expected cost that comes from events rather than fights.</summary>
        public float EventAttritionShare =>
            ExpectedHealthCost > 0f ? ExpectedEventHealthCost / ExpectedHealthCost : 0f;

        /// <summary>Party HP the whole level is expected to consume — fights plus events.</summary>
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

        /// <summary>Per-hero XP budget the party entering this level was modelled at — the expected
        /// income of every earlier floor, greedy-spent on each hero's sphere grid.</summary>
        public int XpBudget;

        /// <summary>Heroes in the party entering this level. Roster growth makes this vary per level.</summary>
        public int PartySize;

        /// <summary>HP + healing the party brings into this level, plus what its refuges give back — the denominator of AttritionLoad.</summary>
        public int SustainPool;

        /// <summary>Rooms of this level promoted to a one-shot cache, and to a refuge.</summary>
        public int TreasureRooms;

        public int RestRooms;

        /// <summary>Health the level's refuges are expected to return, already inside <see cref="SustainPool"/>.</summary>
        public int RestHealing;

        /// <summary>
        /// The party this level was measured against. Held so the simulator can fight this level's
        /// rooms with the roster the player actually has here, rather than the starting party.
        /// </summary>
        public PartyBaseline Party;

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

        /// <summary>
        /// The roster and banked lifetime XP the party walks out of this run with. A campaign is a
        /// graph of runs, so the run after this one is played by *this* party - not by a fresh save.
        /// <c>BalanceAnalyzer</c> feeds these forward along the prerequisite edges.
        /// </summary>
        public List<HeroSO> EndRoster = new List<HeroSO>();

        public Dictionary<HeroSO, int> EndLifetimeXp = new Dictionary<HeroSO, int>();

        public static RunCurve Build(RunDefinitionSO run, PartyBaseline party, BalanceRulesSO rules)
        {
            return Build(run, party, rules, null, null);
        }

        /// <summary>
        /// Models a run. <paramref name="seedRoster"/> and <paramref name="seedLifetimeXp"/> carry a
        /// prior run's end state in, which is what makes a campaign's later runs measurable: without
        /// them every run is judged against a fresh solo starting party, so a run gated behind three
        /// others reads as brutally overtuned when in play it is fought by a grown party.
        /// Pass null for both to measure the run as if it were played first.
        /// </summary>
        public static RunCurve Build(RunDefinitionSO run, PartyBaseline party, BalanceRulesSO rules,
            IReadOnlyList<HeroSO> seedRoster, IReadOnlyDictionary<HeroSO, int> seedLifetimeXp)
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
            if (seedRoster != null)
            {
                foreach (var hero in seedRoster)
                {
                    // The widest legal party is the ceiling here for the same reason it is inside a
                    // run: a hero past the cap gets benched, not fielded.
                    if (hero != null && !roster.Contains(hero) && roster.Count < PartySlots.MaxCap)
                    {
                        roster.Add(hero);
                    }
                }
            }

            // The XP loop, closed: each floor's expected income is banked per hero and greedy-spent
            // on their grids before the next floor is measured, so the back half of a run is judged
            // against a party that grew on the way there — hero power finally moves within a run,
            // not just roster width.
            var lifetime = new Dictionary<HeroSO, float>();
            foreach (var hero in roster)
            {
                lifetime[hero] = rules.ReferenceHeroXp;
                if (seedLifetimeXp != null && seedLifetimeXp.TryGetValue(hero, out int carried))
                {
                    lifetime[hero] = Mathf.Max(carried, rules.ReferenceHeroXp);
                }
            }
            bool carriedIn = seedRoster != null || seedLifetimeXp != null;

            for (int i = 0; i < run.Levels.Count; i++)
            {
                var entry = run.Levels[i];
                if (entry == null)
                {
                    continue;
                }

                // Level 0 reuses the caller's baseline so gear/save options are honoured; later
                // levels are rebuilt from the grown roster at its accumulated XP.
                // Carried-in state beats the caller's baseline: that grown party is the truth here.
                var levelParty = i == 0 && !carriedIn
                    ? party
                    : PartyBaseline.Build(roster,
                        h => Mathf.FloorToInt(lifetime.TryGetValue(h, out var xp) ? xp : 0f),
                        null, party.PotionItem, party.PotionCount, null);

                var level = BuildLevel(i, entry, levelParty, rules);
                level.RescuedHere = entry.RescueHero;
                // Mirror levelParty exactly: the only level measured at the bare ReferenceHeroXp is
                // level 0 of a run nothing feeds into. A run reached along a campaign edge is fought
                // by the party its prerequisite left behind, from its first floor on - reporting 0
                // there understated every gated run's party by a whole campaign's worth of XP.
                level.XpBudget = i == 0 && !carriedIn
                    ? rules.ReferenceHeroXp
                    : Mathf.FloorToInt(RosterAverage(lifetime, roster));
                curve.Levels.Add(level);

                // Pay this floor's expected XP forward: every fielded hero banks an even share
                // (XpSplit's model form), so floor i+1's party is the one that spent floors 0..i's
                // income.
                float share = XpSplit.ExpectedShare(level.ExpectedXp, Mathf.Max(1, roster.Count));
                foreach (var hero in roster)
                {
                    lifetime[hero] += share;
                }

                // A hero freed *during* a level only helps for part of it, so they count from the
                // next level on — the conservative reading.
                //
                // Capped at PartySlots.MaxCap because that is the widest party the game can field:
                // acquiring a fifth hero does not make level 5 easier, it benches somebody. This
                // models the *widest* legal party, so a player who fields fewer sees a harder run
                // than the curve reports — see the min/max band follow-up in docs/NEXT_STEPS.md.
                if (entry.RescueHero != null && !roster.Contains(entry.RescueHero) &&
                    roster.Count < PartySlots.MaxCap)
                {
                    roster.Add(entry.RescueHero);
                    // Recruits join with the same starter bank the game grants — one rule, one
                    // function (SphereGridOps.StarterBank over the roster's lifetimes).
                    var lifetimes = new List<int>();
                    foreach (var pair in lifetime)
                    {
                        lifetimes.Add(Mathf.FloorToInt(pair.Value));
                    }
                    lifetime[entry.RescueHero] = SphereGridOps.StarterBank(lifetimes);
                }
            }

            foreach (var level in curve.Levels)
            {
                curve.TotalExpectedXp += level.ExpectedXp;
                curve.TotalExpectedGold += level.ExpectedGold;
            }

            curve.EndRoster = new List<HeroSO>(roster);
            foreach (var hero in roster)
            {
                curve.EndLifetimeXp[hero] = Mathf.FloorToInt(lifetime.TryGetValue(hero, out var xp) ? xp : 0f);
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

        private static float RosterAverage(Dictionary<HeroSO, float> lifetime, List<HeroSO> roster)
        {
            if (roster == null || roster.Count == 0)
            {
                return 0f;
            }

            float total = 0f;
            foreach (var hero in roster)
            {
                if (lifetime.TryGetValue(hero, out var xp))
                {
                    total += xp;
                }
            }
            return total / roster.Count;
        }

        private static LevelCurve BuildLevel(int index, RunLevelEntry entry, PartyBaseline party, BalanceRulesSO rules)
        {
            var level = new LevelCurve
            {
                Index = index,
                Name = string.IsNullOrEmpty(entry.LevelName) ? $"Level {index + 1}" : entry.LevelName,
                Template = entry.LevelTemplate,
                Layout = entry.ManualLayout,
                Boss = entry.BossEnemy,
                Entry = entry,
                Tuning = entry.EnemyTuning,

                // Room kinds are a level-template quota, and they cut both ways: each one is a fight
                // the level does not have, and a cache or a refuge it does.
                TreasureRooms = entry.LevelTemplate != null ? Mathf.Max(0, entry.LevelTemplate.TreasureRooms) : 0,
                RestRooms = entry.LevelTemplate != null ? Mathf.Max(0, entry.LevelTemplate.RestRooms) : 0
            };

            if (entry.ManualLayout != null)
            {
                BuildManualRooms(level, entry.ManualLayout, party, rules);
            }
            else if (entry.LevelTemplate != null)
            {
                BuildGeneratedRooms(level, entry.LevelTemplate, party, rules);
            }

            // Events are modelled before the boss displaces a room: the exit room still exists and
            // is still event-eligible (descending is a button), it is only its spawn table that the
            // boss wipes. Reading the post-displacement occurrences would quietly under-count.
            BuildEvents(level, entry, party, rules);

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

            for (int i = 0; i < layout.Rooms.Count; i++)
            {
                var room = layout.Rooms[i];
                if (room == null || room.RoomTemplate == null)
                {
                    continue;
                }

                // The party spawns here and EnemyManager.SpawnEnemies skips the room it is in
                // (`if (room == playerRoom) continue`), so the start room never costs anything.
                if (i == layout.StartRoomIndex)
                {
                    continue;
                }

                var encounter = RoomEncounter.Build(
                    room.RoomTemplate,
                    room.EnemySpawnOverride,
                    room.GuaranteeAllSpawns,
                    party,
                    rules,
                    level.Tuning);
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
            // entry is expected to appear RoomsToGenerate / poolSize times. One of those rooms is
            // the party's starting room, which EnemyManager.SpawnEnemies deliberately skips, so it
            // contributes nothing and is taken off the total before spreading.
            //
            // The level's non-combat rooms come off too: DungeonManager promotes that many rooms to a
            // cache or a refuge before anything spawns, and EnemyManager skips them. Without this the
            // model prices fights the player never has, which reads as attrition the level does not
            // actually charge.
            int nonCombat = Mathf.Max(0, template.TreasureRooms) + Mathf.Max(0, template.RestRooms);
            int populated = Mathf.Max(0, template.RoomsToGenerate - 1 - nonCombat);
            float perEntry = (float)populated / template.RoomPool.Count;

            foreach (var room in template.RoomPool)
            {
                if (room == null)
                {
                    continue;
                }

                var encounter = RoomEncounter.Build(room, null, false, party, rules, level.Tuning);
                encounter.Occurrences = perEntry;
                level.Rooms.Add(encounter);
            }
        }

        /// <summary>
        /// Models the level's room events. Eligibility mirrors <c>DungeonManager.IsEventEligible</c>:
        /// connectors are out (<see cref="RoomEventModel"/> drops them), the party's start room is
        /// already out of every room's <c>Occurrences</c>, and the room holding a captive is taken out
        /// here — one room of the level is spoken for whenever the entry defines a rescue.
        /// </summary>
        private static void BuildEvents(LevelCurve level, RunLevelEntry entry, PartyBaseline party, BalanceRulesSO rules)
        {
            float eligibleRooms = 0f;
            foreach (var room in level.Rooms)
            {
                if (room != null && room.Room != null && !room.Room.IsConnectorRoom)
                {
                    eligibleRooms += room.Occurrences;
                }
            }

            float factor = 1f;
            if (entry.RescueHero != null && eligibleRooms > 1f)
            {
                factor = (eligibleRooms - 1f) / eligibleRooms;
            }

            level.Events = RoomEventModel.BuildForLevel(
                level.Rooms, party, rules, level.Index, factor, level.Tuning);
        }

        private static void ReplaceExitRoomWithBoss(LevelCurve level, RunLevelEntry entry, PartyBaseline party, BalanceRulesSO rules)
        {
            // Take one ordinary combat room back out of the level: the exit room's spawns are wiped
            // before the boss is placed. Spread that single room across every combat entry rather
            // than deleting one outright -- which room the exit lands on is random, and removing a
            // whole pool entry can erase an enemy from the level entirely (it once made Bog Shaman,
            // and therefore Heal, unreachable on the only level that offered it).
            float combatEntries = 0f;
            foreach (var room in level.Rooms)
            {
                if (room.IsCombatRoom)
                {
                    combatEntries += 1f;
                }
            }

            if (combatEntries > 0f)
            {
                float share = 1f / combatEntries;
                foreach (var room in level.Rooms)
                {
                    if (room.IsCombatRoom)
                    {
                        room.Occurrences = Mathf.Max(0f, room.Occurrences - share);
                    }
                }
            }

            var bossEncounter = new RoomEncounter
            {
                Room = null,
                RoomName = $"Exit room — {entry.BossEnemy.Label}",
                GuaranteedSpawns = true,
                IsBossRoom = true,
                Occurrences = 1f,
                Tuning = level.Tuning
            };
            bossEncounter.Expected.Tuning = level.Tuning;
            bossEncounter.WorstCase.Tuning = level.Tuning;
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
                    level.ExpectedCombatHealthCost += room.Occurrences * room.ExpectedHealthCost;
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

            // Room events, folded in on the same footing as combat. They cost sustain (damage, a
            // spent potion, the fight an outcome wakes) and pay gold, XP and loot back, so leaving
            // them out made the attrition curve optimistic and the economy pessimistic at once.
            // EventEngagementRate is the designer's dial for how much of that a player actually takes.
            float engagement = Mathf.Clamp01(rules != null ? rules.EventEngagementRate : 1f);
            foreach (var roomEvent in level.Events)
            {
                if (roomEvent == null)
                {
                    continue;
                }

                level.ExpectedEventRooms += roomEvent.Occurrences;
                level.ExpectedEventHealthCost += engagement * roomEvent.ExpectedSustainCost;
                level.ExpectedEventGold += engagement * roomEvent.ExpectedGold;
                level.ExpectedAfflictions += engagement * roomEvent.ExpectedAfflictions;

                // Only the XP an awakened fight pays; an event is not itself an XP source.
                level.ExpectedXp += engagement * roomEvent.ExpectedXp;
            }

            level.ExpectedGold += level.ExpectedEventGold;

            // Cache gold, priced at the same depth the loot roll uses. A cache is guaranteed once the
            // player walks into it, so unlike an event there is no engagement rate to apply.
            if (level.TreasureRooms > 0)
            {
                level.ExpectedGold += level.TreasureRooms * RoomKindRewards.TreasureGold(level.Index);
            }

            level.ExpectedHealthCost = level.ExpectedCombatHealthCost + level.ExpectedEventHealthCost;

            // A refuge is sustain the level hands back, so it belongs in the denominator beside the
            // party's health and potions - otherwise adding one reads as pure difficulty relief the
            // curve cannot see.
            int restHealing = RoomKindRewards.ExpectedRestHealing(level.RestRooms, party.HealthPool);
            int sustain = party.SustainPool + restHealing;
            level.PartySize = party.Size;
            level.RestHealing = restHealing;
            level.SustainPool = sustain;
            level.Party = party;
            level.AttritionLoad = sustain > 0 ? level.ExpectedHealthCost / sustain : 0f;
        }
    }
}
