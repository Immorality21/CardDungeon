using System.Collections.Generic;
using Assets.Scripts.Combat;
using Assets.Scripts.Heroes;
using Assets.Scripts.Items;
using Assets.Scripts.Resources;
using Assets.Scripts.UnitStats;
using UnityEngine;

namespace Assets.Scripts.Balance
{
    /// <summary>One hero in the reference party, with the numbers that decide how survivable they are.</summary>
    public class HeroBaseline
    {
        public HeroSO Definition;
        public int SavedXp = -1;                    // unspent bank; -1 when not sourced from a save
        /// <summary>XP budget this baseline was modelled at (greedy-spent on the grid);
        /// -1 when the node set came from a save instead.</summary>
        public int XpBudget = -1;
        /// <summary>Sphere-grid node keys this baseline's stats include.</summary>
        public List<string> ActivatedNodes = new List<string>();
        /// <summary>XP those activations cost, at current node prices.</summary>
        public int SpentXp;
        public int NodesActivated;
        public int NodesTotal;
        /// <summary>Stats after node gains and gear. Named to distinguish it from <c>SimUnit.Stats</c>,
        /// which is a live <c>Stats</c> with current health.</summary>
        public StatBlock Effective;
        public List<ItemSO> Gear = new List<ItemSO>();
        public SimUnit Unit;

        public string Name => Definition != null ? Definition.DisplayName : "(none)";

        /// <summary>Fraction of incoming damage this hero's defense removes.</summary>
        public float EnduranceReduction => BalanceMath.EnduranceReduction(Effective[StatType.Endurance]);
    }

    /// <summary>
    /// The party every other metric is measured against — "is this boss too hard" has no answer
    /// without "for whom". Built from hero definitions plus an XP budget (greedy-spent on each
    /// hero's sphere grid) and an optional gear loadout, so the same code serves the designed
    /// baseline and a real save file (which supplies its actual node sets instead).
    /// </summary>
    public class PartyBaseline
    {
        public List<HeroBaseline> Heroes = new List<HeroBaseline>();
        public string SourceLabel = "Designed baseline";

        /// <summary>Healing carried into a level: potion count x restore amount.</summary>
        public int PotionCount = PartyResourceManager.DEFAULT_HEALING_POTION_MAX;
        public int PotionHealAmount;
        public ItemSO PotionItem;

        public List<SimUnit> Units
        {
            get
            {
                var units = new List<SimUnit>();
                foreach (var hero in Heroes)
                {
                    if (hero.Unit != null)
                    {
                        units.Add(hero.Unit);
                    }
                }
                return units;
            }
        }

        public int Size => Heroes.Count;

        public int HealthPool
        {
            get
            {
                int total = 0;
                foreach (var hero in Heroes)
                {
                    total += hero.Effective[StatType.MaxHealth];
                }
                return total;
            }
        }

        /// <summary>Total HP the party can restore inside one level from its potion belt.</summary>
        public int HealingPool => PotionCount * PotionHealAmount;

        /// <summary>Everything a level can chew through before the run ends: HP plus healing.</summary>
        public int SustainPool => HealthPool + HealingPool;

        /// <summary>
        /// The party's <b>best</b> value per stat - the maximum over heroes, stat by stat, so a
        /// requirement covered by one hero and another covered by a different one both read as met.
        /// This is the same shape <c>DungeonManager.BestRosterStats</c> hands to room-event
        /// placement, and it is what those spawn rolls and stat checks are resolved against.
        /// </summary>
        public StatBlock BestStats
        {
            get
            {
                var best = new StatBlock();
                foreach (var hero in Heroes)
                {
                    if (hero == null || hero.Effective == null)
                    {
                        continue;
                    }

                    foreach (var stat in StatCatalog.Types)
                    {
                        int value = hero.Effective[stat];
                        if (value > best[stat])
                        {
                            best[stat] = value;
                        }
                    }
                }
                return best;
            }
        }

        /// <summary>
        /// The hero a room event's check would be resolved against: the party's best at
        /// <paramref name="stat"/>, matching <see cref="Rooms.Events.RoomEventResolver.BestFor"/>.
        /// Null for an empty party or <see cref="StatType.None"/>.
        /// </summary>
        public HeroBaseline BestAt(StatType stat)
        {
            if (stat == StatType.None)
            {
                return null;
            }

            HeroBaseline best = null;
            int bestValue = int.MinValue;
            foreach (var hero in Heroes)
            {
                if (hero == null || hero.Effective == null)
                {
                    continue;
                }
                int value = hero.Effective[stat];
                if (value > bestValue)
                {
                    bestValue = value;
                    best = hero;
                }
            }
            return best;
        }

        /// <summary>
        /// Builds a reference party at a flat XP budget per hero, greedy-spent on each hero's
        /// sphere grid (<see cref="SphereGridOps.GreedySpend"/>, deterministic). Pass
        /// <paramref name="gearLookup"/> to fold in equipped items (a save audit does, the designed
        /// baseline does not).
        /// </summary>
        public static PartyBaseline Build(
            IList<HeroSO> heroDefinitions,
            int xpBudgetPerHero,
            System.Func<HeroSO, List<ItemSO>> gearLookup = null,
            ItemSO potionItem = null,
            int potionCount = -1)
        {
            return Build(heroDefinitions, _ => xpBudgetPerHero, gearLookup, potionItem, potionCount, null);
        }

