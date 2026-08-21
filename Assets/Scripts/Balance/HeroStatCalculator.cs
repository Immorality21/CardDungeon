using System.Collections.Generic;
using Assets.Scripts.Heroes;
using Assets.Scripts.Items;
using Assets.Scripts.Rooms;
using UnityEngine;

namespace Assets.Scripts.Balance
{
    /// <summary>A hero's stats after level-ups and gear, with no MonoBehaviour or singleton involved.</summary>
    public struct EffectiveStats
    {
        public int Strength;
        public int Endurance;
        public int MaxHealth;
        public int Agility;
        public int Intelligence;
        public int Spirit;
        public int Luck;
    }

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

        /// <summary>Base stats (no gear) at a level, applying every progression entry up to it.</summary>
        public static Stats BaseStatsAtLevel(HeroSO hero, int level)
        {
            if (hero == null)
            {
                return new Stats(0, 0, 1);
            }

            var stats = new Stats(hero.BaseStrength, hero.BaseEndurance, hero.BaseHealth, hero.BaseAgility,
                hero.BaseIntelligence, hero.BaseSpirit, hero.BaseLuck);
            if (hero.LevelProgression == null)
            {
                return stats;
            }

            for (int l = 2; l <= level; l++)
            {
                var entry = hero.LevelProgression.Find(e => e != null && e.Level == l);
                if (entry == null)
                {
                    continue;
                }
                stats.Strength += entry.StrengthGain;
                stats.Endurance += entry.EnduranceGain;
                stats.MaxHealth += entry.HealthGain;
                stats.Health += entry.HealthGain;
                stats.Agility += entry.AgilityGain;
            }

            return stats;
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
        public static EffectiveStats WithGear(Stats baseStats, IEnumerable<ItemSO> gear)
        {
            var raw = InventoryOperations.ComputeBonuses(gear, BonusType.Raw);
            var pct = InventoryOperations.ComputeBonuses(gear, BonusType.Percentage);

            return new EffectiveStats
            {
                Strength = Apply(baseStats.Strength, raw[StatType.Strength], pct[StatType.Strength]),
                Endurance = Apply(baseStats.Endurance, raw[StatType.Endurance], pct[StatType.Endurance]),
                MaxHealth = Apply(baseStats.MaxHealth, raw[StatType.MaxHealth], pct[StatType.MaxHealth]),
                Agility = Apply(baseStats.Agility, raw[StatType.Agility], pct[StatType.Agility]),
                Intelligence = Apply(baseStats.Intelligence, raw[StatType.Intelligence], pct[StatType.Intelligence]),
                Spirit = Apply(baseStats.Spirit, raw[StatType.Spirit], pct[StatType.Spirit]),
                Luck = Apply(baseStats.Luck, raw[StatType.Luck], pct[StatType.Luck])
            };
        }

        private static int Apply(int baseValue, float rawBonus, float percentBonus)
        {
            return Mathf.RoundToInt((baseValue + rawBonus) * (1f + percentBonus / 100f));
        }
    }
}
