using System;
using System.Collections.Generic;
using Assets.Scripts.Cards;
using Assets.Scripts.Dungeon;
using Assets.Scripts.Enemies;
using Assets.Scripts.Heroes;
using Assets.Scripts.Items;
using Assets.Scripts.Progression;
using Assets.Scripts.Resources;
using Assets.Scripts.Rooms;
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

        /// <summary>
        /// Every hero the player can end up fielding, in <c>PartyRosterSO.Heroes</c> order — the
        /// starting lineup plus everyone still to be unlocked.
        ///
        /// <para>This is not the same list as <see cref="AllHeroes"/> conceptually, even where the
        /// project makes them equal: recruiting is a <b>gold purchase</b>, which makes party width an
        /// axis the player invests along exactly like a bought party slot. The frontier sweep needs
        /// it for that reason — without it the widest measurable party is however many heroes the
        /// campaign hands over for free, and the endgame's "buy a fourth body" route is invisible.
        /// Nothing else should use it: danger and attrition are still judged against the party a run
        /// actually starts with.</para>
        /// </summary>
        public List<HeroSO> Roster = new List<HeroSO>();
        public List<EnemySO> Enemies = new List<EnemySO>();
        public List<RunDefinitionSO> Runs = new List<RunDefinitionSO>();

        /// <summary>
        /// The campaign graph, when the project has one. It supplies the order runs are actually
        /// played in and, more importantly, which runs are already behind the player when a given
        /// run starts - without it every run is measured against a fresh starting party.
        /// </summary>
        public CampaignSO Campaign;
        public List<MagicSO> Magic = new List<MagicSO>();
        public List<MagicComboSO> Combos = new List<MagicComboSO>();
        public List<ItemSO> Items = new List<ItemSO>();

        /// <summary>
        /// Every room-event asset in the project. The run curves reach events through their rooms'
        /// <c>PossibleEvents</c>, so this list is only needed for the checks that are about the asset
        /// itself - chiefly whether any room offers it at all.
        /// </summary>
        public List<Rooms.Events.RoomEventSO> RoomEvents = new List<Rooms.Events.RoomEventSO>();

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
            // Run curves come first now: they know the campaign order, so they are the authority on
            // which party meets which enemy.
            BuildRunCurves(input, rules, report);

            var partyByEnemy = report.Runs.Count > 0
                ? BuildEncounterPartiesFromCurves(report)
                : BuildFirstEncounterParties(input, rules, report.Party);
            report.PartyByEnemy = partyByEnemy;

            BuildEnemyMetrics(input, rules, report, partyByEnemy);

            report.Variety = VarietyReport.Build(ProjectWideEnemySet(input.Enemies), input.Magic, rules);
            report.Progression = ProgressionMap.Build(
                report.Runs, input.Magic, input.Combos, input.HeroesToAudit, input.Items);
            report.Materials = BuildMaterialYield(report);

            if (input.RunSimulation)
            {
                RunSimulations(input, rules, report);
                RunFloorSimulations(input, rules, report);
                if (rules.MeasureInvestmentFrontiers)
                {
                    RunFrontierSweeps(input, rules, report);
                }
            }

            EvaluateParty(report, rules, input);
            EvaluateEnemies(report, rules);
            EvaluateRuns(report, rules, input);
            EvaluateEvents(report, rules, input);
            EvaluateVariety(report, rules);
            EvaluateProgression(report, rules);
            EvaluateEconomy(report, rules);
            EvaluateMaterials(report, input);
            EvaluateSimulations(report, rules);
            EvaluateFloorSimulations(report, rules);
            EvaluateFrontiers(report, rules);
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
        /// <summary>
        /// Models every run, walking the campaign shallowest-first so a run inherits the party its
        /// prerequisites left behind. Where a run has several prerequisites the player can only have
        /// arrived via one of them, so the *weakest* incoming state is used - the run has to be
        /// clearable by whoever reaches it first, not only by a completionist.
        ///
        /// <para>Falls back to measuring every run against the starting party when no campaign is
        /// authored, which is what the analyzer did before the graph existed.</para>
        /// </summary>
        private static void BuildRunCurves(BalanceInput input, BalanceRulesSO rules, BalanceReport report)
        {
            if (input.Runs == null)
            {
                return;
            }

            if (input.Campaign == null)
            {
                foreach (var run in input.Runs)
                {
                    if (run != null)
                    {
                        report.Runs.Add(RunCurve.Build(run, report.Party, rules));
                    }
                }
                return;
            }

            var byKey = new Dictionary<string, RunCurve>();
            var remaining = new List<RunDefinitionSO>(input.Runs);

            foreach (var node in CampaignOps.GetNodesInPlayOrder(input.Campaign))
            {
                var run = node.Run;
                if (run == null || !remaining.Contains(run))
                {
                    continue;
                }
                remaining.Remove(run);

                List<HeroSO> seedRoster = null;
                Dictionary<HeroSO, int> seedXp = null;

                RunCurve weakest = null;
                foreach (var prerequisite in node.Requires)
                {
                    if (prerequisite == null)
                    {
                        continue;
                    }
                    if (byKey.TryGetValue(CampaignOps.RunKeyOf(prerequisite), out var prior) &&
                        (weakest == null || TotalBankedXp(prior) < TotalBankedXp(weakest)))
                    {
                        weakest = prior;
                    }
                }
                if (weakest != null)
                {
                    seedRoster = weakest.EndRoster;
                    seedXp = weakest.EndLifetimeXp;
                }

                var curve = RunCurve.Build(run, report.Party, rules, seedRoster, seedXp);
                report.Runs.Add(curve);
                byKey[CampaignOps.RunKeyOf(run)] = curve;
            }

            // Runs that exist as assets but are not on the map still get measured, as a fresh start -
            // CampaignAssetTests is what actually objects to them being unreachable.
            foreach (var run in remaining)
            {
                if (run != null)
                {
                    report.Runs.Add(RunCurve.Build(run, report.Party, rules));
                }
            }
        }

        private static int TotalBankedXp(RunCurve curve)
        {
            int total = 0;
            foreach (var pair in curve.EndLifetimeXp)
            {
                total += pair.Value;
            }
            return total;
        }

        /// <summary>
        /// Which party each enemy is first met by, read straight off the modelled run curves so the
        /// answer cannot drift from the curve the rest of the report is drawn from. Every level
        /// already records the party it was measured against; this just indexes it by enemy, in play
        /// order, first writer wins.
        /// </summary>
        private static Dictionary<EnemySO, PartyBaseline> BuildEncounterPartiesFromCurves(BalanceReport report)
        {
            var result = new Dictionary<EnemySO, PartyBaseline>();
            foreach (var curve in report.Runs)
            {
                foreach (var level in curve.Levels)
                {
                    if (level.Party == null || level.Entry == null)
                    {
                        continue;
                    }
                    foreach (var enemy in EnemiesInLevel(level.Entry))
                    {
                        if (enemy != null && !result.ContainsKey(enemy))
                        {
                            result[enemy] = level.Party;
                        }
                    }
                }
            }
            return result;
        }

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
                        // Same rule as the run curve: the starting baseline's loadout travels with
                        // the party, since nothing can change it mid-run.
                        party = PartyBaseline.Build(roster, rules.ReferenceHeroXp, starting.GearLookup,
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
        /// <summary>
        /// One measurement per <b>placement</b> — per (enemy, level) — rather than one per asset.
        ///
        /// <para>An <c>EnemySO</c> is a template and the level it appears in owns its numbers
        /// (<see cref="LevelEnemyTuning"/>), so "is the Floating Eye in band" has no answer on its
        /// own: it is in all ten authored levels, against parties from 40 HP and no spent XP to 64 HP
        /// and 176. A per-asset row could only ever be right about one of them. This is also why the
        /// <c>EnemySO</c> inspector no longer carries a balance footer — the numbers it would show
        /// belong to a level, not to the asset.</para>
        ///
        /// <para>An enemy no run places still gets a single template-only row, so authoring checks
        /// (no XP, no spell list) keep working on content that is not wired up yet.</para>
        /// </summary>
        private static void BuildEnemyMetrics(
            BalanceInput input, BalanceRulesSO rules, BalanceReport report,
            Dictionary<EnemySO, PartyBaseline> partyByEnemy)
        {
            var placed = new List<EnemySO>();

            foreach (var run in report.Runs)
            {
                foreach (var level in run.Levels)
                {
                    string context = $"{run.Name} / {level.Reference}";

                    foreach (var enemy in EnemiesInLevelCurve(level))
                    {
                        if (!placed.Contains(enemy))
                        {
                            placed.Add(enemy);
                        }

                        var metrics = EnemyMetrics.Compute(
                            enemy, level.Party, rules, report.Party, level.Tuning, context);
                        metrics.Run = run.Run;
                        metrics.LevelIndex = level.Index;
                        metrics.LevelLabel = level.Name;
                        report.Enemies.Add(metrics);
                    }
                }
            }

            foreach (var enemy in input.Enemies)
            {
                if (enemy == null || placed.Contains(enemy))
                {
                    continue;
                }

                PartyBaseline against;
                if (!partyByEnemy.TryGetValue(enemy, out against) || against == null)
                {
                    against = report.Party;
                }

                // No tuning: nothing places it, so the template's own numbers are all there is.
                var unplaced = EnemyMetrics.Compute(enemy, against, rules, report.Party);
                unplaced.LevelLabel = "(unplaced)";
                report.Enemies.Add(unplaced);
            }

            report.Enemies.Sort((a, b) =>
            {
                int byName = string.Compare(a.Name, b.Name, StringComparison.Ordinal);
                return byName != 0 ? byName : string.Compare(a.Context, b.Context, StringComparison.Ordinal);
            });
        }

        /// <summary>Distinct enemies a modelled level can present, boss included.</summary>
        private static IEnumerable<EnemySO> EnemiesInLevelCurve(LevelCurve level)
        {
            var seen = new List<EnemySO>();
            foreach (var room in level.Rooms)
            {
                foreach (var member in room.Expected.Members)
                {
                    if (member.Definition != null && !seen.Contains(member.Definition))
                    {
                        seen.Add(member.Definition);
                    }
                }
                foreach (var member in room.WorstCase.Members)
                {
                    if (member.Definition != null && !seen.Contains(member.Definition))
                    {
                        seen.Add(member.Definition);
                    }
                }
            }
            return seen;
        }

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

            foreach (var add in entry.EnumerateBossAdds())
            {
                yield return add;
            }
        }

        private static PartyBaseline BuildReferenceParty(BalanceInput input, BalanceRulesSO rules, SaveAudit save)
        {
            // Two ways gear can reach the reference party, and they are not interchangeable.
            // A saved loadout is what one player on one machine actually equipped - right for a save
            // audit, useless for a published number, which is why the regression suite never turns
            // it on. A gold budget is derived from the item catalog by GearLoadout and is therefore
            // reproducible anywhere, which is what makes gear something the model can *state*.
            Func<HeroSO, List<ItemSO>> gearLookup = null;
            string gearLabel = "no gear";
            GearSpend designedGear = null;

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
                gearLabel = "saved gear";
            }
            else if (rules.ReferencePartyGoldBudget > 0)
            {
                // The reference party is built once for the whole campaign, so the mix its gear is
                // ranked against is the campaign's, not a floor's. Per-floor wards are the
                // frontier's business; this party has to be dressed for everything at once.
                designedGear = GearLoadout.Spend(
                    input.Heroes,
                    rules.ReferenceHeroXp,
                    input.Items,
                    rules.ReferencePartyGoldBudget,
                    rules.WeightFor,
                    IncomingDamageMix.FromEnemies(input.Enemies));
                gearLookup = designedGear.Lookup;
                gearLabel = $"{designedGear.GoldSpent}g of gear";
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

            party.SourceLabel = $"Designed baseline ({rules.ReferenceHeroXp} XP spent, {gearLabel})";
            party.DesignedGear = designedGear;

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
                var unit = SimUnit.FromEnemy(metrics.Definition, metrics.Tuning);
                if (unit == null)
                {
                    continue;
                }

                var simReport = new EncounterSimReport
                {
                    Label = $"{metrics.Reference} (solo)",
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

                AssignMagicLoadout(against, report.Save, input.Magic);
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
                    AssignMagicLoadout(levelParty, report.Save, input.Magic);
                    simReport.Outcomes = EncounterSimulator.RunAllPolicies(levelParty, units, settings);
                    report.Simulations.Add(simReport);
                }
            }
        }

        /// <summary>
        /// Gives every simulated hero the kit they would actually walk in with: the spells their
        /// activated <c>MagicKnown</c> sphere-grid nodes teach them, capped at their slot count.
        /// Without this the simulated party has empty slots and the attack-spam comparison only
        /// measures potion use rather than whether magic changes the outcome.
        ///
        /// <para><b>This used to read the enemies.</b> Until Draw was removed (2026-09-04) a
        /// simulated hero was handed whatever the encounter offered, because that was how the loop
        /// worked - you fought with what the fight let you take. Magic is now bought on the grid, so
        /// the encounter has no say in it and the loadout is a function of investment alone. That is
        /// the point: a magic kit is now something the frontier has to <i>pay for</i>, not something
        /// the room hands over.</para>
        ///
        /// <para>Charges come from the granting node (the run's whole allowance of that spell) and
        /// slot counts from <see cref="EquippedMagicState.DefaultSlotCount"/> plus each hero's
        /// activated <c>MagicSlot</c> nodes. The auto-fill order is
        /// <c>MagicLoadoutOps.Resolve</c>'s, so the model and the game agree on what an
        /// unconfigured hero carries. A player who hand-picks a better loadout does strictly better
        /// than this, which is the right direction for the model to be wrong in.</para>
        /// </summary>
        private static void AssignMagicLoadout(
            PartyBaseline party, SaveAudit save, IList<MagicSO> allMagic)
        {
            if (party == null)
            {
                return;
            }

            foreach (var hero in party.Heroes)
            {
                if (hero.Unit == null)
                {
                    continue;
                }

                var grid = hero.Definition != null ? hero.Definition.SphereGrid : null;
                int slotCount = EquippedMagicState.DefaultSlotCount
                    + SphereGridOps.SlotBonusForNodes(grid, hero.ActivatedNodes);

                hero.Unit.MagicSlots.Clear();

                var known = SphereGridOps.KnownMagicForNodes(grid, hero.ActivatedNodes);
                foreach (var key in MagicLoadoutOps.Resolve(known, null, slotCount))
                {
                    var magic = ResolveMagicByKey(allMagic, key);
                    if (magic == null)
                    {
                        continue;
                    }

                    int charges = Mathf.Max(1, MagicLoadoutOps.ChargesFor(known, key));
                    hero.Unit.MagicSlots.Add(new SimMagicSlot
                    {
                        Magic = magic,
                        Charges = charges,
                        MaxCharges = charges,
                        UpgradeLevel = UpgradeLevelFor(magic, save)
                    });
                }
            }
        }

        /// <summary>
        /// A magic asset by <c>MagicSO.Key</c>, resolved from the analyzer's own input list. The
        /// runtime <c>MagicCatalog</c> is a scene singleton the analyzer cannot reach, and reaching for
        /// <c>AssetDatabase</c> here would put editor-only code in a runtime assembly.
        /// </summary>
        private static MagicSO ResolveMagicByKey(IList<MagicSO> allMagic, string key)
        {
            if (allMagic == null || string.IsNullOrEmpty(key))
            {
                return null;
            }

            foreach (var magic in allMagic)
            {
                if (magic != null && magic.Key == key)
                {
                    return magic;
                }
            }
            return null;
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
            // A behaviour is authored per *asset* while stats are authored per *level*, so the
            // findings about it that are not level-specific must be reported once, not once per
            // placement - otherwise one un-authored enemy in ten levels is ten identical findings.
            var castChanceReported = new HashSet<EnemySO>();

            foreach (var metrics in report.Enemies)
            {
                float ceiling = metrics.DangerCeiling(rules);

                if (metrics.SoloDangerIndex >= 1f)
                {
                    report.Issues.Add(new BalanceIssue(BalanceSeverity.Critical, BalanceCategory.Enemy, metrics.Reference,
                        $"{metrics.Reference} beats the party on paper (danger {metrics.SoloDangerIndex:0.00})")
                    {
                        Asset = metrics.Definition,
                        Detail = $"The party needs {metrics.PartyTurnsToKill:0.0} turns to kill it; it needs fewer "
                               + "to wipe the party. Danger at or above 1.00 means the encounter is lost before "
                               + "any decisions are made.",
                        Suggestion = SuggestDifficulty(metrics, metrics.DangerCeiling(rules))
                    });
                }
                else if (metrics.SoloDangerIndex > ceiling)
                {
                    report.Issues.Add(new BalanceIssue(BalanceSeverity.Warning, BalanceCategory.Enemy, metrics.Reference,
                        $"{metrics.Reference} is above its danger band ({metrics.SoloDangerIndex:0.00} > {ceiling:0.00})")
                    {
                        Asset = metrics.Definition,
                        Detail = $"{(metrics.IsBoss ? "Boss" : "Trash")} target is {ceiling:0.00}. "
                               + $"Effective damage per turn {metrics.EffectiveDamagePerTurn:0.0}, "
                               + $"party turns to kill {metrics.PartyTurnsToKill:0.0}.",
                        Suggestion = SuggestDifficulty(metrics, metrics.DangerCeiling(rules))
                    });
                }
                else if (metrics.SoloDangerIndex < rules.MinMeaningfulDanger
                         && metrics.SoloDangerIndex > 0f
                         && metrics.Archetype != EnemyArchetype.Healer)
                {
                    // Healers are exempt: the danger index measures a damage race, and a Healer's cost to
                    // the player is the turns it adds by undoing damage, not the damage it deals. Judging
                    // one on offence would always read as "no threat".
                    report.Issues.Add(new BalanceIssue(BalanceSeverity.Warning, BalanceCategory.Enemy, metrics.Reference,
                        $"{metrics.Reference} is no threat at all (danger {metrics.SoloDangerIndex:0.000})")
                    {
                        Asset = metrics.Definition,
                        Detail = $"It deals {metrics.EffectiveDamagePerTurn:0.0} per turn and dies in "
                               + $"{metrics.PartyTurnsToKill:0.0} party turns. It costs the player time, not resources.",
                        Suggestion = SuggestDifficulty(metrics, rules.MinMeaningfulDanger * 1.5f)
                    });
                }

                if (metrics.FewestHitsToKillAHero <= 1 && metrics.FewestHitsToKillAHero > 0)
                {
                    report.Issues.Add(new BalanceIssue(BalanceSeverity.Critical, BalanceCategory.Enemy, metrics.Reference,
                        $"{metrics.Reference} one-shots {metrics.FastestKillTarget}")
                    {
                        Asset = metrics.Definition,
                        Detail = $"Attack {metrics.Stats[StatType.Strength]} lands "
                               + $"{FindPerHero(metrics, metrics.FastestKillTarget):0.0} average damage on a hero who "
                               + "cannot survive one hit.",
                        Suggestion = "Lower Attack, or raise hero max HP — the HP:damage scale is the root cause."
                    });
                }

                float ttkCeiling = metrics.TimeToKillCeiling(rules);
                if (float.IsInfinity(metrics.PartyTurnsToKill))
                {
                    // The party cannot finish it at all: it heals or shields back at least everything
                    // the party lands. Worth its own finding because the danger index cannot say so -
                    // that measures a damage race, so an enemy the party also cannot be killed *by*
                    // reads as 0.00, the safest-looking number there is for the worst encounter in the
                    // game.
                    report.Issues.Add(new BalanceIssue(BalanceSeverity.Critical, BalanceCategory.Enemy, metrics.Reference,
                        $"{metrics.Reference} cannot be killed by the party at all")
                    {
                        Asset = metrics.Definition,
                        Detail = $"It restores or protects at least as much per tick as the party deals. "
                               + $"Healing {metrics.ExpectedHealingPerTurn:0.0}/turn and party output "
                               + $"x{metrics.PartyOutputMultiplier:0.00}. The fight is a stalemate, not a loss, "
                               + "so the danger index reads 0.00 and every other check passes.",
                        Suggestion = "Lower its Heal power, cut how often it heals, or give the party a way "
                                   + "to out-damage the sustain."
                    });
                }
                else if (metrics.PartyTurnsToKill > ttkCeiling)
                {
                    report.Issues.Add(new BalanceIssue(BalanceSeverity.Warning, BalanceCategory.Enemy, metrics.Reference,
                        $"{metrics.Reference} takes {metrics.PartyTurnsToKill:0.0} party turns to kill")
                    {
                        Asset = metrics.Definition,
                        Detail = $"Target ceiling is {ttkCeiling:0.0} turns. Long fights without added pressure read as a slog.",
                        Suggestion = $"Lower this level's Difficulty, or override Health here toward "
                               + $"{Mathf.RoundToInt(metrics.Stats[StatType.MaxHealth] * ttkCeiling / Mathf.Max(0.01f, metrics.PartyTurnsToKill))}."
                    });
                }
                else if (metrics.PartyTurnsToKill < rules.MinEnemyTimeToKill && metrics.PartyTurnsToKill > 0f)
                {
                    report.Issues.Add(new BalanceIssue(BalanceSeverity.Info, BalanceCategory.Enemy, metrics.Reference,
                        $"{metrics.Reference} dies in {metrics.PartyTurnsToKill:0.0} party turns")
                    {
                        Asset = metrics.Definition,
                        Detail = $"Below the {rules.MinEnemyTimeToKill:0.0}-turn floor: it never gets to act meaningfully.",
                        Suggestion = "Raise this level's Difficulty, or use the enemy only in groups."
                    });
                }

                if (metrics.ActionShareVsParty >= 1.5f)
                {
                    report.Issues.Add(new BalanceIssue(BalanceSeverity.Info, BalanceCategory.Enemy, metrics.Reference,
                        $"{metrics.Reference} acts {metrics.ActionShareVsParty:0.0}x as often as a hero")
                    {
                        Asset = metrics.Definition,
                        Detail = $"Agility {metrics.Stats[StatType.Agility]} against the party average. Its real threat is "
                               + "that multiple of what its Attack suggests, and nothing in the inspector shows it.",
                        Suggestion = "Treat Agility as a damage multiplier when tuning this enemy."
                    });
                }

                if (metrics.XpReward <= 0)
                {
                    report.Issues.Add(new BalanceIssue(BalanceSeverity.Info, BalanceCategory.Economy, metrics.Name,
                        $"{metrics.Name} awards no XP")
                    {
                        Asset = metrics.Definition,
                        Detail = "XpReward is 0, so killing it cannot advance hero levels.",
                        Suggestion = "Set an XpReward proportional to its danger."
                    });
                }

                EvaluateEnemyCasting(metrics, report, rules, castChanceReported);
            }

            EvaluateRewardSpread(report, rules);
        }

        /// <summary>
        /// Whether this placement's <c>CastMagic</c> actions are doing anything useful. Casting is
        /// authored on the enemy's behaviour while its stats come from the level, so it is easy to
        /// leave in a state where the spells exist and never fire, or fire so often the rest of the
        /// repertoire stops mattering.
        /// </summary>
        private static void EvaluateEnemyCasting(
            EnemyMetrics metrics, BalanceReport report, BalanceRulesSO rules, HashSet<EnemySO> alreadyReported)
        {
            if (metrics.SpellCount == 0)
            {
                return;
            }

            // The two chance findings are about the asset, not this placement; the damage one below is
            // per-level, because the level owns the spell scale it is measured at.
            bool firstForThisAsset = metrics.Definition == null || alreadyReported.Add(metrics.Definition);

            if (metrics.MagicCastChance <= 0f)
            {
                if (!firstForThisAsset)
                {
                    return;
                }
                report.Issues.Add(new BalanceIssue(BalanceSeverity.Info, BalanceCategory.Enemy, metrics.Name,
                    $"{metrics.Name} never casts the magic it carries")
                {
                    Asset = metrics.Definition,
                    Detail = $"It knows {metrics.SpellCount} magic(s) but its behaviour "
                           + "has no CastMagic action, so the spells are player supply only and the enemy "
                           + "itself does nothing with them.",
                    Suggestion = "Add a CastMagic action to its EnemyBehavior (EnemyBehaviorSO.CastFromSpellList "
                               + "is the shape), or clear the Spells list if it is not meant to cast."
                });
                return;
            }

            if (firstForThisAsset && metrics.MagicCastChance > rules.MaxEnemyCastChance)
            {
                report.Issues.Add(new BalanceIssue(BalanceSeverity.Warning, BalanceCategory.Enemy, metrics.Name,
                    $"{metrics.Name} casts {metrics.MagicCastChance:0%} of its turns, so its archetype barely acts")
                {
                    Asset = metrics.Definition,
                    Detail = $"Above {rules.MaxEnemyCastChance:0%} the rest of the repertoire - charges, heavies, "
                           + "heals, the boss signature - is what the player stops seeing. Those actions are the "
                           + "enemy's identity; casting is meant to punctuate them.",
                    Suggestion = $"Lower the CastMagic action's ChanceGate toward {rules.MaxEnemyCastChance:0.00}."
                });
            }

            // A cast that lands less than an ordinary swing is a turn the enemy wastes, which reads to
            // the player as the enemy helping them. Only judged for spells aimed at the hero side:
            // a heal or a self-buff is not competing with the attack on damage.
            if (metrics.ExpectedCastDamage > 0f
                && metrics.AverageDamagePerHit > 0f
                && metrics.ExpectedCastDamage < metrics.AverageDamagePerHit)
            {
                report.Issues.Add(new BalanceIssue(BalanceSeverity.Info, BalanceCategory.Enemy, metrics.Reference,
                    $"{metrics.Reference} casts for less than it hits for")
                {
                    Asset = metrics.Definition,
                    Detail = $"A cast averages {metrics.ExpectedCastDamage:0.0} against this party where a plain swing "
                           + $"averages {metrics.AverageDamagePerHit:0.0}, so every cast is a turn spent making the "
                           + "fight easier. Spell power rides this level's Difficulty "
                           + $"({(metrics.Tuning != null ? metrics.Tuning.Difficulty : 1f):0.00}), and a boss on an "
                           + "absolute Overrides row deliberately does not scale at all.",
                    Suggestion = "Raise the magic's authored Power, give the effect a ScalingStat the enemy actually "
                               + "has, or lower its CastMagic ChanceGate so it leans on its attack."
                });
            }
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

        /// <summary>
        /// What to change to bring a placement to <paramref name="targetDanger"/>.
        ///
        /// <para>The dial is the level's <c>EnemyTuning.Difficulty</c>, not the template's stats: the
        /// same enemy is in other levels that may be perfectly in band. Difficulty scales MaxHealth
        /// and Strength together and the danger index is roughly the product of the two, so the
        /// multiplier needed is about <c>sqrt(target / current)</c> — a starting point rather than a
        /// solved value, because the defense curve and turn order both bend it.</para>
        /// </summary>
        private static string SuggestDifficulty(EnemyMetrics metrics, float targetDanger)
        {
            if (metrics.SoloDangerIndex <= 0f || float.IsInfinity(metrics.SoloDangerIndex) || targetDanger <= 0f)
            {
                return "Adjust this level's EnemyTuning.Difficulty until the danger index falls inside the band.";
            }

            float current = metrics.Tuning != null ? metrics.Tuning.Difficulty : 1f;
            float factor = Mathf.Sqrt(targetDanger / metrics.SoloDangerIndex);
            float suggested = Mathf.Max(0.1f, current * factor);

            string where = string.IsNullOrEmpty(metrics.Context) ? "this level" : metrics.Context;
            return $"Set {where}'s EnemyTuning.Difficulty to about {suggested:0.00} (from {current:0.00}), "
                 + $"taking this enemy here to roughly Strength {Mathf.RoundToInt(metrics.Stats[StatType.Strength] * factor)} "
                 + $"/ Health {Mathf.RoundToInt(metrics.Stats[StatType.MaxHealth] * factor)}. "
                 + "A per-enemy override on the same tuning handles anything the level dial should not move.";
        }

        /// <summary>
        /// Whether XP is paid in proportion to danger, across every **placement** rather than every
        /// enemy — the same enemy at two levels' worth of tuning is two different bargains, because
        /// <c>XpReward</c> is per asset while its danger comes from the level.
        ///
        /// <para><b>A placement below <see cref="BalanceRulesSO.MinDangerForRewardCheck"/> does not set
        /// the spread.</b> Reward efficiency is XP over danger, so something almost harmless produces a
        /// huge ratio off a tiny denominator and would decide the finding on its own — the same
        /// small-base artefact <c>MinAttritionForJumpCheck</c> exists for. Those placements are still
        /// reported, as an Info, because a harmless enemy paying full XP is worth knowing about; they
        /// just do not get to break the band.</para>
        ///
        /// <para>The finding names the <b>placement</b> at each end, not the enemy. It used to name only
        /// the enemy, and this one warning was consequently mis-diagnosed four times over — the
        /// endpoints are what tell you whether the cause is a mis-set <c>XpReward</c> (two different
        /// enemies) or an untouched <c>XpMultiplier</c> (two placements of the same one).</para>
        /// </summary>
        private static void EvaluateRewardSpread(BalanceReport report, BalanceRulesSO rules)
        {
            EnemyMetrics low = null;
            EnemyMetrics high = null;
            EnemyMetrics belowFloor = null;
            int belowFloorCount = 0;

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

                if (metrics.RewardYardstickDanger < rules.MinDangerForRewardCheck)
                {
                    belowFloorCount++;
                    if (belowFloor == null || metrics.XpPerDanger > belowFloor.XpPerDanger)
                    {
                        belowFloor = metrics;
                    }
                    continue;
                }

                if (low == null || metrics.XpPerDanger < low.XpPerDanger)
                {
                    low = metrics;
                }
                if (high == null || metrics.XpPerDanger > high.XpPerDanger)
                {
                    high = metrics;
                }
            }

            if (belowFloor != null)
            {
                report.Issues.Add(new BalanceIssue(BalanceSeverity.Info, BalanceCategory.Economy, "Reward curve",
                    $"{belowFloorCount} placement(s) pay full XP for almost no danger")
                {
                    Asset = belowFloor.Definition,
                    Detail = $"Highest is {belowFloor.Reference} at {belowFloor.XpPerDanger:0} XP per danger, "
                           + $"off a danger of only {belowFloor.RewardYardstickDanger:0.000}. Below the "
                           + $"{rules.MinDangerForRewardCheck:0.00} floor a ratio says more about the "
                           + "denominator than about the reward, so these do not set the spread.",
                    Suggestion = "Nothing to do if the floor is a deliberately gentle opening level - a "
                               + "tutorial should pay generously for what it asks. Otherwise raise the "
                               + "level's EnemyTuning.Difficulty until the fight is worth its reward."
                });
            }

            if (low == null || high == null || low.XpPerDanger <= 0f)
            {
                return;
            }

            float spread = high.XpPerDanger / low.XpPerDanger;
            if (spread <= rules.MaxRewardEfficiencySpread)
            {
                return;
            }

            // Same enemy at both ends means the level dial is the cause, not the enemy's XpReward.
            bool sameEnemy = high.Definition != null && high.Definition == low.Definition;
            string suggestion = sameEnemy
                ? $"Both ends are {high.Name}, so its XpReward is not the problem - the levels are. Set "
                + "EnemyTuning.XpMultiplier in proportion to each level's Difficulty (danger goes as "
                + "Difficulty squared), which is what that field is for."
                : $"Raise {low.Name}'s XpReward, or lower {high.Name}'s - and check "
                + "EnemyTuning.XpMultiplier as well, since the same enemy is a different bargain at "
                + "each level's Difficulty.";

            report.Issues.Add(new BalanceIssue(BalanceSeverity.Warning, BalanceCategory.Economy, "Reward curve",
                $"XP per unit of danger varies {spread:0.0}x across placements")
            {
                Asset = low.Definition,
                Detail = $"{high.Reference} pays {high.XpPerDanger:0} XP per danger (XP {high.XpReward} "
                       + $"at danger {high.RewardYardstickDanger:0.000}); {low.Reference} pays "
                       + $"{low.XpPerDanger:0} (XP {low.XpReward} at danger {low.RewardYardstickDanger:0.000}). "
                       + $"The band allows {rules.MaxRewardEfficiencySpread:0.0}x.",
                Suggestion = suggestion
            });
        }

        // ------------------------------------------------------------------ runs and levels

        /// <summary>
        /// Investment the run curve's own party is holding at <paramref name="level"/>, in the same
        /// units the tier budgets are written in: party slots past the free base cap, plus XP spent
        /// per hero. This is what "the player who walked straight here and bought nothing extra"
        /// arrives with.
        /// </summary>
        private static int CurvePartyInvestment(LevelCurve level, BalanceRulesSO rules)
        {
            int bought = Mathf.Max(0, level.PartySize - PartySlots.BaseCap);
            return bought * Mathf.Max(0, rules.HeroXpEquivalent) + Mathf.Max(0, level.XpBudget);
        }

        /// <summary>
        /// A run's last floor, when its tier is budgeted to demand more investment than the curve's
        /// own party is carrying, is the tier's <b>gate</b> — and being unclearable by that party is
        /// the design working rather than a defect. Returns the tier budget, or -1 when the floor is
        /// not a gate.
        ///
        /// <para>This is the closed-form model catching up with §0g. Attrition asks "can <i>this</i>
        /// party clear the level", and a gated finale is deliberately beyond the party that walks
        /// into it: the answer is meant to be no until the player has paid the tier's price. Without
        /// this the three deep finales report as broken content the moment they start gating, which
        /// would train everyone to ignore the check that catches genuinely unclearable levels.</para>
        /// </summary>
        private static int GateBudgetFor(RunCurve run, LevelCurve level, BalanceRulesSO rules,
            IDictionary<string, int> tiers)
        {
            if (run.Run == null || run.Levels.Count == 0 || level != run.Levels[run.Levels.Count - 1])
            {
                return -1;
            }
            if (tiers == null || !tiers.TryGetValue(CampaignOps.RunKeyOf(run.Run), out int tier))
            {
                return -1;
            }

            int budget = rules.InvestmentBudgetForTier(tier);
            return budget > CurvePartyInvestment(level, rules) ? budget : -1;
        }

        private static void EvaluateRuns(BalanceReport report, BalanceRulesSO rules, BalanceInput input)
        {
            var tiers = CampaignOps.ComputeTiers(input != null ? input.Campaign : null);

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
                    EvaluateLevel(run, level, report, rules, GateBudgetFor(run, level, rules, tiers));
                }

                for (int i = 0; i < run.DifficultyJumps.Count; i++)
                {
                    float jump = run.DifficultyJumps[i];
                    string from = run.Levels[i].Reference;
                    string to = run.Levels[i + 1].Reference;

                    // The step *onto* a gated finale is the gate. It is supposed to be a cliff for a
                    // party that has not paid for the tier, so measuring it against the ordinary
                    // level-to-level ceiling reports the design as a defect.
                    if (GateBudgetFor(run, run.Levels[i + 1], rules, tiers) >= 0)
                    {
                        continue;
                    }

                    if (jump > rules.MaxDifficultyJump)
                    {
                        // A ratio needs a base worth dividing by. A deliberately light opening floor
                        // - the tutorial is one fight and the exit - costs so little that the first
                        // real level after it is arithmetically a several-hundred-percent spike while
                        // being a couple of HP in absolute terms. Report it, but not as a warning:
                        // the honest reading is the absolute step, and that is inside every band.
                        float fromLoad = run.Levels[i].AttritionLoad;
                        float toLoad = run.Levels[i + 1].AttritionLoad;
                        bool baseTooLightToJudge = fromLoad < rules.MinAttritionForJumpCheck;

                        if (baseTooLightToJudge)
                        {
                            report.Issues.Add(new BalanceIssue(BalanceSeverity.Info, BalanceCategory.Run, run.Name,
                                $"{to} is {jump:P0} up on {from}, but {from} is too light to measure against")
                            {
                                Asset = run.Run,
                                Detail = $"Attrition load {fromLoad:0.00} → {toLoad:0.00}, an absolute step of "
                                       + $"{toLoad - fromLoad:0.00} of the party's pool. {from} is under the "
                                       + $"{rules.MinAttritionForJumpCheck:P0} floor at which a ratio means "
                                       + "anything, so the percentage is an artefact of the small base rather "
                                       + "than a difficulty cliff.",
                                Suggestion = "Nothing to do if the opening floor is deliberately light. Raise "
                                           + "MinAttritionForJumpCheck to stop hearing about it, or give that "
                                           + "level more to do so the curve starts from somewhere."
                            });
                        }
                        else
                        {
                            report.Issues.Add(new BalanceIssue(BalanceSeverity.Warning, BalanceCategory.Run, run.Name,
                                $"Difficulty spikes {jump:P0} from {from} to {to}")
                            {
                                Asset = run.Run,
                                Detail = $"Attrition load {fromLoad:0.00} → {toLoad:0.00}; the ceiling is "
                                       + $"{rules.MaxDifficultyJump:P0}.",
                                Suggestion = "Add an intermediate step, or soften the later level's spawn tables."
                            });
                        }
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

        /// <param name="gateBudget">
        /// The tier investment this floor is budgeted to demand, when it is its tier's gate and that
        /// budget is above what the curve's own party is carrying; -1 otherwise. A gate is meant to
        /// be unclearable by the party walking into it, so the attrition verdict changes meaning.
        /// </param>
        private static void EvaluateLevel(RunCurve run, LevelCurve level, BalanceReport report,
            BalanceRulesSO rules, int gateBudget = -1)
        {
            string subject = $"{run.Name} / {level.Reference}";

            if (level.ExpectedCombatRooms <= 0f)
            {
                report.Issues.Add(new BalanceIssue(BalanceSeverity.Info, BalanceCategory.Level, subject,
                    $"{level.Reference} has no combat")
                {
                    Asset = run.Run,
                    Detail = "No room in this level has a populated spawn table."
                           + (level.ExpectedEventRooms > 0f
                               ? $" It does offer {level.ExpectedEventRooms:0.0} room event(s), costing "
                                 + $"{level.ExpectedEventHealthCost:0} HP."
                               : string.Empty)
                });
                return;
            }

            if (gateBudget >= 0)
            {
                // The tier's gate. Being beyond the party that walks in is the point, so the only
                // things worth saying are what it costs and that the closed form is out of its depth
                // here: attrition composes per enemy and never sees a party focus-firing a four-enemy
                // room down, so a dense floor reads several times more expensive than it plays. Trust
                // the frontier (Frontier category), which is simulated, for the real verdict.
                report.Issues.Add(new BalanceIssue(BalanceSeverity.Info, BalanceCategory.Level, subject,
                    $"{level.Reference} gates its tier at {gateBudget} investment")
                {
                    Asset = run.Run,
                    Detail = $"Expected cost {level.ExpectedHealthCost:0} HP against a sustain pool of "
                           + $"{level.SustainPool} ({level.PartySize} hero(es) + potions) across "
                           + $"{level.ExpectedCombatRooms:0.0} combat rooms{EventShare(level)} — attrition "
                           + $"{level.AttritionLoad:0.00}. The party the curve walks in here is carrying "
                           + $"{CurvePartyInvestment(level, rules)} investment against a tier budget of "
                           + $"{gateBudget}, so it is *supposed* to fail: dying here is how the player "
                           + "learns what to buy. The closed-form number above is an upper bound only.",
                    Suggestion = "Judge this floor by its investment frontier, not by attrition. The "
                           + "attrition ceiling still applies to every other floor of the run."
                });
            }
            else if (level.AttritionMargin < 0f)
            {
                report.Issues.Add(new BalanceIssue(BalanceSeverity.Critical, BalanceCategory.Level, subject,
                    $"{level.Reference} is unclearable on one health bar")
                {
                    Asset = run.Run,
                    Detail = $"Expected cost {level.ExpectedHealthCost:0} HP against a sustain pool of "
                           + $"{level.SustainPool} ({level.PartySize} hero(es) + potions) across "
                           + $"{level.ExpectedCombatRooms:0.0} combat rooms{EventShare(level)}. "
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
                           + $"({level.PartySize} hero(es)){EventShare(level)}; the target margin is "
                           + $"{rules.MinAttritionMargin:P0}.",
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

            // Bodies on screen, not difficulty: past MaxBodiesPerRoom the battle stage overlaps its
            // own sprites (CombatStage.BuildColumn), so this fires however winnable the room is.
            foreach (var room in level.Rooms)
            {
                float bodies = room.WorstCase != null ? room.WorstCase.TotalCount : 0f;
                if (bodies <= rules.MaxBodiesPerRoom)
                {
                    continue;
                }

                report.Issues.Add(new BalanceIssue(BalanceSeverity.Warning, BalanceCategory.Level, subject,
                    $"{room.RoomName} can field {bodies:0.#} enemies at once")
                {
                    Asset = room.Room != null ? (UnityEngine.Object)room.Room : run.Run,
                    Detail = $"The battle stage fits {rules.MaxBodiesPerRoom}. Above that "
                           + "CombatStage spaces the column tighter than one sprite height and the "
                           + "enemies overlap, whatever the room's danger says.",
                    Suggestion = "Drop an EvaluationCount, or a boss escort, until the worst-case "
                               + $"roll is {rules.MaxBodiesPerRoom} or fewer."
                });
            }

            if (level.Boss != null)
            {
                // The sealed exit room has no spawn roll, so it sits outside the peak-danger tail
                // check the rolled rooms answer to (see RunCurveModel.Aggregate) and is judged here
                // against the boss ceiling instead - the rule that already says a climax is allowed
                // to read as lost on paper, because the closed form never sees a party focus-firing.
                if (level.BossDanger > rules.MaxBossDanger)
                {
                    report.Issues.Add(new BalanceIssue(BalanceSeverity.Critical, BalanceCategory.Level, subject,
                        $"{level.Reference}'s boss room is past the climax ceiling (danger {level.BossDanger:0.00})")
                    {
                        Asset = level.Boss,
                        Detail = $"{BossRoomComposition(level)} scores {level.BossDanger:0.00} against a "
                               + $"ceiling of {rules.MaxBossDanger:0.00}.",
                        Suggestion = level.BossAddCount > 0
                            ? "Danger is superlinear in body count - drop one add before touching the "
                              + "boss's own stats."
                            : "Soften the boss's Overrides row, or lower the level's Difficulty."
                    });
                }

                if (level.BossToTrashRatio > 0f)
                {
                    if (level.BossToTrashRatio > rules.MaxBossToTrashRatio)
                    {
                        report.Issues.Add(new BalanceIssue(BalanceSeverity.Critical, BalanceCategory.Level, subject,
                            $"{level.Reference} boss room is {level.BossToTrashRatio:0.0}x the level's trash difficulty")
                        {
                            Asset = level.Boss,
                            Detail = $"{BossRoomComposition(level)} scores {level.BossDanger:0.00} against an "
                                   + $"average room of "
                                   + $"{level.BossDanger / Mathf.Max(0.001f, level.BossToTrashRatio):0.00}. "
                                   + $"The band is {rules.MinBossToTrashRatio:0.0}x–{rules.MaxBossToTrashRatio:0.0}x.",
                            Suggestion = level.BossAddCount > 0
                                ? "Nothing in the level prepares the player for this. Drop an add, or "
                                  + "escalate the trash leading to it."
                                : "Nothing in the level prepares the player for this. Soften the boss or "
                                  + "escalate the trash leading to it."
                        });
                    }
                    else if (level.BossToTrashRatio < rules.MinBossToTrashRatio)
                    {
                        report.Issues.Add(new BalanceIssue(BalanceSeverity.Warning, BalanceCategory.Level, subject,
                            $"{level.Reference} boss room is only {level.BossToTrashRatio:0.0}x the level's trash difficulty")
                        {
                            Asset = level.Boss,
                            Detail = $"{BossRoomComposition(level)}. A climax should stand out; the floor is "
                                   + $"{rules.MinBossToTrashRatio:0.0}x.",
                            Suggestion = "Give it adds (RunLevelEntry.BossAdds) - with trash rooms capped at "
                                   + "two bodies, the exit room is the level's remaining danger budget. "
                                   + "Raising the boss's own Health or Attack works too, but a lone boss "
                                   + "bought up to ratio is a long fight rather than a hard one."
                        });
                    }
                }
            }
        }

        /// <summary>
        /// How the sealed exit room is populated, for the findings that quote its danger - the boss
        /// alone reads differently from a boss holding a line, and the fix differs with it.
        /// </summary>
        private static string BossRoomComposition(LevelCurve level)
        {
            string boss = level.Boss != null ? level.Boss.Label : "The boss";
            if (level.BossAddCount <= 0)
            {
                return $"{boss}, alone,";
            }
            return $"{boss} plus {level.BossAddCount} add{(level.BossAddCount == 1 ? "" : "s")}";
        }

        /// <summary>
        /// The events half of a level's attrition, phrased for the findings that quote a level's HP
        /// cost. Empty when the level has no events, so the sentence reads as it always did.
        /// </summary>
        private static string EventShare(LevelCurve level)
        {
            if (level == null || level.ExpectedEventHealthCost <= 0f)
            {
                return string.Empty;
            }

            return $", of which {level.ExpectedEventHealthCost:0} HP ({level.EventAttritionShare:P0}) is "
                 + $"{level.ExpectedEventRooms:0.0} room event(s) rather than fights";
        }

        /// <summary>
        /// What an event asset looks like across every level that offers it. Gathered before anything
        /// is reported because most of these questions are only answerable run-wide: an event gated on
        /// Intelligence 6 is *meant* to be shut to the solo Warrior on level 1 and open once the
        /// Acolyte is recruited, so "no party can meet this gate" is only true when no level's party can.
        /// </summary>
        private class EventAudit
        {
            public Rooms.Events.RoomEventSO Definition;
            public RoomEventEncounter First;
            public bool RequirementsEverMet;
            public bool AnyEngageableOption;
            public bool AnyStatCheck;
            public bool AnyDownside;
            public float MaxAppearChance;
            public float TotalOccurrences;
            public float WorstDamageFraction;
            public string WorstDamageWhere = "";
            public readonly List<string> Levels = new List<string>();

            public string Name => First != null ? First.Name : Definition.name;

            public void Observe(RoomEventEncounter encounter, RunCurve run, LevelCurve level)
            {
                if (First == null)
                {
                    First = encounter;
                }

                Levels.Add($"{run.Name} / {level.Reference}");
                RequirementsEverMet |= encounter.RequirementsMet;
                MaxAppearChance = Mathf.Max(MaxAppearChance, encounter.AppearChancePerRoom);
                TotalOccurrences += encounter.Occurrences;

                int smallestBar = SmallestHealthBar(level.Party);

                foreach (var option in encounter.Options)
                {
                    if (!option.IsEngageable)
                    {
                        continue;
                    }

                    AnyEngageableOption = true;
                    AnyStatCheck |= option.Kind == Rooms.Events.RoomEventOptionKind.StatCheck;
                    AnyDownside |= option.HasDownside;

                    if (smallestBar > 0 && option.WorstSingleHeroDamage > 0f)
                    {
                        float fraction = option.WorstSingleHeroDamage / smallestBar;
                        if (fraction > WorstDamageFraction)
                        {
                            WorstDamageFraction = fraction;
                            WorstDamageWhere = $"{option.WorstSingleHeroDamage:0} damage against a "
                                             + $"{smallestBar} HP bar in {level.Reference}";
                        }
                    }
                }
            }

            private static int SmallestHealthBar(PartyBaseline party)
            {
                if (party == null)
                {
                    return 0;
                }

                int smallest = 0;
                foreach (var hero in party.Heroes)
                {
                    int bar = hero.Effective[StatType.MaxHealth];
                    if (bar > 0 && (smallest == 0 || bar < smallest))
                    {
                        smallest = bar;
                    }
                }
                return smallest;
            }
        }

        /// <summary>
        /// Room events, judged the same way spawn tables are. Two shapes of finding: what the events do
        /// to a <i>level</i> (how much of its attrition they are), and what is wrong with an
        /// <i>event asset</i> (unreachable, ungated, or hitting harder than the 1-HP floor will allow).
        /// </summary>
        private static void EvaluateEvents(BalanceReport report, BalanceRulesSO rules, BalanceInput input)
        {
            var audits = new Dictionary<Rooms.Events.RoomEventSO, EventAudit>();

            foreach (var run in report.Runs)
            {
                foreach (var level in run.Levels)
                {
                    EvaluateLevelEvents(run, level, report, rules);

                    foreach (var encounter in level.Events)
                    {
                        if (encounter == null || encounter.Event == null)
                        {
                            continue;
                        }

                        EventAudit audit;
                        if (!audits.TryGetValue(encounter.Event, out audit))
                        {
                            audit = new EventAudit { Definition = encounter.Event };
                            audits[encounter.Event] = audit;
                        }
                        audit.Observe(encounter, run, level);
                    }
                }
            }

            var ceiling = ProjectStatCeiling(input);
            foreach (var pair in audits)
            {
                EvaluateEventAsset(pair.Value, report, rules, ceiling);
            }

            // An event asset no run's room pool lists is content nobody can reach - the room-event
            // equivalent of ProgressionMap's unreachable magic. Only answerable from the project-wide
            // list, since the curves can only see what the rooms offer.
            if (input.RoomEvents != null)
            {
                foreach (var definition in input.RoomEvents)
                {
                    if (definition == null || audits.ContainsKey(definition))
                    {
                        continue;
                    }

                    report.Issues.Add(new BalanceIssue(BalanceSeverity.Warning, BalanceCategory.Event,
                        string.IsNullOrEmpty(definition.Title) ? definition.name : definition.Title,
                        "No room in any run offers this event")
                    {
                        Asset = definition,
                        Detail = "Nothing lists it in RoomSO.PossibleEvents, or the rooms that do are in no "
                               + "run's level pool, so it can never be placed.",
                        Suggestion = "Add it to a RoomSO.PossibleEvents that a run's templates draw from, "
                                   + "or delete the asset."
                    });
                }
            }
        }

        /// <summary>
        /// What a level's events do to the level: how much of its attrition comes from gambles rather
        /// than fights, and how many level-long afflictions the curve is not pricing.
        /// </summary>
        private static void EvaluateLevelEvents(RunCurve run, LevelCurve level, BalanceReport report, BalanceRulesSO rules)
        {
            if (level.ExpectedEventHealthCost <= 0f)
            {
                return;
            }

            string subject = $"{run.Name} / {level.Reference}";

            if (level.EventAttritionShare > rules.MaxEventAttritionShare)
            {
                report.Issues.Add(new BalanceIssue(BalanceSeverity.Warning, BalanceCategory.Level, subject,
                    $"{level.Reference} takes {level.EventAttritionShare:P0} of its attrition from room events")
                {
                    Asset = run.Run,
                    Detail = $"{level.ExpectedEventHealthCost:0} HP from {level.ExpectedEventRooms:0.0} "
                           + $"event(s) against {level.ExpectedCombatHealthCost:0} HP from "
                           + $"{level.ExpectedCombatRooms:0.0} combat room(s); the ceiling is "
                           + $"{rules.MaxEventAttritionShare:P0}. Tuning the spawn tables will not move "
                           + "most of this level's difficulty.",
                    Suggestion = "Lower the events' SpawnChancePercent, soften their failure outcomes, or "
                               + "raise the level's combat load to match."
                });
            }

            if (level.ExpectedAfflictions >= 1f)
            {
                report.Issues.Add(new BalanceIssue(BalanceSeverity.Info, BalanceCategory.Level, subject,
                    $"{level.Reference} hands out {level.ExpectedAfflictions:0.0} level affliction(s) the curve does not price")
                {
                    Asset = run.Run,
                    Detail = "An affliction is a stat delta for the rest of the level, so it raises the cost "
                           + "of every fight after it. The model counts them but cannot price them without "
                           + "re-measuring the level against a second party, so this level is harder than "
                           + "its attrition load says.",
                    Suggestion = "Read the attrition figure as a floor on levels that hand out afflictions."
                });
            }
        }

        /// <summary>
        /// The highest each stat can reach anywhere in the project: every authored hero with their
        /// whole sphere grid bought. A spawn gate above this is unreachable by construction, which is
        /// an authoring error; a gate the *modelled* run path never reaches is a much weaker claim,
        /// because the model grows a roster only through <c>RunLevelEntry.RescueHero</c> and cannot
        /// see a hero acquired any other way.
        /// </summary>
        private static StatBlock ProjectStatCeiling(BalanceInput input)
        {
            var ceiling = new StatBlock();
            if (input.HeroesToAudit == null)
            {
                return ceiling;
            }

            foreach (var hero in input.HeroesToAudit)
            {
                if (hero == null)
                {
                    continue;
                }

                // A budget nothing can exhaust, so this is the grid bought out.
                var nodes = SphereGridOps.GreedySpend(hero.SphereGrid, null, int.MaxValue / 4, out _);
                var stats = HeroStatCalculator.BaseStatsForNodes(hero, nodes);

                foreach (var stat in StatCatalog.Types)
                {
                    if (stats[stat] > ceiling[stat])
                    {
                        ceiling[stat] = stats[stat];
                    }
                }
            }

            return ceiling;
        }

        /// <summary>One event asset, judged across every level that offers it.</summary>
        private static void EvaluateEventAsset(
            EventAudit audit, BalanceReport report, BalanceRulesSO rules, StatBlock projectCeiling)
        {
            var definition = audit.Definition;
            string subject = audit.Name;

            if (!audit.RequirementsEverMet)
            {
                var parts = new List<string>();
                foreach (var requirement in definition.SpawnRequirements)
                {
                    if (requirement != null && requirement.Type != StatType.None)
                    {
                        parts.Add($"{StatCatalog.DisplayName(requirement.Type)} {requirement.Amount}");
                    }
                }

                var beyondEveryone = RoomEventModel.UnmetRequirements(definition.SpawnRequirements, projectCeiling);
                if (beyondEveryone.Count > 0)
                {
                    report.Issues.Add(new BalanceIssue(BalanceSeverity.Warning, BalanceCategory.Event, subject,
                        "No hero in the project can reach this event's SpawnRequirements")
                    {
                        Asset = definition,
                        Detail = $"Needs {string.Join(" + ", parts)}, which is past the best any authored hero "
                               + "reaches with their whole sphere grid bought - so the event can never be "
                               + $"placed, in any of the {audit.Levels.Count} level(s) that offer it.",
                        Suggestion = "Lower the threshold, or add nodes that grant enough of the stat. A gate "
                                   + "is meant to be a reason to recruit, not a wall."
                    });
                }
                else
                {
                    report.Issues.Add(new BalanceIssue(BalanceSeverity.Info, BalanceCategory.Event, subject,
                        "No party on the modelled run path meets this event's SpawnRequirements")
                    {
                        Asset = definition,
                        Detail = $"Needs {string.Join(" + ", parts)}. A hero in the project reaches it, but the "
                               + "run curves only grow a roster through RunLevelEntry.RescueHero - a hero the "
                               + "player unlocks any other way is invisible to the model - so this may be a "
                               + "modelling gap rather than unreachable content.",
                        Suggestion = "If the stat is meant to come from a hero the player unlocks later, this "
                                   + "is working as designed; if not, the gate never opens on the rescue-only path."
                    });
                }
            }
            else if (audit.MaxAppearChance <= 0f)
            {
                report.Issues.Add(new BalanceIssue(BalanceSeverity.Warning, BalanceCategory.Event, subject,
                    "This event can never be placed")
                {
                    Asset = definition,
                    Detail = $"SpawnChancePercent is {definition.SpawnChancePercent:0.#}, so every roll fails. "
                           + "The rooms that list it always fall through to the next candidate.",
                    Suggestion = "Raise SpawnChancePercent above 0, or remove it from the room pools."
                });
            }

            if (!audit.AnyEngageableOption)
            {
                report.Issues.Add(new BalanceIssue(BalanceSeverity.Warning, BalanceCategory.Event, subject,
                    "This event has nothing to do")
                {
                    Asset = definition,
                    Detail = "Every option is a Decline, so the Action button opens a window whose only exit "
                           + "is walking away.",
                    Suggestion = "Add a StatCheck or Guaranteed option, or delete the event."
                });
            }
            else if (audit.AnyStatCheck && !audit.AnyDownside)
            {
                report.Issues.Add(new BalanceIssue(BalanceSeverity.Info, BalanceCategory.Event, subject,
                    "This gamble has no downside")
                {
                    Asset = definition,
                    Detail = "No outcome in any pool costs health, a consumable, an affliction or a woken "
                           + "enemy, so taking the check is free. A risk-free gamble is a button.",
                    Suggestion = "Give the failure pool a cost, or make the option Guaranteed and stop "
                               + "presenting it as a risk."
                });
            }

            if (audit.WorstDamageFraction > rules.MaxEventDamageFraction)
            {
                var severity = audit.WorstDamageFraction >= 1f
                    ? BalanceSeverity.Warning
                    : BalanceSeverity.Info;

                report.Issues.Add(new BalanceIssue(severity, BalanceCategory.Event, subject,
                    $"One outcome takes {audit.WorstDamageFraction:P0} of a hero's health bar")
                {
                    Asset = definition,
                    Detail = $"{audit.WorstDamageWhere}; the ceiling is {rules.MaxEventDamageFraction:P0}. "
                           + "RoomEventRunner clamps event damage to a floor of 1 HP, so past 100% the "
                           + "authored number stops mattering and every failure reads the same.",
                    Suggestion = "Lower the outcome's Power, or spread it over the party instead of the one "
                               + "hero who reached in."
                });
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

            foreach (var overlap in variety.SpellOverlaps)
            {
                string a = string.IsNullOrEmpty(overlap.A.DisplayName) ? overlap.A.name : overlap.A.DisplayName;
                string b = string.IsNullOrEmpty(overlap.B.DisplayName) ? overlap.B.name : overlap.B.DisplayName;

                report.Issues.Add(new BalanceIssue(BalanceSeverity.Warning, BalanceCategory.Variety, $"{a} / {b}",
                    $"{a} and {b} know {overlap.Share:P0} the same spells")
                {
                    Asset = overlap.A,
                    Detail = $"Shared: {string.Join(", ", overlap.SharedMagic)}. Spell variety is what makes two "
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
                    Suggestion = "Vary the enemies' LootTable gear so kills feel distinct. (Shared materials are not counted here.)"
                });
            }

            if (variety.EnemiesWithoutSpells > 0)
            {
                report.Issues.Add(new BalanceIssue(BalanceSeverity.Info, BalanceCategory.Variety, variety.Scope,
                    $"{variety.EnemiesWithoutSpells} enemy definition(s) know no spells")
                {
                    Detail = "A spell list is what lets an enemy do anything but swing; one with an empty Spells "
                           + "list contributes nothing to it.",
                    Suggestion = "Give every enemy at least one spell, or accept it as a pure bruiser."
                });
            }

            if (variety.CatalogMagicCount > 0 && variety.EnemySpellCoverage < 0.5f)
            {
                report.Issues.Add(new BalanceIssue(BalanceSeverity.Warning, BalanceCategory.Variety, variety.Scope,
                    $"Only {variety.DistinctEnemySpells} of {variety.CatalogMagicCount} magics are cast by any enemy")
                {
                    Detail = $"{variety.EnemySpellCoverage:P0} coverage — the rest of the catalog is unreachable in play.",
                    Suggestion = "Spread the catalog across enemy spell lists so the party meets what it can learn."
                });
            }
        }

        // ------------------------------------------------------------------ progression / unlocks

        /// <summary>
        /// The supply side of the elemental layer. Resistances and combos are both authored content that
        /// only becomes live if the sphere grids hand the player the pieces — so a combo whose required
        /// tag lives on magic no grid teaches, or a level that resists an element the player cannot yet deal,
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
                else if (combo.TagsNotLearnable.Count > 0)
                {
                    report.Issues.Add(new BalanceIssue(BalanceSeverity.Warning, BalanceCategory.Progression, combo.Name,
                        $"Combo '{combo.Name}' is unreachable — {string.Join(", ", combo.TagsNotLearnable)} only exists on magic no grid teaches")
                    {
                        Asset = combo.Combo,
                        Detail = $"Requires {string.Join(" + ", combo.RequiredTags)}. "
                               + $"{string.Join("; ", combo.EnablingMagic)}. A sphere grid is the only route to "
                               + "magic, so a tag carried only by magic no MagicKnown node grants can never reach "
                               + "a fight.",
                        Suggestion = "Put the carrying magic on a MagicKnown node in some hero's sphere grid."
                    });
                }
                else if (combo.UnlockedAt == null)
                {
                    report.Issues.Add(new BalanceIssue(BalanceSeverity.Info, BalanceCategory.Progression, combo.Name,
                        $"Combo '{combo.Name}' is affordable but the campaign never buys it")
                    {
                        Asset = combo.Combo,
                        Detail = $"Every required tag is on a grid ({string.Join("; ", combo.EnablingMagic)}), "
                               + $"roughly {combo.InvestmentToEnable} xp in total, but no modelled party owns all "
                               + "of them at once on any floor. Note GreedySpend is a breadth build, so it "
                               + "under-buys deliberately deep magic branches.",
                        Suggestion = "Move a carrying magic shallower on its grid, or accept it as a reward for "
                               + "a build the model does not play."
                    });
                }
            }

            // Name the unreachable magic outright — the variety tab only reports the count. This is a
            // Critical rather than the Warning it was under Draw: a spell on no grid has no second
            // route to the player, so it is dead content rather than late content.
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
                report.Issues.Add(new BalanceIssue(BalanceSeverity.Critical, BalanceCategory.Progression, "Sphere grids",
                    $"{unreachable.Count} magic(s) are on no hero's sphere grid")
                {
                    Detail = $"Unreachable: {string.Join(", ", unreachable)}. "
                           + $"Grid coverage is {map.ReachableMagicCount}/{map.CatalogMagicCount}. A MagicKnown "
                           + "node is the only way a hero learns a spell, so these cannot be cast by anyone.",
                    Suggestion = "Add each to a MagicKnown node on some hero's grid, or delete the asset."
                });
            }

            foreach (var run in map.Runs)
            {
                foreach (var level in run.Levels)
                {
                    // Front-loading: if the party's kit jumps by most of the catalog in one step, the
                    // rest of the campaign has nothing left to reveal. The cause moved with the
                    // mechanic — it used to be a room pool offering too many draws, and it is now a
                    // grid handing over too many MagicKnown nodes for one floor's worth of XP.
                    if (map.CatalogMagicCount > 0 && level.NewlyKnown.Count > 0)
                    {
                        float share = (float)level.NewlyKnown.Count / map.CatalogMagicCount;
                        if (share > rules.MaxUnlockSharePerLevel)
                        {
                            report.Issues.Add(new BalanceIssue(BalanceSeverity.Warning, BalanceCategory.Progression,
                                $"{run.Name} / {level.Reference}",
                                $"{level.Reference} adds {share:P0} of the magic catalog to the party's kit at once")
                            {
                                Asset = run.Run,
                                Detail = $"{level.NewlyKnown.Count} of {map.CatalogMagicCount} magics are first "
                                       + "known by the modelled party here, because that floor's XP reaches a "
                                       + $"cluster of MagicKnown nodes. Ceiling is {rules.MaxUnlockSharePerLevel:P0}.",
                                Suggestion = "Spread the MagicKnown nodes across grid depths so spells arrive "
                                       + "across the campaign rather than in one pass."
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
                            Detail = "Its enemies carry resistances, but none are in an element the modelled party "
                                   + "can deal by this "
                                   + $"point in the run order. Available so far: "
                                   + $"{(level.ElementsAvailable.Count > 0 ? string.Join(", ", level.ElementsAvailable) : "none")}.",
                            Suggestion = "Either move the enabling magic shallower on a grid, or retarget "
                                   + "the resistances to an element the player already has."
                        });
                    }
                }
            }
        }

        // ------------------------------------------------------------------ economy

        /// <summary>Campaign-wide material yield: every run's, summed per material.</summary>
        private static List<MaterialYield> BuildMaterialYield(BalanceReport report)
        {
            var totals = new Dictionary<string, MaterialYield>();
            foreach (var run in report.Runs)
            {
                foreach (var yield in MaterialYieldModel.ForRun(run))
                {
                    if (!totals.TryGetValue(yield.Key, out var entry))
                    {
                        entry = new MaterialYield
                        {
                            Material = yield.Material,
                            Key = yield.Key,
                            Name = yield.Name
                        };
                        totals[yield.Key] = entry;
                    }
                    entry.FromKills += yield.FromKills;
                    entry.FromCaches += yield.FromCaches;
                }
            }

            var list = new List<MaterialYield>(totals.Values);
            list.Sort((a, b) =>
            {
                int byTotal = b.Total.CompareTo(a.Total);
                return byTotal != 0 ? byTotal : string.CompareOrdinal(a.Name, b.Name);
            });
            return list;
        }

        /// <summary>
        /// The two ways a material can be authored and never reach a player: nothing drops it at all,
        /// and a level authoring a MaterialTable with no cache to roll it in. Both are silent in the
        /// inspector, and both get much more expensive to find once buildings are priced in materials
        /// (<c>docs/plans/HUB.md</c> §7), which is why the check lands with the drops rather than after.
        /// </summary>
        private static void EvaluateMaterials(BalanceReport report, BalanceInput input)
        {
            foreach (var material in MaterialYieldModel.Unobtainable(input.Items, report.Runs))
            {
                report.Issues.Add(new BalanceIssue(BalanceSeverity.Warning, BalanceCategory.Economy,
                    material.DisplayName, "No enemy or level yields this material")
                {
                    Asset = material,
                    Detail = "It is in the item catalog but appears in no EnemySO.LootTable and no "
                           + "LevelDefinitionSO.MaterialTable that a cache can roll, so no run can produce it.",
                    Suggestion = "Add it to a drop table, or delete it."
                });
            }

            foreach (var run in report.Runs)
            {
                foreach (var level in MaterialYieldModel.LevelsWithUnreachableMaterialTable(run))
                {
                    report.Issues.Add(new BalanceIssue(BalanceSeverity.Info, BalanceCategory.Economy,
                        $"{run.Name} {level.Reference}", "Material table never rolls - the level has no cache")
                    {
                        Asset = level.Template,
                        Detail = $"{level.Name} authors {level.Template.MaterialTable.Count} material entr(ies) "
                               + "but TreasureRooms is 0, and a level's MaterialTable is only rolled when a "
                               + "cache is opened. Its enemies still drop theirs.",
                        Suggestion = "Raise TreasureRooms (which also lowers the floor's attrition by taking a "
                                   + "room off the combat count), or clear the MaterialTable."
                    });
                }
            }
        }

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


        /// <summary>
        /// Simulates every floor end to end, off one pool of health, potions and charges.
        ///
        /// <para>Why this is separate from <see cref="RunSimulations"/>: that one measures rooms, and a
        /// room is not where runs end. It re-clones a full-health party with a full potion belt for
        /// every encounter, so a four-room floor is measured as four independent opening fights - which
        /// is why every single encounter in the project reports a 100% win rate while the closed-form
        /// attrition model has floors running at three quarters of the party's resources. Both are
        /// right about different questions. This answers the one the player asks.</para>
        /// </summary>
        private static void RunFloorSimulations(BalanceInput input, BalanceRulesSO rules, BalanceReport report)
        {
            foreach (var run in report.Runs)
            {
                foreach (var level in run.Levels)
                {
                    var rooms = BuildFloorRooms(level);
                    if (rooms.Count == 0)
                    {
                        continue;
                    }

                    var settings = new EncounterSimulator.FloorSimSettings
                    {
                        Trials = rules.SimulationTrials,
                        Seed = rules.SimulationSeed,
                        MaxTurns = rules.MaxSimTurns,
                        Combos = input.Combos,
                        PotionCount = level.Party.PotionCount,
                        PotionHealAmount = level.Party.PotionHealAmount,
                        RestRooms = level.RestRooms,
                        RestHealFraction = RoomKindRewards.RestHealFraction,

                        // Charges are a run resource: only floor 0 starts full. Granting every floor
                        // full charges is exactly the optimism the per-room simulation carries.
                        StartsWithFullCharges = level.Index == 0
                    };

                    // Same loadout rules as the per-room sims, so the two are comparable.
                    AssignMagicLoadout(level.Party, report.Save, input.Magic);

                    var floorReport = new FloorSimReport
                    {
                        Label = $"{run.Name} / {level.Reference}",
                        Asset = run.Run,
                        Run = run.Run,
                        LevelIndex = level.Index,
                        Rooms = rooms.Count,
                        PredictedAttrition = level.AttritionLoad,
                        StartsWithFullCharges = settings.StartsWithFullCharges
                    };
                    floorReport.Outcomes = EncounterSimulator.RunAllPoliciesOnFloor(level.Party, rooms, settings);
                    report.Floors.Add(floorReport);
                }
            }
        }

        /// <summary>
        /// A floor's rooms in the order the player meets them: each combat room repeated as often as
        /// the level is expected to contain it, then the boss alone in the sealed exit room.
        /// </summary>
        private static List<IList<SimUnit>> BuildFloorRooms(LevelCurve level)
        {
            var rooms = new List<IList<SimUnit>>();
            if (level == null || level.Rooms == null)
            {
                return rooms;
            }

            // The floor's combat rooms, minus the sealed exit room: that is appended below, boss and
            // all. Walking past it here as well fought every boss twice, which quietly doubled the
            // climax of every finale in the campaign from the day the floor simulation shipped (§5h)
            // until 2026-08-28.
            var pool = new List<RoomEncounter>();
            float expectedRooms = 0f;
            foreach (var room in level.Rooms)
            {
                if (room == null || !room.IsCombatRoom || room.IsBossRoom)
                {
                    continue;
                }
                pool.Add(room);
                expectedRooms += Mathf.Max(0f, room.Occurrences);
            }

            // Occurrences is fractional on a generated level (RoomsToGenerate / poolSize), so the
            // floor has to be rounded to whole rooms somewhere. Round the *total* once and apportion
            // it largest-remainder-first, for the same reason ToDiscreteUnits does: rounding each
            // pool entry on its own makes the floor's length depend on how many kinds of room it
            // draws from rather than on how many rooms it generates. With two pool entries and a
            // boss, `RoomsToGenerate` 5 and 7 both landed on 2.5 appearances each and produced the
            // identical five-room floor - a whole authored room the model simply could not see.
            int roomSeats = Mathf.RoundToInt(expectedRooms);
            var appearances = new int[pool.Count];
            int allocated = 0;
            for (int i = 0; i < pool.Count && allocated < roomSeats; i++)
            {
                int whole = Mathf.Min(Mathf.FloorToInt(Mathf.Max(0f, pool[i].Occurrences)), roomSeats - allocated);
                appearances[i] = whole;
                allocated += whole;
            }

            // Leftovers go to the entries with the largest fractional part, ties to the likelier room.
            var order = new List<int>();
            for (int i = 0; i < pool.Count; i++)
            {
                order.Add(i);
            }
            order.Sort((a, b) =>
            {
                float occA = Mathf.Max(0f, pool[a].Occurrences);
                float occB = Mathf.Max(0f, pool[b].Occurrences);
                int byFraction = (occB - Mathf.Floor(occB)).CompareTo(occA - Mathf.Floor(occA));
                return byFraction != 0 ? byFraction : occB.CompareTo(occA);
            });
            int next = 0;
            while (allocated < roomSeats && order.Count > 0)
            {
                appearances[order[next % order.Count]]++;
                allocated++;
                next++;
            }

            for (int i = 0; i < pool.Count; i++)
            {
                for (int n = 0; n < appearances[i]; n++)
                {
                    var units = pool[i].Expected.ToDiscreteUnits();
                    if (units.Count > 0)
                    {
                        rooms.Add(units);
                    }
                }
            }

            if (level.Boss != null)
            {
                // Take the sealed exit room straight off the curve rather than rebuilding it from
                // level.Boss: the room is the boss *and* its adds, and a floor that fought only the
                // boss would under-price every finale the moment one gained an escort.
                RoomEncounter bossRoom = null;
                foreach (var room in level.Rooms)
                {
                    if (room != null && room.IsBossRoom)
                    {
                        bossRoom = room;
                        break;
                    }
                }

                var units = bossRoom != null
                    ? bossRoom.Expected.ToDiscreteUnits()
                    : new List<SimUnit>();
                if (units.Count == 0)
                {
                    var boss = SimUnit.FromEnemy(level.Boss, level.Tuning);
                    if (boss != null)
                    {
                        units.Add(boss);
                    }
                }
                if (units.Count > 0)
                {
                    rooms.Add(units);
                }
            }

            return rooms;
        }

        private static void EvaluateFloorSimulations(BalanceReport report, BalanceRulesSO rules)
        {
            // A run's last floor is the one that has to be able to end the run.
            var finalFloorOf = new Dictionary<RunDefinitionSO, int>();
            foreach (var floor in report.Floors)
            {
                if (floor.Run == null)
                {
                    continue;
                }
                if (!finalFloorOf.TryGetValue(floor.Run, out int deepest) || floor.LevelIndex > deepest)
                {
                    finalFloorOf[floor.Run] = floor.LevelIndex;
                }
            }

            foreach (var floor in report.Floors)
            {
                var outcome = floor.Adaptive;
                if (outcome == null || outcome.Trials == 0)
                {
                    continue;
                }

                if (outcome.WipeRate > rules.MaxFloorWipeRate)
                {
                    report.Issues.Add(new BalanceIssue(BalanceSeverity.Warning, BalanceCategory.Simulation, floor.Label,
                        $"Floor wipes the party {outcome.WipeRate:P0} of the time")
                    {
                        Asset = floor.Asset,
                        Detail = $"Over {outcome.Trials} simulated floors of {floor.Rooms} room(s) under competent "
                               + $"play the party died {outcome.WipeRate:P0} of the time, clearing "
                               + $"{outcome.AverageRoomsCleared:0.0} rooms and losing {outcome.AverageHeroDeaths:0.00} "
                               + $"heroes on average. The ceiling is {rules.MaxFloorWipeRate:P0}. Closed-form "
                               + $"attrition predicted {floor.PredictedAttrition:0.00}.",
                        Suggestion = "Thin the floor, add a refuge, or lower the level's Difficulty. Note nothing "
                               + "revives mid-floor, so a hero lost early compounds for every room after it."
                    });
                }

                bool isFinalFloor = floor.Run != null
                    && finalFloorOf.TryGetValue(floor.Run, out int deepest)
                    && floor.LevelIndex == deepest;

                if (isFinalFloor && outcome.WipeRate < rules.MinFinalFloorWipeRate)
                {
                    report.Issues.Add(new BalanceIssue(BalanceSeverity.Warning, BalanceCategory.Simulation, floor.Label,
                        "The run's last floor cannot end the run")
                    {
                        Asset = floor.Asset,
                        Detail = $"The deepest floor wipes the party {outcome.WipeRate:P0} of the time against a "
                               + $"{rules.MinFinalFloorWipeRate:P0} floor, ending at "
                               + $"{outcome.AverageEndHealthFraction:P0} health with "
                               + $"{outcome.AveragePotionsUsed:0.0} of {outcome.Rooms} room(s) worth of potions "
                               + "spent. A run with no failure state makes every decision inside it free, which "
                               + "is the root cause behind the depth-gap findings rather than a separate problem.",
                        Suggestion = "Enemy count is the lever with headroom - per-enemy strength is already at "
                               + "MinHitsToKillHero. Add rooms or enemies per room on the deepest floor, or cut "
                               + "the sustain the floor hands back (potions, refuges)."
                    });
                }

                if (outcome.WipeRate <= 0f && outcome.AverageEndHealthFraction > rules.TrivialFloorEndHealth)
                {
                    report.Issues.Add(new BalanceIssue(BalanceSeverity.Info, BalanceCategory.Simulation, floor.Label,
                        "Floor never spends the party's resources")
                    {
                        Asset = floor.Asset,
                        Detail = $"Ends at {outcome.AverageEndHealthFraction:P0} health (ceiling "
                               + $"{rules.TrivialFloorEndHealth:P0}) having used "
                               + $"{outcome.AveragePotionsUsed:0.0} potions, and never wipes. Fine for an opening "
                               + "floor; a problem anywhere the run is supposed to be escalating."
                    });
                }

                if (outcome.Stalemates > 0)
                {
                    report.Issues.Add(new BalanceIssue(BalanceSeverity.Info, BalanceCategory.Simulation, floor.Label,
                        $"{outcome.Stalemates} of {outcome.Trials} floors ran out of turns")
                    {
                        Asset = floor.Asset,
                        Detail = "A room hit the turn cap with enemies still standing - neither side able to finish "
                               + "the other, which usually means a heal or a defense outpaces the party's output."
                    });
                }
            }
        }

        // ------------------------------------------------- investment frontiers

        /// <summary>
        /// The frontier sweep on its own — run curves closed-form (about a second), then only the
        /// finales simulated. This is the loop a tuning pass wants: a full <see cref="Analyze"/> with
        /// simulation on runs hundreds of battles per *encounter* before it reaches the frontiers, and
        /// none of that output moves while you are pushing a tier toward its budget.
        ///
        /// <para>No findings are produced. Read the returned frontiers, change one dial, measure
        /// again; <see cref="Analyze"/> is what turns the result into issues once it is settled.</para>
        /// </summary>
        public static List<FloorFrontier> MeasureFrontiers(BalanceInput input)
        {
            var frontiers = new List<FloorFrontier>();
            if (input == null)
            {
                return frontiers;
            }

            var rules = input.Rules ?? BalanceRulesSO.CreateDefault();
            var report = new BalanceReport();
            report.Party = BuildReferenceParty(input, rules, null);
            BuildRunCurves(input, rules, report);
            RunFrontierSweeps(input, rules, report);
            return report.Frontiers;
        }

        /// <summary>
        /// Measures what each run's <b>finale</b> asks of the player, as a frontier over the two
        /// investment axes the game sells: party width and sphere-grid XP.
        ///
        /// <para>Why this exists beside <see cref="RunFloorSimulations"/>: that one answers "can this
        /// floor be lost", against a single modelled party. The design being tuned toward is that
        /// deeper tiers are <i>unclearable until the player invests</i>, which makes the honest
        /// question "lost by whom, having paid what" — and a single reference party cannot express
        /// that. Worse, sampling one party actively misleads: the first pass at this swept XP at a
        /// three-hero party, found nothing, and reported that XP does not matter. Three heroes is the
        /// corner of the surface where nothing matters. (<c>docs/BALANCING.md</c> §5i → §5j.)</para>
        ///
        /// <para>Only finales are swept. A tier's gate is its last floor — the earlier ones are the
        /// run showing you what it is made of — and a frontier costs a dozen floor batches, so
        /// sweeping every floor would multiply the analysis by the campaign's depth for findings
        /// nothing acts on.</para>
        /// </summary>
        private static void RunFrontierSweeps(BalanceInput input, BalanceRulesSO rules, BalanceReport report)
        {
            var tiers = CampaignOps.ComputeTiers(input.Campaign);

            foreach (var run in report.Runs)
            {
                var level = FinalLevelOf(run);
                if (level == null || level.Party == null || level.Party.Heroes.Count == 0)
                {
                    continue;
                }

                var rooms = BuildFloorRooms(level);
                if (rooms.Count == 0)
                {
                    continue;
                }

                // The roster in party order. Rooms carry no party state (LevelEnemyTuning owns the
                // enemy numbers), which is exactly what lets one room list be fought by every mix.
                var roster = new List<HeroSO>();
                foreach (var hero in level.Party.Heroes)
                {
                    if (hero.Definition != null && !roster.Contains(hero.Definition))
                    {
                        roster.Add(hero.Definition);
                    }
                }

                // Then everyone still unowned, up to the widest party the game can field. Leaving
                // them out would hide half the width axis - and with it the endgame's "bring
                // another body" route, the alternative that keeps a deep tier from being a
                // checklist.
                //
                // NOTE (2026-09-05): this used to be justified by the tavern - a hero was bought
                // with gold exactly as a party slot is, so it belonged inside the same currency.
                // With the tavern retired a hero is a progression *unlock*: a hard precondition
                // on the frontier rather than a currency inside it (SPECIALIZATION.md section 5b,
                // item 5). The behaviour is left as-is deliberately - balance work is paused
                // until the specialization refactor lands - but this is the assumption to revisit
                // when it resumes, because the model now assumes a roster the player may not be
                // able to reach at that tier.
                if (input.Roster != null)
                {
                    foreach (var hero in input.Roster)
                    {
                        if (hero != null && !roster.Contains(hero) && roster.Count < PartySlots.MaxCap)
                        {
                            roster.Add(hero);
                        }
                    }
                }

                if (roster.Count == 0)
                {
                    continue;
                }

                int tier = -1;
                if (run.Run != null && tiers.TryGetValue(CampaignOps.RunKeyOf(run.Run), out int depth))
                {
                    tier = depth;
                }

                var settings = new FrontierSweepSettings
                {
                    Roster = roster,
                    Rooms = rooms,
                    Widths = rules.FrontierPartyWidths,
                    XpSteps = rules.FrontierXpSteps,
                    GoldSteps = rules.FrontierGoldSteps,
                    Catalog = input.Items,
                    StatWeightFor = rules.WeightFor,
                    HeroXpEquivalent = rules.HeroXpEquivalent,
                    InvestmentPointsPerGold = rules.InvestmentPointsPerGold,
                    BaseWidth = PartySlots.BaseCap,
                    ClearWipeRate = rules.MaxFloorWipeRate,
                    SafeWipeRate = rules.MinFinalFloorWipeRate,
                    EquivalentInvestmentTolerance = rules.EquivalentInvestmentTolerance,
                    PotionItem = level.Party.PotionItem,
                    PotionCount = level.Party.PotionCount,
                    PrepareParty = party => AssignMagicLoadout(party, report.Save, input.Magic),
                    Sim = new EncounterSimulator.FloorSimSettings
                    {
                        Trials = rules.FrontierTrials,
                        Seed = rules.SimulationSeed,
                        MaxTurns = rules.MaxSimTurns,
                        Policy = SimPolicy.Adaptive,
                        Combos = input.Combos,
                        RestRooms = level.RestRooms,
                        RestHealFraction = RoomKindRewards.RestHealFraction,

                        // Same rule as the floor sims: charges are a run resource, so only a run's
                        // first floor starts full. A finale almost never is one.
                        StartsWithFullCharges = level.Index == 0
                    }
                };

                var frontier = InvestmentFrontier.Measure(settings);
                frontier.Label = $"{run.Name} / {level.Reference}";
                frontier.Asset = run.Run;
                frontier.Run = run.Run;
                frontier.LevelIndex = level.Index;
                frontier.Tier = tier;
                frontier.Budget = rules.InvestmentBudgetForTier(tier);
                report.Frontiers.Add(frontier);
            }
        }

        private static LevelCurve FinalLevelOf(RunCurve run)
        {
            if (run == null || run.Levels == null || run.Levels.Count == 0)
            {
                return null;
            }
            return run.Levels[run.Levels.Count - 1];
        }

        /// <summary>
        /// Reads the frontiers against the two properties the design asks of them: that a tier offers
        /// a <b>choice</b> of how to pay, and that the ladder <b>rises</b> with campaign depth.
        ///
        /// <para>Both are stated in investment cost rather than in content numbers on purpose. A
        /// frontier stated as "asks 450" survives every edit to the enemies that produce it; one
        /// stated as "Difficulty 2.8 across four rooms" is obsolete the moment anything moves.</para>
        /// </summary>
        private static void EvaluateFrontiers(BalanceReport report, BalanceRulesSO rules)
        {
            if (report.Frontiers.Count == 0)
            {
                return;
            }

            // Deepest ask seen at each shallower tier, so "did this tier move outward" is answered
            // against the whole tier rather than against whichever run happened to be measured first.
            var deepestAskByTier = new Dictionary<int, int>();
            foreach (var frontier in report.Frontiers)
            {
                if (frontier.Tier < 0 || frontier.Unclearable)
                {
                    continue;
                }
                if (!deepestAskByTier.TryGetValue(frontier.Tier, out int ask) || frontier.AskedInvestment > ask)
                {
                    deepestAskByTier[frontier.Tier] = frontier.AskedInvestment;
                }
            }

            foreach (var frontier in report.Frontiers)
            {
                if (frontier.Unclearable)
                {
                    report.Issues.Add(new BalanceIssue(BalanceSeverity.Critical, BalanceCategory.Frontier,
                        frontier.Label, "No investment the player can make clears this floor")
                    {
                        Asset = frontier.Asset,
                        Detail = $"Every mix on the sweep — up to {Widest(rules)} heroes at "
                               + $"{Richest(rules)} XP each — still wipes above the "
                               + $"{rules.MaxFloorWipeRate:P0} ceiling on this {frontier.Rooms}-room "
                               + "floor. A tier the player cannot reach by investing is not a gate, "
                               + "it is a wall: the die → bank → upgrade → return loop has nothing to "
                               + "return to.",
                        Suggestion = "Thin the floor or lower the level's Difficulty until the widest "
                               + "swept mix clears it, then re-measure where the frontier lands."
                    });
                    continue;
                }

                int asked = frontier.AskedInvestment;

                if (!frontier.OffersChoice)
                {
                    report.Issues.Add(new BalanceIssue(BalanceSeverity.Warning, BalanceCategory.Frontier,
                        frontier.Label, "This tier is a checklist, not a choice")
                    {
                        Asset = frontier.Asset,
                        Detail = $"Exactly one affordable way to clear it: {frontier.FrontierText}, at "
                               + $"{asked} investment. The design asks for a range — buy the hero, or "
                               + "deepen the grid, or blend the two — so a tier with one entry on its "
                               + "frontier takes the decision away from the player and just tells them "
                               + "what to buy.",
                        Suggestion = "The two axes substitute at roughly "
                               + $"{rules.HeroXpEquivalent} XP per hero. A tier offers a choice when a "
                               + "narrower, better-grown party lands within "
                               + $"{rules.EquivalentInvestmentTolerance} of the wider, greener one — so "
                               + "move the floor's load off single big hits (which width answers) or "
                               + "off raw volume (which XP answers), whichever it currently leans on."
                    });
                }

                if (frontier.Tier > 0)
                {
                    int shallower = -1;
                    string shallowerLabel = "";
                    for (int tier = 0; tier < frontier.Tier; tier++)
                    {
                        if (deepestAskByTier.TryGetValue(tier, out int ask) && ask > shallower)
                        {
                            shallower = ask;
                            shallowerLabel = $"tier {tier}";
                        }
                    }

                    if (shallower >= 0 && asked <= shallower)
                    {
                        report.Issues.Add(new BalanceIssue(BalanceSeverity.Warning, BalanceCategory.Frontier,
                            frontier.Label, "This tier asks no more than the one before it")
                        {
                            Asset = frontier.Asset,
                            Detail = $"Tier {frontier.Tier} asks {asked} investment; {shallowerLabel} "
                                   + $"already asked {shallower}. Depth is supposed to be the axis "
                                   + "along which the ask grows — a deeper tier that costs the same or "
                                   + "less means every upgrade bought for the earlier one is spent, and "
                                   + "the rest of the campaign is free.",
                            Suggestion = "Raise this floor's load until its frontier sits past the "
                                   + $"shallower tier's — the tier budget for depth {frontier.Tier} is "
                                   + $"{frontier.Budget}. Enemy count is the lever with headroom; "
                                   + "per-enemy strength is pinned at MinHitsToKillHero against the "
                                   + "fresh party, which is the party that is meant to die here."
                        });
                    }
                }

                if (frontier.Budget >= 0)
                {
                    int tolerance = Mathf.Max(0, rules.InvestmentBudgetTolerance);
                    if (asked < frontier.Budget - tolerance)
                    {
                        report.Issues.Add(new BalanceIssue(BalanceSeverity.Warning, BalanceCategory.Frontier,
                            frontier.Label, $"Tier asks {asked} investment against a budget of {frontier.Budget}")
                        {
                            Asset = frontier.Asset,
                            Detail = $"Cheapest clearing mixes: {frontier.FrontierText}. The floor "
                                   + $"stops threatening anyone at {SafeText(frontier)}. A tier under "
                                   + "its budget is content the player walks through on upgrades they "
                                   + "bought for something shallower.",
                            Suggestion = $"Add roughly {frontier.Budget - asked} investment worth of "
                                   + "load. Rooms per floor and enemies per room are the honest levers "
                                   + "(see docs/BALANCING.md §1 Consequence 2); Difficulty buys danger "
                                   + "quadratically but runs into hero one-shotting first."
                        });
                    }
                    else if (asked > frontier.Budget + tolerance)
                    {
                        report.Issues.Add(new BalanceIssue(BalanceSeverity.Warning, BalanceCategory.Frontier,
                            frontier.Label, $"Tier asks {asked} investment against a budget of {frontier.Budget}")
                        {
                            Asset = frontier.Asset,
                            Detail = $"Cheapest clearing mixes: {frontier.FrontierText}. Overshooting "
                                   + "the budget is worse than it looks: every frontier here is "
                                   + "measured against a greedy (near-optimal) grid spend, so a player "
                                   + "who built for flavour is weaker than this number at the same XP.",
                            Suggestion = "Thin the floor, hand back a refuge, or lower the level's "
                                   + "Difficulty until the frontier comes back inside "
                                   + $"±{tolerance} of {frontier.Budget}."
                        });
                    }
                }

                if (frontier.SafeInvestment != int.MaxValue
                    && frontier.SafeInvestment <= asked + Mathf.Max(0, rules.EquivalentInvestmentTolerance))
                {
                    report.Issues.Add(new BalanceIssue(BalanceSeverity.Info, BalanceCategory.Frontier,
                        frontier.Label, "The floor goes from lethal to harmless in one purchase")
                    {
                        Asset = frontier.Asset,
                        Detail = $"It first clears at {asked} investment and stops being a threat at "
                               + $"{frontier.SafeInvestment} — a band of "
                               + $"{frontier.SafeInvestment - asked}. A tier whose whole difficulty "
                               + "lives inside one upgrade is a cliff: before it the floor is "
                               + "impossible, after it nothing on the floor is a decision.",
                        Suggestion = "Widen the band by spreading the floor's threat across more "
                               + "rooms rather than concentrating it in the boss, so extra investment "
                               + "pays down gradually instead of all at once."
                    });
                }
            }

            EvaluateGridShare(report, rules);
        }

        /// <summary>
        /// The standing rule: the campaign's <b>last</b> floor may not be clearable on a token slice
        /// of a hero's sphere grid. If it is, the grid was never the difficulty curve — the player
        /// finishes on starter nodes and everything past them is decoration.
        ///
        /// <para>Only the deepest tier is judged, and only as a <i>floor</i>. That is deliberate:
        /// <b>how</b> a player spends the grid is meant to matter more than how much of it they own
        /// (<c>docs/BALANCING.md</c> §5t), so a tight band here would be measuring the wrong thing.
        /// A breadth build and a single-branch build should reach the end at very different shares.</para>
        /// </summary>
        private static void EvaluateGridShare(BalanceReport report, BalanceRulesSO rules)
        {
            float fullGrid = AverageFullGridCost(report);
            if (fullGrid <= 0f || rules.MinGridShareForLastFloor <= 0f)
            {
                return;
            }

            FloorFrontier last = null;
            foreach (var frontier in report.Frontiers)
            {
                if (frontier.Unclearable)
                {
                    continue;
                }
                if (last == null || frontier.Tier > last.Tier)
                {
                    last = frontier;
                }
            }

            var mix = last != null ? last.CheapestMix : null;
            if (mix == null)
            {
                return;
            }

            float share = mix.XpPerHero / fullGrid;
            if (share >= rules.MinGridShareForLastFloor)
            {
                return;
            }

            report.Issues.Add(new BalanceIssue(BalanceSeverity.Warning, BalanceCategory.Frontier,
                last.Label,
                $"The campaign's last floor clears on {share:P0} of a sphere grid")
            {
                Asset = last.Asset,
                Detail = $"Its cheapest clearing mix spends {mix.XpPerHero} xp per hero against an "
                       + $"average full grid of {fullGrid:0} xp, with {mix.GoldOnGear} gold of gear "
                       + $"and {mix.PartySize} hero(es). The standing floor is "
                       + $"{rules.MinGridShareForLastFloor:P0}: a campaign finishable on starter "
                       + "nodes means grid progression is not what the difficulty curve is made of.",
                Suggestion = "Raise the deep floors' load, or lengthen the grid so the same nodes are "
                           + "a smaller share of it. Note this is a floor, not a target — a player "
                           + "who commits to one branch should still be able to finish on less of the "
                           + "grid than a breadth build does."
            });
        }

        /// <summary>Average full-grid XP across the party's heroes, or 0 when none has a grid.</summary>
        private static float AverageFullGridCost(BalanceReport report)
        {
            if (report.Party == null)
            {
                return 0f;
            }

            float total = 0f;
            int counted = 0;
            foreach (var hero in report.Party.Heroes)
            {
                var grid = hero.Definition != null ? hero.Definition.SphereGrid : null;
                if (grid == null || grid.Nodes == null || grid.Nodes.Count == 0)
                {
                    continue;
                }

                var every = new List<string>();
                foreach (var node in grid.Nodes)
                {
                    if (node != null && !string.IsNullOrEmpty(node.Key))
                    {
                        every.Add(node.Key);
                    }
                }
                total += SphereGridOps.TotalCostOf(grid, every);
                counted++;
            }
            return counted > 0 ? total / counted : 0f;
        }

        private static string SafeText(FloorFrontier frontier)
        {
            return frontier.SafeInvestment == int.MaxValue
                ? "no swept mix (it stays a threat throughout)"
                : $"{frontier.SafeInvestment} investment";
        }

        private static int Widest(BalanceRulesSO rules)
        {
            int widest = 0;
            if (rules.FrontierPartyWidths != null)
            {
                foreach (int width in rules.FrontierPartyWidths)
                {
                    if (width > widest)
                    {
                        widest = width;
                    }
                }
            }
            return widest;
        }

        private static int Richest(BalanceRulesSO rules)
        {
            int richest = 0;
            if (rules.FrontierXpSteps != null)
            {
                foreach (int xp in rules.FrontierXpSteps)
                {
                    if (xp > richest)
                    {
                        richest = xp;
                    }
                }
            }
            return richest;
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
