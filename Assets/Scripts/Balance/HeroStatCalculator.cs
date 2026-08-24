using System.Collections.Generic;
using Assets.Scripts.Heroes;
using Assets.Scripts.Items;
using Assets.Scripts.Rooms;
using Assets.Scripts.UnitStats;
using UnityEngine;

namespace Assets.Scripts.Balance
{
    /// <summary>
    /// Pure re-derivation of hero stats from a <see cref="HeroSO"/> plus activated sphere-grid
    /// nodes and a gear loadout. <see cref="Hero"/> itself cannot be used here: its GetEffective*
    /// methods reach into <see cref="InventoryManager"/>.Instance, so it only works with a live
    /// scene. Node grants come from <see cref="SphereGridOps"/> and the gear math reuses
    /// <see cref="InventoryOperations"/>, so nothing is duplicated.
    /// </summary>
    public static class HeroStatCalculator
    {
        /// <summary>
        /// Base stats (no gear) with a set of activated sphere-grid nodes applied. Stat-agnostic
        /// like <see cref="BaseStatsAtLevel"/>: node <c>Gains</c> blocks are simply added. The
        /// returned <see cref="Stats"/> starts at full health, preserving the freshly-derived-hero
        /// guarantee the level path had.
        /// </summary>
        public static Stats BaseStatsForNodes(HeroSO hero, IEnumerable<string> activatedNodes)
        {
            if (hero == null)
            {
                return new Stats(new StatBlock(new UnitStat(StatType.MaxHealth, 1)));
            }

            var block = hero.BaseStats.Clone();
            block.Add(SphereGridOps.StatsForNodes(hero.SphereGrid, activatedNodes));
            return new Stats(block);
        }

        /// <summary>
        /// Folds a gear loadout into base stats the same way Hero.GetEffective* does:
        /// (base + raw bonus) * (1 + percent/100), rounded.
        /// </summary>
        public static StatBlock WithGear(Stats baseStats, IEnumerable<ItemSO> gear)
        {
            var raw = InventoryOperations.ComputeBonuses(gear, BonusType.Raw);
            var pct = InventoryOperations.ComputeBonuses(gear, BonusType.Percentage);

            var result = new StatBlock();
            foreach (var stat in StatCatalog.Types)
            {
                result[stat] = Apply(baseStats[stat], raw[stat], pct[stat]);
            }
            return result;
        }

        private static int Apply(int baseValue, float rawBonus, float percentBonus)
        {
            return Mathf.RoundToInt((baseValue + rawBonus) * (1f + percentBonus / 100f));
        }
    }
}
