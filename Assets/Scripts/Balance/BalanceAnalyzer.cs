using System;
using System.Collections.Generic;
using Assets.Scripts.Cards;
using Assets.Scripts.Dungeon;
using Assets.Scripts.Enemies;
using Assets.Scripts.Heroes;
using Assets.Scripts.Items;
using Assets.Scripts.Progression;
using Assets.Scripts.Resources;
using Assets.Scripts.UnitStats;
using UnityEngine;

namespace Assets.Scripts.Balance
{
    /// <summary>
    /// Everything the analyzer needs, handed in rather than discovered — the runtime assembly has no
    /// AssetDatabase, so the editor window (or a test) collects the assets and passes them here. That
    /// also means a test can analyze a hand-built set of assets in isolation.
    /// </summary>
    public class BalanceInput
    {
        public BalanceRulesSO Rules;

        /// <summary>
        /// The *starting* party. Danger, attrition and win rates are all measured against this,
        /// because judging level 1 content against a fully-recruited roster understates every number.
        /// </summary>
        public List<HeroSO> Heroes = new List<HeroSO>();

        /// <summary>
        /// Every hero asset in the project. Checks about a hero's own authoring — the shape of its
        /// level curve, say — belong here rather than on <see cref="Heroes"/>: they are properties of
        /// the asset, not of who happens to be in the party. Falls back to <see cref="Heroes"/> when
        /// a caller does not populate it.
        /// </summary>
        public List<HeroSO> AllHeroes = new List<HeroSO>();

        /// <summary>Every hero whose own authoring should be checked, party membership aside.</summary>
        public List<HeroSO> HeroesToAudit
        {
            get { return AllHeroes != null && AllHeroes.Count > 0 ? AllHeroes : Heroes; }
        }
        public List<EnemySO> Enemies = new List<EnemySO>();
        public List<RunDefinitionSO> Runs = new List<RunDefinitionSO>();
        public List<MagicSO> Magic = new List<MagicSO>();
        public List<MagicComboSO> Combos = new List<MagicComboSO>();
        public List<ItemSO> Items = new List<ItemSO>();
        public ItemSO HealingPotion;

        public bool RunSimulation = true;
        public bool IncludeSaveAudit = true;

        public Func<string, HeroSO> ResolveHero;
        public Func<string, ItemSO> ResolveItem;
    }

    /// <summary>
    /// Turns the measured metrics into findings by comparing them with <see cref="BalanceRulesSO"/>.
    /// This is the only place the rules are interpreted, so the editor window and the EditMode balance
    /// tests always agree on what "off" means.
    /// </summary>
    public static class BalanceAnalyzer
    {
        public static BalanceReport Analyze(BalanceInput input)
        {
            var report = new BalanceReport();
            if (input == null)
            {
                return report;
            }

            var rules = input.Rules ?? BalanceRulesSO.CreateDefault();

            if (input.IncludeSaveAudit)
            {
                report.Save = SaveAudit.Load(input.ResolveHero, input.ResolveItem, input.HealingPotion);
            }

            report.Party = BuildReferenceParty(input, rules, report.Save);

            // Judge each enemy against the party it is actually first met with. The roster grows
            // during a run (RunLevelEntry.RescueHero), and party size is the strongest lever on
            // danger there is, so measuring a level-3 enemy against the level-1 party reports it as
            // out of band when it never gets fought that way. Falls back to the starting party for
            // anything no run places.
            var partyByEnemy = BuildFirstEncounterParties(input, rules, report.Party);
            report.PartyByEnemy = partyByEnemy;

            foreach (var enemy in input.Enemies)
            {
                if (enemy != null)
                {
                    PartyBaseline against;
                    if (!partyByEnemy.TryGetValue(enemy, out against) || against == null)
                    {
                        against = report.Party;
                    }
                    report.Enemies.Add(EnemyMetrics.Compute(enemy, against, rules, report.Party));
                }
            }

            foreach (var run in input.Runs)
            {
                if (run != null)
                {
                    report.Runs.Add(RunCurve.Build(run, report.Party, rules));
                }
            }

            report.Variety = VarietyReport.Build(ProjectWideEnemySet(input.Enemies), input.Magic, rules);
            report.Progression = ProgressionMap.Build(report.Runs, input.Magic, input.Combos, input.Items);

            if (input.RunSimulation)
            {
                RunSimulations(input, rules, report);
            }

            EvaluateParty(report, rules, input);
            EvaluateEnemies(report, rules);
            EvaluateRuns(report, rules, input);
            EvaluateVariety(report, rules);
            EvaluateProgression(report, rules);
            EvaluateEconomy(report, rules);
            EvaluateSimulations(report, rules);
            EvaluateSave(report, rules, input);

            return report;
        }

        /// <summary>
        /// Maps every enemy a run places to the party it is *first* encountered with. Walks the runs
        /// in order, growing a roster as each level's <c>RescueHero</c> is passed, and records the
        /// smallest roster that meets each enemy. A hero rescued *during* a level does not count for
        /// that level's enemies, matching <see cref="RunCurve"/>'s conservative reading.
        ///
        /// This exists because party size roughly halves per-enemy danger, so a fixed reference
        /// party makes late-run enemies read as out of band and early ones as harmless.
        /// </summary>
        private static Dictionary<EnemySO, PartyBaseline> BuildFirstEncounterParties(
            BalanceInput input, BalanceRulesSO rules, PartyBaseline starting)
        {
            var result = new Dictionary<EnemySO, PartyBaseline>();
            if (input.Runs == null)
            {
                return result;
            }

            var startRoster = new List<HeroSO>();
            foreach (var hero in starting.Heroes)
            {
                if (hero.Definition != null && !startRoster.Contains(hero.Definition))
                {
                    startRoster.Add(hero.Definition);
                }
            }

            // One baseline per roster size, so a four-level run builds at most a handful.
            var bySize = new Dictionary<int, PartyBaseline>();
            bySize[startRoster.Count] = starting;

            var ordered = new List<RunDefinitionSO>(input.Runs);
            ordered.Sort((a, b) =>
            {
                int ai = a != null ? a.SequenceIndex : 0;
                int bi = b != null ? b.SequenceIndex : 0;
                return ai.CompareTo(bi);
            });

            foreach (var run in ordered)
            {
                if (run == null || run.Levels == null)
                {
                    continue;
                }

                var roster = new List<HeroSO>(startRoster);
                foreach (var entry in run.Levels)
                {
                    if (entry == null)
                    {
                        continue;
                    }

                    PartyBaseline party;
                    if (!bySize.TryGetValue(roster.Count, out party))
                    {
                        party = PartyBaseline.Build(roster, rules.ReferenceHeroXp, null,
                            starting.PotionItem, starting.PotionCount);
                        bySize[roster.Count] = party;
                    }

                    foreach (var enemy in EnemiesInLevel(entry))
                    {
                        if (enemy != null && !result.ContainsKey(enemy))
                        {
                            result[enemy] = party;
                        }
                    }

                    if (entry.RescueHero != null && !roster.Contains(entry.RescueHero))
                    {
                        roster.Add(entry.RescueHero);
                    }
                }
            }

            return result;
        }

        /// <summary>Every enemy a level can present: its rooms' spawn tables plus any boss.</summary>
        private static IEnumerable<EnemySO> EnemiesInLevel(RunLevelEntry entry)
        {
            var rooms = new List<Rooms.RoomSO>();
            if (entry.ManualLayout != null && entry.ManualLayout.Rooms != null)
            {
                foreach (var r in entry.ManualLayout.Rooms)
                {
                    if (r != null && r.RoomTemplate != null)
                    {
                        rooms.Add(r.RoomTemplate);
                    }
                    if (r != null && r.EnemySpawnOverride != null)
                    {
                        foreach (var e in r.EnemySpawnOverride)
                        {
                            if (e != null && e.Enemy != null)
                            {
                                yield return e.Enemy;
                            }
                        }
                    }
                }
            }
            else if (entry.LevelTemplate != null && entry.LevelTemplate.RoomPool != null)
            {
                rooms.AddRange(entry.LevelTemplate.RoomPool);
            }

            foreach (var room in rooms)
            {
                if (room == null || room.EnemySpawnTable == null)
                {
                    continue;
                }
                foreach (var e in room.EnemySpawnTable)
                {
                    if (e != null && e.Enemy != null)
                    {
                        yield return e.Enemy;
                    }
                }
            }

            if (entry.BossEnemy != null)
            {
                yield return entry.BossEnemy;
            }
        }

