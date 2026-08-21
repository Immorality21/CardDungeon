using System.Collections.Generic;
using Assets.Scripts.Heroes;
using Assets.Scripts.Items;
using Assets.Scripts.Rooms;
using Assets.Scripts.UnitStats;
using UnityEngine;

namespace Assets.Scripts.Balance
{
    /// <summary>
    /// Pure re-derivation of hero stats from a <see cref="HeroSO"/> plus XP and a gear loadout.
    /// <see cref="Hero"/> itself cannot be used here: its GetEffective* methods reach into
    /// <see cref="InventoryManager"/>.Instance, so it only works with a live scene. The level-up
    /// loop below mirrors <see cref="Hero.AddXp"/> exactly (cumulative XpRequired, applied in
    /// ascending level order) and the gear math reuses <see cref="InventoryOperations"/>, so the
    /// only duplicated logic is the loop shape.
    /// </summary>
    public static class HeroStatCalculator
    {
        /// <summary>Highest level the hero has a <see cref="LevelConfiguration"/> for (1 if none).</summary>
        public static int MaxDefinedLevel(HeroSO hero)
        {
            if (hero == null || hero.LevelProgression == null)
            {
                return 1;
            }

            int max = 1;
            foreach (var entry in hero.LevelProgression)
            {
                if (entry != null && entry.Level > max)
                {
                    max = entry.Level;
                }
            }
            return max;
        }

        /// <summary>The level a hero reaches with the given total XP, using the same loop as Hero.AddXp.</summary>
        public static int LevelForXp(HeroSO hero, int totalXp)
        {
            int level = 1;
            if (hero == null || hero.LevelProgression == null)
            {
                return level;
            }

            while (true)
            {
                var next = hero.LevelProgression.Find(l => l != null && l.Level == level + 1);
                if (next == null || totalXp < next.XpRequired)
                {
                    return level;
                }
                level = next.Level;
            }
        }

        /// <summary>Total XP needed to reach a level, or -1 when the level has no configuration.</summary>
        public static int XpToReachLevel(HeroSO hero, int level)
        {
            if (hero == null || hero.LevelProgression == null || level <= 1)
            {
                return 0;
            }

            var entry = hero.LevelProgression.Find(l => l != null && l.Level == level);
            return entry != null ? entry.XpRequired : -1;
        }

        /// <summary>
        /// Base stats (no gear) at a level, applying every progression entry up to it. Stat-agnostic:
        /// a level's <c>Gains</c> block is simply added, so a new StatType needs no change here.
        /// </summary>
        public static Stats BaseStatsAtLevel(HeroSO hero, int level)
        {
            if (hero == null)
            {
                return new Stats(new StatBlock(new UnitStat(StatType.MaxHealth, 1)));
            }

            var block = hero.BaseStats.Clone();
            if (hero.LevelProgression != null)
            {
                for (int l = 2; l <= level; l++)
                {
                    var entry = hero.LevelProgression.Find(e => e != null && e.Level == l);
                    if (entry != null)
                    {
                        block.Add(entry.Gains);
                    }
                }
            }

            return new Stats(block);
        }

        /// <summary>Base stats for a saved XP total.</summary>
        public static Stats BaseStatsForXp(HeroSO hero, int totalXp)
        {
            return BaseStatsAtLevel(hero, LevelForXp(hero, totalXp));
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