        /// <summary>
        /// The full form: a per-hero XP budget (the run curve grows it per floor), and an optional
        /// <paramref name="nodesLookup"/> that supplies real activated node sets instead of the
        /// budget spend — the save audit uses it so a save is measured at exactly the nodes it
        /// bought, whatever today's prices would have afforded.
        /// </summary>
        public static PartyBaseline Build(
            IList<HeroSO> heroDefinitions,
            System.Func<HeroSO, int> xpBudgetFor,
            System.Func<HeroSO, List<ItemSO>> gearLookup,
            ItemSO potionItem,
            int potionCount,
            System.Func<HeroSO, List<string>> nodesLookup)
        {
            var baseline = new PartyBaseline();

            if (potionItem != null)
            {
                baseline.PotionItem = potionItem;
                baseline.PotionHealAmount = potionItem.ConsumableAmount;
            }
            if (potionCount >= 0)
            {
                baseline.PotionCount = potionCount;
            }

            if (heroDefinitions == null)
            {
                return baseline;
            }

            foreach (var definition in heroDefinitions)
            {
                if (definition == null)
                {
                    continue;
                }

                var gear = gearLookup != null ? gearLookup(definition) : new List<ItemSO>();
                gear = gear ?? new List<ItemSO>();

                List<string> nodes;
                int budget = -1;
                if (nodesLookup != null)
                {
                    nodes = SphereGridOps.SanitizeActivated(definition.SphereGrid, nodesLookup(definition));
                }
                else
                {
                    budget = Mathf.Max(0, xpBudgetFor != null ? xpBudgetFor(definition) : 0);
                    nodes = SphereGridOps.GreedySpend(definition.SphereGrid, null, budget, out _);
                }

                var baseStats = HeroStatCalculator.BaseStatsForNodes(definition, nodes);
                var effective = HeroStatCalculator.WithGear(baseStats, gear);

                var hero = new HeroBaseline
                {
                    Definition = definition,
                    XpBudget = budget,
                    ActivatedNodes = nodes,
                    SpentXp = SphereGridOps.TotalCostOf(definition.SphereGrid, nodes),
                    NodesActivated = nodes.Count,
                    NodesTotal = definition.SphereGrid != null && definition.SphereGrid.Nodes != null
                        ? definition.SphereGrid.Nodes.Count
                        : 0,
                    Effective = effective,
                    Gear = gear
                };

                // Node resistances reach the danger index and the simulator through SimUnit, the
                // same way gear resistance does — no separate plumbing.
                var resistances = SphereGridOps.ResistancesForNodes(definition.SphereGrid, nodes);
                resistances.AddRange(InventoryOperations.ComputeResistances(gear));

                hero.Unit = new SimUnit
                {
                    DisplayName = hero.Name,
                    HeroKey = definition.SaveKey,
                    IsHero = true,
                    Stats = new Rooms.Stats(effective),
                    Effective = effective.Clone(),
                    // Resolve the hero's chosen attack stat the same way Hero does, or the model
                    // would have every hero swinging off Strength while the game does not.
                    AttackStat = definition != null ? definition.ResolvedAttackStat : StatType.Strength,
                    EffectiveAttackPower = AttackPowerFor(definition, effective),
                    // Heroes deal physical damage; node + gear resistance folds in like Hero does.
                    AttackDamageType = Combat.DamageType.Normal,
                    Resistances = resistances
                };

                baseline.Heroes.Add(hero);
            }

            return baseline;
        }

        /// <summary>
        /// The stat named by <see cref="HeroSO.ResolvedAttackStat"/> — the same property
        /// <c>Hero.AttackStat</c> reads, so the two cannot drift.
        /// </summary>
        private static int AttackPowerFor(HeroSO definition, StatBlock effective)
        {
            return effective[definition != null ? definition.ResolvedAttackStat : StatType.Strength];
        }

        /// <summary>
        /// Fresh, full-health clones of the party — one set per simulated battle.
        ///
        /// <para>Health is reset here rather than relied upon: <c>SimUnit.Clone()</c> preserves
        /// current health (it clones the whole <c>Stats</c>), so the guarantee in this method's name
        /// would otherwise depend on nobody ever wounding a baseline unit. Enforcing it costs one
        /// line and stops every later trial silently starting wounded.</para>
        /// </summary>
        public List<SimUnit> CloneUnits()
        {
            var clones = new List<SimUnit>();
            foreach (var hero in Heroes)
            {
                if (hero.Unit == null)
                {
                    continue;
                }
                var clone = hero.Unit.Clone();
                clone.Stats.Health = clone.Stats.MaxHealth;
                clones.Add(clone);
            }
            return clones;
        }
    }
}