        private static PartyBaseline BuildReferenceParty(BalanceInput input, BalanceRulesSO rules, SaveAudit save)
        {
            Func<HeroSO, List<ItemSO>> gearLookup = null;
            if (rules.ReferencePartyUsesSavedGear && save != null)
            {
                gearLookup = hero =>
                {
                    foreach (var saved in save.Heroes)
                    {
                        if (saved.Definition == hero)
                        {
                            return saved.Gear;
                        }
                    }
                    return new List<ItemSO>();
                };
            }

            int potionCount = save != null && save.PotionCap > 0
                ? save.PotionCap
                : PartyResourceManager.DEFAULT_HEALING_POTION_MAX;

            var party = PartyBaseline.Build(
                input.Heroes,
                rules.ReferenceHeroXp,
                gearLookup,
                input.HealingPotion,
                potionCount);

            party.SourceLabel = rules.ReferencePartyUsesSavedGear
                ? $"Designed baseline ({rules.ReferenceHeroXp} XP spent, saved gear)"
                : $"Designed baseline ({rules.ReferenceHeroXp} XP spent, no gear)";

            return party;
        }

        private static List<WeightedEnemy> ProjectWideEnemySet(List<EnemySO> enemies)
        {
            var members = new List<WeightedEnemy>();
            foreach (var enemy in enemies)
            {
                if (enemy == null)
                {
                    continue;
                }
                members.Add(new WeightedEnemy
                {
                    Definition = enemy,
                    Unit = SimUnit.FromEnemy(enemy),
                    Weight = 1f
                });
            }
            return members;
        }

        // ------------------------------------------------------------------ simulation

        private static void RunSimulations(BalanceInput input, BalanceRulesSO rules, BalanceReport report)
        {
            var settings = new SimSettings
            {
                Trials = rules.SimulationTrials,
                Seed = rules.SimulationSeed,
                MaxTurns = rules.MaxSimTurns,
                Combos = input.Combos,
                PotionCount = report.Party.PotionCount,
                PotionHealAmount = report.Party.PotionHealAmount
            };

            // Every enemy on its own: the cleanest read on one asset's difficulty.
            foreach (var metrics in report.Enemies)
            {
                var unit = SimUnit.FromEnemy(metrics.Definition);
                if (unit == null)
                {
                    continue;
                }

                var simReport = new EncounterSimReport
                {
                    Label = $"{metrics.Name} (solo)",
                    Asset = metrics.Definition,
                    IsBoss = metrics.IsBoss
                };
                simReport.Enemies.Add(unit);

                // Fight it with the party that actually meets it, not the starting party - the boss
                // is never fought solo, and reporting it as unwinnable was an artefact of doing so.
                PartyBaseline against;
                if (!report.PartyByEnemy.TryGetValue(metrics.Definition, out against) || against == null)
                {
                    against = report.Party;
                }

                AssignDrawLoadout(against, simReport.Enemies, report.Save);
                simReport.Outcomes = EncounterSimulator.RunAllPolicies(against, simReport.Enemies, settings);
                report.Simulations.Add(simReport);
            }

            // Each level's hardest expected room, which is where runs actually end.
            foreach (var run in report.Runs)
            {
                foreach (var level in run.Levels)
                {
                    RoomEncounter worst = null;
                    foreach (var room in level.Rooms)
                    {
                        if (!room.IsCombatRoom)
                        {
                            continue;
                        }
                        if (worst == null || room.WorstCaseDanger > worst.WorstCaseDanger)
                        {
                            worst = room;
                        }
                    }

                    if (worst == null)
                    {
                        continue;
                    }

                    var units = worst.WorstCase.ToDiscreteUnits();
                    if (units.Count == 0)
                    {
                        continue;
                    }

                    var simReport = new EncounterSimReport
                    {
                        Label = $"{run.Name} / {level.Reference} — worst room ({worst.RoomName})",
                        Asset = run.Run,
                        IsBoss = level.IsBossLevel && worst.Room == null,
                        Enemies = units
                    };

                    var levelParty = level.Party ?? report.Party;
                    AssignDrawLoadout(levelParty, units, report.Save);
                    simReport.Outcomes = EncounterSimulator.RunAllPolicies(levelParty, units, settings);
                    report.Simulations.Add(simReport);
                }
            }
        }

        /// <summary>
        /// Gives every simulated hero the magic this encounter's enemies actually offer, which is how
        /// the game's loop works: you fight with what the fight lets you Draw. Without this the
        /// simulated party has empty slots, and the attack-spam comparison would only be measuring
        /// potion use rather than whether magic changes the outcome.
        ///
        /// Charges and slot counts come from <see cref="EquippedMagicState"/>'s default plus each
        /// hero's activated MagicSlot grid nodes, and upgrade levels from the save, so the simulated
        /// loadout matches what a player at this point could really be holding. Note that the turn a
        /// Draw costs is not modelled — the loadout is assumed already in hand.
        /// </summary>
        private static void AssignDrawLoadout(PartyBaseline party, IList<SimUnit> enemies, SaveAudit save)
        {
            if (party == null)
            {
                return;
            }

            // Slot counts are per hero now; collect up to the widest count anyone can hold and let
            // each hero take their own prefix of the offer.
            int widestSlotCount = EquippedMagicState.DefaultSlotCount;
            foreach (var hero in party.Heroes)
            {
                int slots = EquippedMagicState.DefaultSlotCount + SphereGridOps.SlotBonusForNodes(
                    hero.Definition != null ? hero.Definition.SphereGrid : null, hero.ActivatedNodes);
                if (slots > widestSlotCount)
                {
                    widestSlotCount = slots;
                }
            }

            var offered = new List<MagicSO>();
            var seen = new HashSet<string>();
            foreach (var enemy in enemies)
            {
                if (enemy == null || enemy.Definition == null || enemy.Definition.DrawableMagics == null)
                {
                    continue;
                }

                foreach (var draw in enemy.Definition.DrawableMagics)
                {
                    if (draw == null || draw.Magic == null)
                    {
                        continue;
                    }

                    string key = string.IsNullOrEmpty(draw.Magic.Key) ? draw.Magic.name : draw.Magic.Key;
                    if (seen.Add(key) && offered.Count < widestSlotCount)
                    {
                        offered.Add(draw.Magic);
                    }
                }
            }

            foreach (var hero in party.Heroes)
            {
                if (hero.Unit == null)
                {
                    continue;
                }

                int slotCount = EquippedMagicState.DefaultSlotCount + SphereGridOps.SlotBonusForNodes(
                    hero.Definition != null ? hero.Definition.SphereGrid : null, hero.ActivatedNodes);

                hero.Unit.MagicSlots.Clear();
                foreach (var magic in offered)
                {
                    if (hero.Unit.MagicSlots.Count >= slotCount)
                    {
                        break;
                    }

                    hero.Unit.MagicSlots.Add(new SimMagicSlot
                    {
                        Magic = magic,
                        Charges = EquippedMagicState.DefaultMaxCharges,
                        MaxCharges = EquippedMagicState.DefaultMaxCharges,
                        UpgradeLevel = UpgradeLevelFor(magic, save)
                    });
                }
            }
        }

        private static int UpgradeLevelFor(MagicSO magic, SaveAudit save)
        {
            if (save == null || magic == null)
            {
                return 0;
            }

            string key = string.IsNullOrEmpty(magic.Key) ? magic.name : magic.Key;
            foreach (var upgrade in save.MagicUpgrades)
            {
                if (upgrade.Key == key)
                {
                    return upgrade.Level;
                }
            }
            return 0;
        }

        // ------------------------------------------------------------------ party

