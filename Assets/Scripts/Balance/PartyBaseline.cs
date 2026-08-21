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
        public int Level;
        public int MaxDefinedLevel;
        public int SavedXp = -1;                    // -1 when not sourced from a save
        /// <summary>Stats after level gains and gear. Named to distinguish it from <c>SimUnit.Stats</c>,
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
    /// without "for whom". Built from hero definitions plus a level and an optional gear loadout, so
    /// the same code serves the designed baseline and a real save file.
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
        /// Builds a reference party. <paramref name="level"/> is applied to every hero; pass
        /// <paramref name="gearLookup"/> to fold in equipped items (a save audit does, the designed
        /// baseline does not).
        /// </summary>
        public static PartyBaseline Build(
            IList<HeroSO> heroDefinitions,
            int level,
            System.Func<HeroSO, List<ItemSO>> gearLookup = null,
            ItemSO potionItem = null,
            int potionCount = -1)
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

                int clampedLevel = Mathf.Max(1, level);
                var baseStats = HeroStatCalculator.BaseStatsAtLevel(definition, clampedLevel);
                var effective = HeroStatCalculator.WithGear(baseStats, gear);

                var hero = new HeroBaseline
                {
                    Definition = definition,
                    Level = clampedLevel,
                    MaxDefinedLevel = HeroStatCalculator.MaxDefinedLevel(definition),
                    Effective = effective,
                    Gear = gear
                };

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
                    // Heroes deal physical damage; gear resistance folds in the same way Hero does.
                    AttackDamageType = Combat.DamageType.Normal,
                    Resistances = InventoryOperations.ComputeResistances(gear)
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