        private static void EvaluateParty(BalanceReport report, BalanceRulesSO rules, BalanceInput input)
        {
            var party = report.Party;
            if (party == null || party.Size == 0)
            {
                report.Issues.Add(new BalanceIssue(BalanceSeverity.Critical, BalanceCategory.Party, "Party",
                    "No heroes to analyze")
                {
                    Detail = "The analyzer found no HeroSO assets, so nothing can be measured against a party.",
                    Suggestion = "Check that hero definitions exist under Assets/ScriptableObjects/Heroes."
                });
                return;
            }

            // The single most important ratio in the game: how many ordinary hits a hero survives.
            foreach (var hero in party.Heroes)
            {
                int worstHitsToKill = int.MaxValue;
                string worstEnemy = "";

                foreach (var metrics in report.Enemies)
                {
                    if (metrics.IsBoss)
                    {
                        continue;
                    }
                    foreach (var record in metrics.PerHero)
                    {
                        if (record.HeroName != hero.Name)
                        {
                            continue;
                        }
                        if (record.HitsToKill < worstHitsToKill)
                        {
                            worstHitsToKill = record.HitsToKill;
                            worstEnemy = metrics.Name;
                        }
                    }
                }

                if (worstHitsToKill == int.MaxValue)
                {
                    continue;
                }

                if (worstHitsToKill <= 1)
                {
                    report.Issues.Add(new BalanceIssue(BalanceSeverity.Critical, BalanceCategory.Party, hero.Name,
                        $"{hero.Name} is one-shot by ordinary enemies")
                    {
                        Asset = hero.Definition,
                        Detail = $"{worstEnemy} kills {hero.Name} in a single hit "
                               + $"({hero.Effective[StatType.MaxHealth]} max HP). Fights are decided by turn order, not play.",
                        Suggestion = $"Raise max HP to roughly {SuggestedHeroHealth(hero, report, rules)} "
                               + $"so a plain hit costs about {(1f / rules.TargetHitsToKillHero):P0} of the bar."
                    });
                }
                else if (worstHitsToKill < rules.MinHitsToKillHero)
                {
                    report.Issues.Add(new BalanceIssue(BalanceSeverity.Warning, BalanceCategory.Party, hero.Name,
                        $"{hero.Name} survives only {worstHitsToKill} ordinary hits")
                    {
                        Asset = hero.Definition,
                        Detail = $"Worst case is {worstEnemy}; the target is at least {rules.MinHitsToKillHero} hits.",
                        Suggestion = $"Raise max HP toward {SuggestedHeroHealth(hero, report, rules)}."
                    });
                }

                // A progression that stops immediately is a dead end no amount of stat tuning fixes.
                if (hero.NodesTotal == 0)
                {
                    report.Issues.Add(new BalanceIssue(BalanceSeverity.Warning, BalanceCategory.Progression, hero.Name,
                        $"{hero.Name} has no sphere grid")
                    {
                        Asset = hero.Definition,
                        Detail = "SphereGrid is unset or empty, so banked XP can never be spent.",
                        Suggestion = "Author a grid asset and assign it to HeroSO.SphereGrid."
                    });
                }
                else if (hero.NodesTotal < rules.MinGridNodes)
                {
                    report.Issues.Add(new BalanceIssue(BalanceSeverity.Warning, BalanceCategory.Progression, hero.Name,
                        $"{hero.Name}'s sphere grid runs out almost immediately")
                    {
                        Asset = hero.Definition,
                        Detail = $"Only {hero.NodesTotal} node(s) are authored against a floor of "
                               + $"{rules.MinGridNodes}, so XP stops mattering almost immediately.",
                        Suggestion = "Extend the grid to cover the XP the later run content pays out."
                    });
                }

            }

            // Deliberately over every hero asset, not just the party: a grid is authored on the
            // asset, so a hero who joins at depth 3 can carry a broken one for the whole run
            // without ever being measured. The Tank's +5 Agility on a base of 5 was invisible for
            // exactly this reason.
            foreach (var definition in input.HeroesToAudit)
            {
                EvaluateNodeGainShape(definition, report);
                EvaluateGridAuthoring(definition, report);
            }

            EvaluateHealing(report, rules, input);
            EvaluateMaxHealthGearMismatch(report, input);
        }

        /// <summary>
        /// Largest share of its base an <b>output</b> stat may gain from one node. +5 Agility on a
        /// base of 5 doubles the hero's turn rate in a single activation, which no other number is
        /// scaled for.
        /// </summary>
        private const float MaxOutputStatGainShare = 0.5f;

        /// <summary>
        /// The same limit for a <b>pool</b> stat, which is deliberately far looser. Health bars start
        /// small and are *meant* to grow in large relative steps. Holding pools to the output
        /// threshold reported every hero and meant nothing.
        /// </summary>
        private const float MaxPoolStatGainShare = 1f;

        /// <summary>
        /// Flags grid nodes that move a stat by an implausible share of its base.
        ///
        /// <para>Every stat in <see cref="StatCatalog.Types"/> is checked, and the threshold comes
        /// from <see cref="StatDefinition.IsPool"/>, because a share means something different for a
        /// health bar than for a damage stat — the same rule the old level-curve check applied.</para>
        /// </summary>
        private static void EvaluateNodeGainShape(HeroSO definition, BalanceReport report)
        {
            if (definition == null || definition.SphereGrid == null || definition.SphereGrid.Nodes == null)
            {
                return;
            }

            foreach (var node in definition.SphereGrid.Nodes)
            {
                if (node == null || node.Kind != SphereNodeKind.Stat || node.Gains == null)
                {
                    continue;
                }

                foreach (var stat in StatCatalog.Types)
                {
                    CheckGain(definition, report, node.Key, stat,
                        node.Gains[stat], definition.BaseStats[stat]);
                }
            }
        }

        private static void CheckGain(HeroSO definition, BalanceReport report, string nodeKey, StatType stat, int gain, int baseValue)
        {
            if (baseValue <= 0 || gain <= 0)
            {
                return;
            }

            float limit = StatCatalog.Of(stat).IsPool ? MaxPoolStatGainShare : MaxOutputStatGainShare;
            float share = (float)gain / baseValue;
            if (share < limit)
            {
                return;
            }

            string name = definition.DisplayName;
            report.Issues.Add(new BalanceIssue(BalanceSeverity.Warning, BalanceCategory.Progression, name,
                $"Node '{nodeKey}' raises {name}'s {StatCatalog.DisplayName(stat)} by {share:P0}")
            {
                Asset = definition,
                Detail = $"+{gain} on a base of {baseValue}. A single node should not reshape a stat this much.",
                Suggestion = $"Split the {StatCatalog.DisplayName(stat)} gain across more nodes."
            });
        }

        /// <summary>
        /// Structural faults in a grid asset the rules layer merely survives: duplicate node keys
        /// (first-in-list wins, the rest are dead weight), neighbour keys that match no node, and
        /// nodes no path from the start can ever reach.
        /// </summary>
        private static void EvaluateGridAuthoring(HeroSO definition, BalanceReport report)
        {
            if (definition == null || definition.SphereGrid == null || definition.SphereGrid.Nodes == null)
            {
                return;
            }

            var grid = definition.SphereGrid;
            string name = definition.DisplayName;
            var seenKeys = new List<string>();

            foreach (var node in grid.Nodes)
            {
                if (node == null || string.IsNullOrEmpty(node.Key))
                {
                    continue;
                }

                if (seenKeys.Contains(node.Key))
                {
                    report.Issues.Add(new BalanceIssue(BalanceSeverity.Warning, BalanceCategory.Progression, name,
                        $"{name}'s grid has a duplicate node key '{node.Key}'")
                    {
                        Asset = grid,
                        Detail = "Every rule resolves a key to the first node in the list, so the duplicate "
                               + "can never be activated or granted.",
                        Suggestion = "Rename one of them. Keys are save data, so rename the unused copy."
                    });
                    continue;
                }
                seenKeys.Add(node.Key);

                if (node.Neighbors != null)
                {
                    foreach (var neighbor in node.Neighbors)
                    {
                        if (!string.IsNullOrEmpty(neighbor) &&
                            SphereGridOps.FindNode(grid, neighbor) == null)
                        {
                            report.Issues.Add(new BalanceIssue(BalanceSeverity.Warning, BalanceCategory.Progression, name,
                                $"Node '{node.Key}' on {name}'s grid lists a neighbour '{neighbor}' that does not exist")
                            {
                                Asset = grid,
                                Detail = "Dangling edges are dropped at runtime, so this link silently does nothing.",
                                Suggestion = "Fix the key or remove the edge."
                            });
                        }
                    }
                }
            }

            // Reachability: flood out from the start node along real edges. A node the flood never
            // touches can never be bought, whatever it costs.
            var reachable = new List<string>();
            string start = SphereGridOps.StartKey(grid);
            if (!string.IsNullOrEmpty(start))
            {
                var adjacency = SphereGridOps.BuildAdjacency(grid);
                var queue = new Queue<string>();
                queue.Enqueue(start);
                reachable.Add(start);
                while (queue.Count > 0)
                {
                    var current = queue.Dequeue();
                    foreach (var neighbor in adjacency[current])
                    {
                        if (!reachable.Contains(neighbor))
                        {
                            reachable.Add(neighbor);
                            queue.Enqueue(neighbor);
                        }
                    }
                }
            }

            foreach (var key in seenKeys)
            {
                if (!reachable.Contains(key))
                {
                    report.Issues.Add(new BalanceIssue(BalanceSeverity.Warning, BalanceCategory.Progression, name,
                        $"Node '{key}' is orphaned — no path from the start node")
                    {
                        Asset = grid,
                        Detail = "Activation grows along edges from the start node, so this node can never "
                               + "be bought.",
                        Suggestion = "Connect it to the reachable part of the grid."
                    });
                }
            }
        }

        private static void EvaluateHealing(BalanceReport report, BalanceRulesSO rules, BalanceInput input)
        {
            var party = report.Party;

            // Potions and heal spells that top a hero straight off have no decision in them.
            if (input.HealingPotion != null && input.HealingPotion.ConsumableAmount > 0)
            {
                foreach (var hero in party.Heroes)
                {
                    if (hero.Effective[StatType.MaxHealth] <= 0)
                    {
                        continue;
                    }

                    float fraction = (float)input.HealingPotion.ConsumableAmount / hero.Effective[StatType.MaxHealth];
                    if (fraction >= rules.MaxSingleHealFraction)
                    {
                        var severity = fraction >= 1f ? BalanceSeverity.Warning : BalanceSeverity.Info;
                        report.Issues.Add(new BalanceIssue(severity, BalanceCategory.Party, hero.Name,
                            $"One potion restores {fraction:P0} of {hero.Name}'s health")
                        {
                            Asset = input.HealingPotion,
                            Detail = $"{input.HealingPotion.ConsumableAmount} HP against a {hero.Effective[StatType.MaxHealth]} HP bar.",
                            Suggestion = "Either raise hero max HP or lower ConsumableAmount so healing is a partial recovery."
                        });
                    }
                }
            }

            foreach (var magic in input.Magic)
            {
                if (magic == null || magic.Effects == null)
                {
                    continue;
                }

                foreach (var effect in magic.Effects)
                {
                    if (effect == null || effect.EffectType != SpellEffectType.Heal || effect.Power <= 0)
                    {
                        continue;
                    }

                    foreach (var hero in party.Heroes)
                    {
                        if (hero.Effective[StatType.MaxHealth] <= 0 || effect.Power < hero.Effective[StatType.MaxHealth])
                        {
                            continue;
                        }

                        report.Issues.Add(new BalanceIssue(BalanceSeverity.Warning, BalanceCategory.Party, magic.DisplayName,
                            $"{magic.DisplayName} heals more than {hero.Name}'s entire health bar")
                        {
                            Asset = magic,
                            Detail = $"Heal power {effect.Power} against {hero.Effective[StatType.MaxHealth]} max HP — always a full heal.",
                            Suggestion = "Scale heal power to a fraction of a hero's bar once HP is retuned."
                        });
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Party.HealAll() fills Stats.MaxHealth, which excludes gear, while the heal cap and HP bar
        /// read GetEffectiveMaxHealth(). Any +MaxHealth item therefore grants a slice of health the
        /// party can never actually be healed into at level start.
        /// </summary>
        private static void EvaluateMaxHealthGearMismatch(BalanceReport report, BalanceInput input)
        {
            foreach (var item in input.Items)
            {
                if (item == null || item.Bonuses == null)
                {
                    continue;
                }

                foreach (var bonus in item.Bonuses)
                {
                    if (bonus == null || bonus.StatType != StatType.MaxHealth || bonus.Value <= 0f)
                    {
                        continue;
                    }

                    report.Issues.Add(new BalanceIssue(BalanceSeverity.Warning, BalanceCategory.Party, item.DisplayName,
                        "+MaxHealth gear is never filled at level start")
                    {
                        Asset = item,
                        Detail = "Party.HealAll() sets Health = Stats.MaxHealth (base only), but the heal cap and "
                               + "HP bar use GetEffectiveMaxHealth() (base + gear). Heroes start each level short "
                               + $"by this item's {bonus.Value:0.#} MaxHealth.",
                        Suggestion = "Have HealAll() heal to GetEffectiveMaxHealth() for heroes."
                    });
                    return;
                }
            }
        }

        private static int SuggestedHeroHealth(HeroBaseline hero, BalanceReport report, BalanceRulesSO rules)
        {
            // Size the bar so an average non-boss hit costs 1/TargetHitsToKillHero of it.
            float worstHit = 0f;
            foreach (var metrics in report.Enemies)
            {
                if (metrics.IsBoss)
                {
                    continue;
                }
                foreach (var record in metrics.PerHero)
                {
                    if (record.HeroName == hero.Name && record.DamagePerHit > worstHit)
                    {
                        worstHit = record.DamagePerHit;
                    }
                }
            }

            if (worstHit <= 0f)
            {
                return hero.Effective[StatType.MaxHealth];
            }

            return Mathf.Max(1, Mathf.RoundToInt(worstHit * rules.TargetHitsToKillHero));
        }

        // ------------------------------------------------------------------ enemies

        private static void EvaluateEnemies(BalanceReport report, BalanceRulesSO rules)
        {
            foreach (var metrics in report.Enemies)
            {
                float ceiling = metrics.DangerCeiling(rules);

                if (metrics.SoloDangerIndex >= 1f)
                {
                    report.Issues.Add(new BalanceIssue(BalanceSeverity.Critical, BalanceCategory.Enemy, metrics.Name,
                        $"{metrics.Name} beats the party on paper (danger {metrics.SoloDangerIndex:0.00})")
                    {
                        Asset = metrics.Definition,
                        Detail = $"The party needs {metrics.PartyTurnsToKill:0.0} turns to kill it; it needs fewer "
                               + "to wipe the party. Danger at or above 1.00 means the encounter is lost before "
                               + "any decisions are made.",
                        Suggestion = SuggestEnemySoftening(metrics, rules)
                    });
                }
                else if (metrics.SoloDangerIndex > ceiling)
                {
                    report.Issues.Add(new BalanceIssue(BalanceSeverity.Warning, BalanceCategory.Enemy, metrics.Name,
                        $"{metrics.Name} is above its danger band ({metrics.SoloDangerIndex:0.00} > {ceiling:0.00})")
                    {
                        Asset = metrics.Definition,
                        Detail = $"{(metrics.IsBoss ? "Boss" : "Trash")} target is {ceiling:0.00}. "
                               + $"Effective damage per turn {metrics.EffectiveDamagePerTurn:0.0}, "
                               + $"party turns to kill {metrics.PartyTurnsToKill:0.0}.",
                        Suggestion = SuggestEnemySoftening(metrics, rules)
                    });
                }
                else if (metrics.SoloDangerIndex < rules.MinMeaningfulDanger
                         && metrics.SoloDangerIndex > 0f
                         && metrics.Archetype != EnemyArchetype.Healer)
                {
                    // Healers are exempt: the danger index measures a damage race, and a Healer's cost to
                    // the player is the turns it adds by undoing damage, not the damage it deals. Judging
                    // one on offence would always read as "no threat".
                    report.Issues.Add(new BalanceIssue(BalanceSeverity.Warning, BalanceCategory.Enemy, metrics.Name,
                        $"{metrics.Name} is no threat at all (danger {metrics.SoloDangerIndex:0.000})")
                    {
                        Asset = metrics.Definition,
                        Detail = $"It deals {metrics.EffectiveDamagePerTurn:0.0} per turn and dies in "
                               + $"{metrics.PartyTurnsToKill:0.0} party turns. It costs the player time, not resources.",
                        Suggestion = "Raise Attack, or drop it from spawn tables in favour of a real encounter."
                    });
                }

                if (metrics.FewestHitsToKillAHero <= 1 && metrics.FewestHitsToKillAHero > 0)
                {
                    report.Issues.Add(new BalanceIssue(BalanceSeverity.Critical, BalanceCategory.Enemy, metrics.Name,
                        $"{metrics.Name} one-shots {metrics.FastestKillTarget}")
                    {
                        Asset = metrics.Definition,
                        Detail = $"Attack {metrics.Definition.BaseStats[StatType.Strength]} lands "
                               + $"{FindPerHero(metrics, metrics.FastestKillTarget):0.0} average damage on a hero who "
                               + "cannot survive one hit.",
                        Suggestion = "Lower Attack, or raise hero max HP — the HP:damage scale is the root cause."
                    });
                }

                float ttkCeiling = metrics.TimeToKillCeiling(rules);
                if (metrics.PartyTurnsToKill > ttkCeiling)
                {
                    report.Issues.Add(new BalanceIssue(BalanceSeverity.Warning, BalanceCategory.Enemy, metrics.Name,
                        $"{metrics.Name} takes {metrics.PartyTurnsToKill:0.0} party turns to kill")
                    {
                        Asset = metrics.Definition,
                        Detail = $"Target ceiling is {ttkCeiling:0.0} turns. Long fights without added pressure read as a slog.",
                        Suggestion = $"Lower Health toward "
                               + $"{Mathf.RoundToInt(metrics.Definition.BaseStats[StatType.MaxHealth] * ttkCeiling / Mathf.Max(0.01f, metrics.PartyTurnsToKill))}."
                    });
                }
                else if (metrics.PartyTurnsToKill < rules.MinEnemyTimeToKill && metrics.PartyTurnsToKill > 0f)
                {
                    report.Issues.Add(new BalanceIssue(BalanceSeverity.Info, BalanceCategory.Enemy, metrics.Name,
                        $"{metrics.Name} dies in {metrics.PartyTurnsToKill:0.0} party turns")
                    {
                        Asset = metrics.Definition,
                        Detail = $"Below the {rules.MinEnemyTimeToKill:0.0}-turn floor: it never gets to act meaningfully.",
                        Suggestion = "Raise Health, or use it only in groups."
                    });
                }

                if (metrics.ActionShareVsParty >= 1.5f)
                {
                    report.Issues.Add(new BalanceIssue(BalanceSeverity.Info, BalanceCategory.Enemy, metrics.Name,
                        $"{metrics.Name} acts {metrics.ActionShareVsParty:0.0}x as often as a hero")
                    {
                        Asset = metrics.Definition,
                        Detail = $"Agility {metrics.Definition.BaseStats[StatType.Agility]} against the party average. Its real threat is "
                               + "that multiple of what its Attack suggests, and nothing in the inspector shows it.",
                        Suggestion = "Treat Agility as a damage multiplier when tuning this enemy."
                    });
                }

                if (metrics.Definition.XpReward <= 0)
                {
                    report.Issues.Add(new BalanceIssue(BalanceSeverity.Info, BalanceCategory.Economy, metrics.Name,
                        $"{metrics.Name} awards no XP")
                    {
                        Asset = metrics.Definition,
                        Detail = "XpReward is 0, so killing it cannot advance hero levels.",
                        Suggestion = "Set an XpReward proportional to its danger."
                    });
                }
            }

            EvaluateRewardSpread(report, rules);
        }

        private static float FindPerHero(EnemyMetrics metrics, string heroName)
        {
            foreach (var record in metrics.PerHero)
            {
                if (record.HeroName == heroName)
                {
                    return record.DamagePerHit;
                }
            }
            return 0f;
        }

        private static string SuggestEnemySoftening(EnemyMetrics metrics, BalanceRulesSO rules)
        {
            float ceiling = metrics.DangerCeiling(rules);
            if (metrics.SoloDangerIndex <= 0f || float.IsInfinity(metrics.SoloDangerIndex))
            {
                return "Reduce Attack or Health until the danger index falls inside the band.";
            }

            float scale = ceiling / metrics.SoloDangerIndex;
            int suggestedAttack = Mathf.Max(1, Mathf.RoundToInt(metrics.Definition.BaseStats[StatType.Strength] * scale));
            int suggestedHealth = Mathf.Max(1, Mathf.RoundToInt(metrics.Definition.BaseStats[StatType.MaxHealth] * scale));

            return $"Either Attack {metrics.Definition.BaseStats[StatType.Strength]} → {suggestedAttack}, or Health "
                 + $"{metrics.Definition.BaseStats[StatType.MaxHealth]} → {suggestedHealth} (or split the difference). "
                 + "Raising hero HP instead fixes it for every enemy at once.";
        }

        private static void EvaluateRewardSpread(BalanceReport report, BalanceRulesSO rules)
        {
            float min = float.MaxValue;
            float max = 0f;
            string minName = "";
            string maxName = "";

            foreach (var metrics in report.Enemies)
            {
                // Bosses are their own tier, and a Healer's danger index understates it by design (see
                // the exemption in EvaluateEnemies), so neither belongs in a reward-per-danger comparison.
                if (metrics.IsBoss
                    || metrics.Archetype == EnemyArchetype.Healer
                    || metrics.XpPerDanger <= 0f
                    || float.IsInfinity(metrics.XpPerDanger))
                {
                    continue;
                }
                if (metrics.XpPerDanger < min)
                {
                    min = metrics.XpPerDanger;
                    minName = metrics.Name;
                }
                if (metrics.XpPerDanger > max)
                {
                    max = metrics.XpPerDanger;
                    maxName = metrics.Name;
                }
            }

            if (min >= float.MaxValue || min <= 0f)
            {
                return;
            }

            float spread = max / min;
            if (spread > rules.MaxRewardEfficiencySpread)
            {
                report.Issues.Add(new BalanceIssue(BalanceSeverity.Warning, BalanceCategory.Economy, "Reward curve",
                    $"XP per unit of danger varies {spread:0.0}x across enemies")
                {
                    Detail = $"{maxName} pays {max:0} XP per danger; {minName} pays {min:0}. "
                           + $"The band allows {rules.MaxRewardEfficiencySpread:0.0}x.",
                    Suggestion = $"Scale XpReward with danger — {minName} is underpaying for the risk it carries."
                });
            }
        }

        // ------------------------------------------------------------------ runs and levels

        private static void EvaluateRuns(BalanceReport report, BalanceRulesSO rules, BalanceInput input)
        {
            foreach (var run in report.Runs)
            {
                if (run.Levels.Count == 0)
                {
                    report.Issues.Add(new BalanceIssue(BalanceSeverity.Warning, BalanceCategory.Run, run.Name,
                        "Run has no levels")
                    {
                        Asset = run.Run,
                        Detail = "The Levels list is empty, so this run cannot be played."
                    });
                    continue;
                }

                foreach (var level in run.Levels)
                {
                    EvaluateLevel(run, level, report, rules);
                }

                for (int i = 0; i < run.DifficultyJumps.Count; i++)
                {
                    float jump = run.DifficultyJumps[i];
                    string from = run.Levels[i].Reference;
                    string to = run.Levels[i + 1].Reference;

                    if (jump > rules.MaxDifficultyJump)
                    {
                        report.Issues.Add(new BalanceIssue(BalanceSeverity.Warning, BalanceCategory.Run, run.Name,
                            $"Difficulty spikes {jump:P0} from {from} to {to}")
                        {
                            Asset = run.Run,
                            Detail = $"Attrition load {run.Levels[i].AttritionLoad:0.00} → "
                                   + $"{run.Levels[i + 1].AttritionLoad:0.00}; the ceiling is {rules.MaxDifficultyJump:P0}.",
                            Suggestion = "Add an intermediate step, or soften the later level's spawn tables."
                        });
                    }
                    else if (jump < rules.MinDifficultyJump)
                    {
                        report.Issues.Add(new BalanceIssue(BalanceSeverity.Info, BalanceCategory.Run, run.Name,
                            $"{to} is no harder than {from} ({jump:P0})")
                        {
                            Asset = run.Run,
                            Detail = $"Attrition load {run.Levels[i].AttritionLoad:0.00} → "
                                   + $"{run.Levels[i + 1].AttritionLoad:0.00}. A flat curve gives the run no shape.",
                            Suggestion = "Vary the level templates or spawn tables so each level escalates."
                        });
                    }
                }

                EvaluateRunXpSupply(run, report, input);
            }
        }

        private static void EvaluateLevel(RunCurve run, LevelCurve level, BalanceReport report, BalanceRulesSO rules)
        {
            string subject = $"{run.Name} / {level.Reference}";

            if (level.ExpectedCombatRooms <= 0f)
            {
                report.Issues.Add(new BalanceIssue(BalanceSeverity.Info, BalanceCategory.Level, subject,
                    $"{level.Reference} has no combat")
                {
                    Asset = run.Run,
                    Detail = "No room in this level has a populated spawn table."
                });
                return;
            }

            if (level.AttritionMargin < 0f)
            {
                report.Issues.Add(new BalanceIssue(BalanceSeverity.Critical, BalanceCategory.Level, subject,
                    $"{level.Reference} is unclearable on one health bar")
                {
                    Asset = run.Run,
                    Detail = $"Expected cost {level.ExpectedHealthCost:0} HP against a sustain pool of "
                           + $"{level.SustainPool} ({level.PartySize} hero(es) + potions) across "
                           + $"{level.ExpectedCombatRooms:0.0} combat rooms. "
                           + "Health only refills between levels, so the party runs out mid-level.",
                    Suggestion = $"Cut expected combat rooms to about "
                           + $"{level.ExpectedCombatRooms * (1f - rules.MinAttritionMargin) / Mathf.Max(0.01f, level.AttritionLoad):0.0}, "
                           + "soften the spawn tables, or raise party HP."
                });
            }
            else if (level.AttritionMargin < rules.MinAttritionMargin)
            {
                report.Issues.Add(new BalanceIssue(BalanceSeverity.Warning, BalanceCategory.Level, subject,
                    $"{level.Reference} leaves only {level.AttritionMargin:P0} of the party's resources")
                {
                    Asset = run.Run,
                    Detail = $"Expected cost {level.ExpectedHealthCost:0} HP of a {level.SustainPool} pool "
                           + $"({level.PartySize} hero(es)); the target margin is {rules.MinAttritionMargin:P0}.",
                    Suggestion = "Reduce room count or spawn density, or add in-level healing."
                });
            }

            if (level.PeakWorstCaseDanger >= 1f)
            {
                report.Issues.Add(new BalanceIssue(BalanceSeverity.Warning, BalanceCategory.Level, subject,
                    $"A bad spawn roll in {level.Reference} is unwinnable (worst-case danger {level.PeakWorstCaseDanger:0.00})")
                {
                    Asset = run.Run,
                    Detail = $"Expected peak danger is {level.PeakRoomDanger:0.00}, but every spawn roll landing "
                           + $"gives {level.PeakWorstCaseDanger:0.00}. Players meet the worst case regularly.",
                    Suggestion = "Lower SpawnChance or EvaluationCount so the tail is survivable."
                });
            }

            if (level.Boss != null && level.BossToTrashRatio > 0f)
            {
                if (level.BossToTrashRatio > rules.MaxBossToTrashRatio)
                {
                    report.Issues.Add(new BalanceIssue(BalanceSeverity.Critical, BalanceCategory.Level, subject,
                        $"{level.Reference} boss is {level.BossToTrashRatio:0.0}x the level's trash difficulty")
                    {
                        Asset = level.Boss,
                        Detail = $"Boss danger {level.BossDanger:0.00} against an average room of "
                               + $"{level.BossDanger / Mathf.Max(0.001f, level.BossToTrashRatio):0.00}. "
                               + $"The band is {rules.MinBossToTrashRatio:0.0}x–{rules.MaxBossToTrashRatio:0.0}x.",
                        Suggestion = "Nothing in the level prepares the player for this. Soften the boss or "
                               + "escalate the trash leading to it."
                    });
                }
                else if (level.BossToTrashRatio < rules.MinBossToTrashRatio)
                {
                    report.Issues.Add(new BalanceIssue(BalanceSeverity.Warning, BalanceCategory.Level, subject,
                        $"{level.Reference} boss is only {level.BossToTrashRatio:0.0}x the level's trash difficulty")
                    {
                        Asset = level.Boss,
                        Detail = $"A climax should stand out; the floor is {rules.MinBossToTrashRatio:0.0}x.",
                        Suggestion = "Raise the boss's Health or Attack, or give it adds."
                    });
                }
            }
        }

        /// <summary>
        /// Does the run actually pay out the XP the hero curve asks for? Measured as a <em>share</em>,
        /// not a total: <c>Party.DistributeXp</c> splits every kill evenly across the fielded party
        /// (<see cref="XpSplit"/>), so a run that looks generous in aggregate can still fail to level
        /// anybody once it is divided four ways. That division is the intended cost of a wide party,
        /// which is why this reports the shortfall rather than treating the split itself as a fault.
        /// </summary>
        private static void EvaluateRunXpSupply(RunCurve run, BalanceReport report, BalanceInput input)
        {
            if (report.Party == null || report.Party.Size == 0)
            {
                return;
            }

            var leader = report.Party.Heroes[0];
            int cheapest = SphereGridOps.CheapestFrontierCost(
                leader.Definition != null ? leader.Definition.SphereGrid : null,
                leader.ActivatedNodes);

            // The party grows across a run, so the share shrinks as it goes. Judge against the
            // widest party the run ever fields - the pessimistic reading, and the one a player who
            // takes every recruit will actually see.
            int widest = report.Party.Size;
            foreach (var level in run.Levels)
            {
                if (level.PartySize > widest)
                {
                    widest = level.PartySize;
                }
            }

            float share = XpSplit.ExpectedShare(run.TotalExpectedXp, widest);
            if (cheapest > 0 && share < cheapest)
            {
                string split = widest > 1
                    ? $" split {widest} ways ({run.TotalExpectedXp:0} total)"
                    : string.Empty;
                report.Issues.Add(new BalanceIssue(BalanceSeverity.Warning, BalanceCategory.Progression, run.Name,
                    "The whole run pays less XP than one sphere node costs")
                {
                    Asset = run.Run,
                    Detail = $"Expected {share:0} XP per hero across every room{split}; {leader.Name}'s "
                           + $"cheapest unactivated node costs {cheapest}.",
                    Suggestion = "Raise XpReward on enemies, lower XpCost on the grid's early nodes, or "
                               + "field a narrower party."
                });
            }
        }

        // ------------------------------------------------------------------ variety

        private static void EvaluateVariety(BalanceReport report, BalanceRulesSO rules)
        {
            var variety = report.Variety;
            if (variety == null || variety.TotalWeight <= 0f)
            {
                return;
            }

            if (variety.DominantArchetypeShare > rules.MaxArchetypeShare)
            {
                report.Issues.Add(new BalanceIssue(BalanceSeverity.Warning, BalanceCategory.Variety, variety.Scope,
                    $"{variety.DominantArchetypeShare:P0} of enemies are {variety.DominantArchetype}")
                {
                    Detail = $"The ceiling is {rules.MaxArchetypeShare:P0}. When one archetype dominates, every "
                           + "fight asks the player the same question.",
                    Suggestion = "Re-archetype some enemies, or add Bruiser/Healer/Debuffer definitions."
                });
            }

            if (variety.ResistanceCoverage < rules.MinResistanceCoverage)
            {
                var severity = variety.ResistanceCoverage <= 0f ? BalanceSeverity.Critical : BalanceSeverity.Warning;
                report.Issues.Add(new BalanceIssue(severity, BalanceCategory.Variety, variety.Scope,
                    variety.ResistanceCoverage <= 0f
                        ? "No enemy has any resistance — the elemental layer does nothing"
                        : $"Only {variety.ResistanceCoverage:P0} of enemies carry a resistance")
                {
                    Detail = "DamageCalculator applies resistance before defense, so with no resistances anywhere "
                           + "every damage type is arithmetically identical. Fire, Ice, Lightning, Holy and Shadow "
                           + "are decoration, and choosing which magic to cast never depends on the target.",
                    Suggestion = $"Give at least {rules.MinResistanceCoverage:P0} of enemies a meaningful "
                           + "resistance (and a weakness) so element choice becomes a real decision."
                });
            }

            if (variety.InertDamageTypes.Count > 0)
            {
                report.Issues.Add(new BalanceIssue(BalanceSeverity.Warning, BalanceCategory.Variety, variety.Scope,
                    $"{variety.InertDamageTypes.Count} damage type(s) exist in magic but nothing resists them")
                {
                    Detail = $"Inert: {string.Join(", ", variety.InertDamageTypes)}. Spells deal these types but no "
                           + "enemy in scope resists or is weak to them, so the type never changes an outcome.",
                    Suggestion = "Add resistances covering these types to some enemies."
                });
            }

            if (variety.UnusedDamageTypes.Count > 0)
            {
                report.Issues.Add(new BalanceIssue(BalanceSeverity.Info, BalanceCategory.Variety, variety.Scope,
                    $"{variety.UnusedDamageTypes.Count} damage type(s) are unused by any magic")
                {
                    Detail = $"Unused: {string.Join(", ", variety.UnusedDamageTypes)}.",
                    Suggestion = "Either author magic that uses them or trim the DamageType enum."
                });
            }

            foreach (var overlap in variety.DrawOverlaps)
            {
                string a = string.IsNullOrEmpty(overlap.A.DisplayName) ? overlap.A.name : overlap.A.DisplayName;
                string b = string.IsNullOrEmpty(overlap.B.DisplayName) ? overlap.B.name : overlap.B.DisplayName;

                report.Issues.Add(new BalanceIssue(BalanceSeverity.Warning, BalanceCategory.Variety, $"{a} / {b}",
                    $"{a} and {b} offer {overlap.Share:P0} the same Draw list")
                {
                    Asset = overlap.A,
                    Detail = $"Shared: {string.Join(", ", overlap.SharedMagic)}. Draw variety is what makes two "
                           + "fights play differently; identical offerings collapse that.",
                    Suggestion = "Give each enemy a distinctive magic so the player has a reason to fight it."
                });
            }

            foreach (var duplicate in variety.DuplicateLootPairs)
            {
                report.Issues.Add(new BalanceIssue(BalanceSeverity.Info, BalanceCategory.Variety, "Loot",
                    "Several enemies drop the same item")
                {
                    Detail = duplicate,
                    Suggestion = "Vary LootItem so kills feel distinct."
                });
            }

            if (variety.EnemiesWithoutDrawList > 0)
            {
                report.Issues.Add(new BalanceIssue(BalanceSeverity.Info, BalanceCategory.Variety, variety.Scope,
                    $"{variety.EnemiesWithoutDrawList} enemy definition(s) offer no Draw")
                {
                    Detail = "Draw is the party's only route to new magic; an enemy with an empty DrawableMagics "
                           + "list contributes nothing to it.",
                    Suggestion = "Give every enemy at least one drawable magic."
                });
            }

            if (variety.CatalogMagicCount > 0 && variety.DrawCoverage < 0.5f)
            {
                report.Issues.Add(new BalanceIssue(BalanceSeverity.Warning, BalanceCategory.Variety, variety.Scope,
                    $"Only {variety.DistinctDrawableMagic} of {variety.CatalogMagicCount} magics are drawable anywhere")
                {
                    Detail = $"{variety.DrawCoverage:P0} coverage — the rest of the catalog is unreachable in play.",
                    Suggestion = "Spread the catalog across enemy Draw lists."
                });
            }
        }

        // ------------------------------------------------------------------ progression / unlocks

        /// <summary>
        /// The supply side of the elemental layer. Resistances and combos are both authored content that
        /// only becomes live if the Draw tables hand the player the pieces — so a combo whose required
        /// tag lives on undrawable magic, or a level that resists an element the player cannot yet deal,
        /// is content that can never fire. Neither is visible in any inspector.
        /// </summary>
        private static void EvaluateProgression(BalanceReport report, BalanceRulesSO rules)
        {
            var map = report.Progression;
            if (map == null)
            {
                return;
            }

            if (map.RunOrderIsImplicit)
            {
                report.Issues.Add(new BalanceIssue(BalanceSeverity.Info, BalanceCategory.Progression, "Run order",
                    "No run declares a SequenceIndex, so the unlock order is guessed from asset names")
                {
                    Detail = "Every RunDefinitionSO has SequenceIndex 0. The progression view has to fall back "
                           + "to alphabetical order, which may not be the intended play order.",
                    Suggestion = "Set SequenceIndex on each run (0 = first)."
                });
            }

            foreach (var combo in map.Combos)
            {
                if (combo.TagsWithNoMagic.Count > 0)
                {
                    report.Issues.Add(new BalanceIssue(BalanceSeverity.Critical, BalanceCategory.Progression, combo.Name,
                        $"Combo '{combo.Name}' can never fire — no magic carries {string.Join(", ", combo.TagsWithNoMagic)}")
                    {
                        Asset = combo.Combo,
                        Detail = $"RequiredTags are {string.Join(" + ", combo.RequiredTags)}, but no MagicSO in the "
                               + $"catalog has the {string.Join(", ", combo.TagsWithNoMagic)} tag, so the combo is "
                               + "unreachable by construction.",
                        Suggestion = "Add the tag to a magic, or change the combo's RequiredTags."
                    });
                }
                else if (combo.TagsNotDrawable.Count > 0)
                {
                    report.Issues.Add(new BalanceIssue(BalanceSeverity.Warning, BalanceCategory.Progression, combo.Name,
                        $"Combo '{combo.Name}' is unreachable — {string.Join(", ", combo.TagsNotDrawable)} only exists on undrawable magic")
                    {
                        Asset = combo.Combo,
                        Detail = $"Requires {string.Join(" + ", combo.RequiredTags)}. "
                               + $"{string.Join("; ", combo.EnablingMagic)}. Draw is the only route to new magic, "
                               + "so a tag that no enemy offers cannot be brought to a fight.",
                        Suggestion = "Add the carrying magic to an enemy's DrawableMagics list."
                    });
                }
            }

            // Name the unreachable magic outright — the variety tab only reports the count.
            var unreachable = new List<string>();
            foreach (var availability in map.Magic)
            {
                if (!availability.IsReachable)
                {
                    unreachable.Add(availability.DisplayName);
                }
            }

            if (unreachable.Count > 0)
            {
                report.Issues.Add(new BalanceIssue(BalanceSeverity.Warning, BalanceCategory.Progression, "Draw tables",
                    $"{unreachable.Count} magic(s) cannot be drawn anywhere")
                {
                    Detail = $"Unreachable: {string.Join(", ", unreachable)}. "
                           + $"Draw coverage is {map.ReachableMagicCount}/{map.CatalogMagicCount}.",
                    Suggestion = "Add each to an enemy's DrawableMagics, or accept it as Forge/merchant-only content."
                });
            }

            foreach (var run in map.Runs)
            {
                foreach (var level in run.Levels)
                {
                    // Front-loading: if one level hands over most of the catalog, the rest of the run
                    // has nothing left to reveal and Draw stops being a reason to fight anything.
                    if (map.CatalogMagicCount > 0 && level.NewlyDrawable.Count > 0)
                    {
                        float share = (float)level.NewlyDrawable.Count / map.CatalogMagicCount;
                        if (share > rules.MaxUnlockSharePerLevel)
                        {
                            report.Issues.Add(new BalanceIssue(BalanceSeverity.Warning, BalanceCategory.Progression,
                                $"{run.Name} / {level.Reference}",
                                $"{level.Reference} unlocks {share:P0} of the magic catalog at once")
                            {
                                Asset = run.Run,
                                Detail = $"{level.NewlyDrawable.Count} of {map.CatalogMagicCount} magics first "
                                       + "become drawable here, because every enemy that offers them appears in "
                                       + $"this level's room pool. Ceiling is {rules.MaxUnlockSharePerLevel:P0}.",
                                Suggestion = "Hold some enemies back for later levels so Draw keeps paying out "
                                       + "across the run."
                            });
                        }
                    }

                    // Defensive mirror: elemental threat the hero side has no way to resist at all.
                    if (level.UndefendableIncoming.Count > 0)
                    {
                        report.Issues.Add(new BalanceIssue(BalanceSeverity.Warning, BalanceCategory.Progression,
                            $"{run.Name} / {level.Reference}",
                            $"{level.Reference} deals {string.Join(", ", level.UndefendableIncoming)} damage that nothing can resist")
                        {
                            Asset = run.Run,
                            Detail = "No gear grants resistance to it and no magic buffs it, so the element is pure "
                                   + "downside for the player. Resistible types today: "
                                   + $"{(map.DefendableTypes.Count > 0 ? string.Join(", ", map.DefendableTypes) : "none")}.",
                            Suggestion = "Add the resistance to a piece of gear, or give those enemies a different "
                                   + "attack element."
                        });
                    }

                    if (!level.HasCombat || level.ResistingWeight + level.WeakWeight <= 0f)
                    {
                        continue;
                    }

                    if (!level.ElementChoiceMatters)
                    {
                        report.Issues.Add(new BalanceIssue(BalanceSeverity.Warning, BalanceCategory.Progression,
                            $"{run.Name} / {level.Reference}",
                            $"{level.Reference} resists elements the player cannot bring yet")
                        {
                            Asset = run.Run,
                            Detail = "Its enemies carry resistances, but none are in an element drawable by this "
                                   + $"point in the run order. Available so far: "
                                   + $"{(level.ElementsAvailable.Count > 0 ? string.Join(", ", level.ElementsAvailable) : "none")}.",
                            Suggestion = "Either move the enabling magic earlier in the Draw tables, or retarget "
                                   + "the resistances to an element the player already has."
                        });
                    }
                }
            }
        }

        // ------------------------------------------------------------------ economy

        private static void EvaluateEconomy(BalanceReport report, BalanceRulesSO rules)
        {
            var save = report.Save;
            float clearsToFirst = save != null
                ? save.ClearsToFirstUpgrade
                : (float)MetaProgressManager.MagicUpgradeCostForNextLevel(0) / MetaProgressManager.EssencePerLevelCleared;
            float clearsToMax = save != null
                ? save.ClearsToMaxOneMagic
                : (float)SaveAudit.TotalEssenceToMaxOneMagic() / MetaProgressManager.EssencePerLevelCleared;

            if (clearsToFirst > rules.TargetClearsToFirstUpgrade * 1.5f)
            {
                report.Issues.Add(new BalanceIssue(BalanceSeverity.Warning, BalanceCategory.Economy, "Essence",
                    $"First magic upgrade takes {clearsToFirst:0.0} level-clears")
                {
                    Detail = $"{MetaProgressManager.MagicUpgradeCostForNextLevel(0)} Essence at "
                           + $"{MetaProgressManager.EssencePerLevelCleared} per clear; the target is "
                           + $"{rules.TargetClearsToFirstUpgrade}.",
                    Suggestion = "Raise EssencePerLevelCleared or lower the base upgrade cost — the first upgrade "
                           + "is what teaches the player the meta-loop exists."
                });
            }

            if (clearsToMax > rules.MaxClearsToMaxOneMagic)
            {
                report.Issues.Add(new BalanceIssue(BalanceSeverity.Info, BalanceCategory.Economy, "Essence",
                    $"Maxing one magic takes {clearsToMax:0} level-clears")
                {
                    Detail = $"{SaveAudit.TotalEssenceToMaxOneMagic()} Essence total at "
                           + $"{MetaProgressManager.EssencePerLevelCleared} per clear, and that is for a single "
                           + $"magic out of the whole catalog. Ceiling in the rules is {rules.MaxClearsToMaxOneMagic}.",
                    Suggestion = "Intentional grind or not, this is the shape of the Essence economy — worth a "
                           + "deliberate decision rather than an accident of two constants."
                });
            }
        }

        // ------------------------------------------------------------------ simulation

        private static void EvaluateSimulations(BalanceReport report, BalanceRulesSO rules)
        {
            foreach (var simulation in report.Simulations)
            {
                var best = simulation.Best;
                if (best == null || best.Trials == 0)
                {
                    continue;
                }

                if (best.WinRate <= 0f)
                {
                    report.Issues.Add(new BalanceIssue(BalanceSeverity.Critical, BalanceCategory.Simulation, simulation.Label,
                        "Party never wins this encounter")
                    {
                        Asset = simulation.Asset,
                        Detail = $"0 wins in {best.Trials} simulated battles under the best policy "
                               + $"({best.Policy}); average {best.AverageTurns:0.0} turns, "
                               + $"{best.AverageHeroDeaths:0.0} hero deaths per attempt.",
                        Suggestion = "This encounter cannot be played around — it has to be retuned."
                    });
                }
                else if (best.WinRate < rules.MinEncounterWinRate)
                {
                    report.Issues.Add(new BalanceIssue(BalanceSeverity.Warning, BalanceCategory.Simulation, simulation.Label,
                        $"Win rate {best.WinRate:P0} under best play")
                    {
                        Asset = simulation.Asset,
                        Detail = $"{best.Wins}/{best.Trials} wins with policy {best.Policy}; the target floor is "
                               + $"{rules.MinEncounterWinRate:P0}. Average {best.AverageHeroDeaths:0.0} hero deaths.",
                        Suggestion = "Soften the encounter, or accept it as an intentional wall."
                    });
                }
                else if (best.WinRate >= 1f && best.AverageEndHealthFraction >= rules.TrivialEndHealthFraction)
                {
                    report.Issues.Add(new BalanceIssue(BalanceSeverity.Info, BalanceCategory.Simulation, simulation.Label,
                        "Encounter is a formality")
                    {
                        Asset = simulation.Asset,
                        Detail = $"Always won, ending at {best.AverageEndHealthFraction:P0} health. It consumes "
                               + "turns without consuming resources.",
                        Suggestion = "Raise the threat, or use this enemy only as filler in mixed groups."
                    });
                }

                if (simulation.DepthGap <= rules.DominantStrategyTolerance && best.WinRate > 0f)
                {
                    var attackOnly = simulation.AttackOnly;
                    report.Issues.Add(new BalanceIssue(BalanceSeverity.Warning, BalanceCategory.Simulation, simulation.Label,
                        "Attack-spam plays this fight as well as thinking does")
                    {
                        Asset = simulation.Asset,
                        Detail = $"Attack-only scores {(attackOnly != null ? attackOnly.Score : 0f):0.000} against "
                               + $"{best.Score:0.000} for the best policy — a gap of {simulation.DepthGap:0.000}, "
                               + $"inside the {rules.DominantStrategyTolerance:0.000} tolerance. Magic, items and "
                               + "targeting make no measurable difference here.",
                        Suggestion = "Give the encounter something attack-spam cannot answer: a resistance that "
                               + "punishes the wrong element, a healer that must be focused, or a charge that has "
                               + "to be pre-empted."
                    });
                }
            }
        }

        // ------------------------------------------------------------------ save

        private static void EvaluateSave(BalanceReport report, BalanceRulesSO rules, BalanceInput input)
        {
            var save = report.Save;
            if (save == null || !save.HasPartySave)
            {
                return;
            }

            foreach (var hero in save.Heroes)
            {
                if (hero.Definition == null)
                {
                    report.Issues.Add(new BalanceIssue(BalanceSeverity.Warning, BalanceCategory.Save, hero.HeroKey,
                        $"Saved hero '{hero.HeroKey}' has no matching HeroSO")
                    {
                        Detail = "The save references a hero key no asset in the project provides, so the party "
                               + "cannot be rebuilt from it.",
                        Suggestion = "Restore the hero asset, or clear the save."
                    });
                    continue;
                }

                if (hero.GridComplete && hero.Xp > 0)
                {
                    report.Issues.Add(new BalanceIssue(BalanceSeverity.Warning, BalanceCategory.Save, hero.Name(),
                        $"{hero.Name()}'s grid is fully activated — {hero.Xp} XP is overflowing")
                    {
                        Asset = hero.Definition,
                        Detail = "Every node is bought, so every kill from here on is wasted XP.",
                        Suggestion = "Extend the grid."
                    });
                }
                else if (hero.CanAffordNext)
                {
                    report.Issues.Add(new BalanceIssue(BalanceSeverity.Info, BalanceCategory.Save, hero.Name(),
                        $"{hero.Name()} has {hero.Xp} XP banked with an affordable node unspent")
                    {
                        Asset = hero.Definition,
                        Detail = $"The cheapest node on their frontier costs {hero.CheapestNextCost}.",
                        Suggestion = "Spend it on the sphere grid at the hub. (A nudge, not a fault.)"
                    });
                }
            }

            if (save.LegacyBonusSlots > 0)
            {
                report.Issues.Add(new BalanceIssue(BalanceSeverity.Info, BalanceCategory.Save, "Meta",
                    $"Save has {save.LegacyBonusSlots} legacy bonus magic slot(s) awaiting Essence refund")
                {
                    Detail = "The Essence-bought global slot upgrade was retired for per-hero MagicSlot "
                           + "grid nodes; the game refunds the Essence on its next launch.",
                    Suggestion = "Launch the game once, or ignore — the refund is automatic."
                });
            }

            // The question the designed baseline cannot answer: would this save survive the run?
            if (save.Party != null && save.Party.Size > 0)
            {
                foreach (var run in report.Runs)
                {
                    if (run.Run == null)
                    {
                        continue;
                    }

                    var realCurve = RunCurve.Build(run.Run, save.Party, rules);
                    for (int i = 0; i < realCurve.Levels.Count; i++)
                    {
                        var level = realCurve.Levels[i];
                        if (level.ExpectedCombatRooms <= 0f)
                        {
                            continue;
                        }

                        if (level.AttritionMargin < 0f)
                        {
                            report.Issues.Add(new BalanceIssue(BalanceSeverity.Critical, BalanceCategory.Save,
                                $"{run.Name} / {level.Reference}",
                                $"The current save cannot clear {level.Reference}")
                            {
                                Asset = run.Run,
                                Detail = $"Real party (HP {save.Party.HealthPool}, potions {save.Party.PotionCount}) "
                                       + $"faces an expected cost of {level.ExpectedHealthCost:0} HP — "
                                       + $"{level.AttritionLoad:0.00}x its whole sustain pool. "
                                       + $"Save is currently at level index {save.CurrentLevelIndex}.",
                                Suggestion = "This is where the run ends for this player. Either the level or the "
                                       + "party's power budget has to move."
                            });
                            break;
                        }
                    }
                }
            }

            // Is the player's wallet where the reward model expects it to be?
            int expectedGold = MetaProgressManager.GoldPerLevelCleared * Mathf.Max(0, save.CurrentLevelIndex);
            if (save.CurrentLevelIndex > 0 && expectedGold > 0)
            {
                float ratio = (float)save.Gold / expectedGold;
                if (ratio > 3f)
                {
                    report.Issues.Add(new BalanceIssue(BalanceSeverity.Info, BalanceCategory.Economy, "Save wallet",
                        $"Save holds {ratio:0.0}x the Gold the clear model predicts")
                    {
                        Detail = $"{save.Gold} Gold against {expectedGold} expected from {save.CurrentLevelIndex} "
                               + "level-clear(s). Either enemy GoldReward is generous or there is nothing to spend it on.",
                        Suggestion = "Check the Merchant's prices against this figure."
                    });
                }
            }
        }
    }

    /// <summary>Display-name helper so save rows read the same as baseline rows.</summary>
    internal static class SavedHeroExtensions
    {
        public static string Name(this SavedHero hero)
        {
            if (hero.Definition != null)
            {
                return hero.Definition.DisplayName;
            }
            return hero.HeroKey;
        }
    }
}
