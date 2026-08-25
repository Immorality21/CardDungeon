using System;
using System.Collections.Generic;
using Assets.Scripts.UnitStats;
using UnityEngine;

namespace Assets.Scripts.Enemies
{
    /// <summary>One stat and the factor a level multiplies it by.</summary>
    [Serializable]
    public class StatScale
    {
        public StatType Stat;

        [Tooltip("1 leaves the template's value alone. 1.5 is +50%.")]
        public float Multiplier = 1f;
    }

    /// <summary>
    /// Absolute stat values one level uses for one enemy, overriding both the template and the
    /// level's scaling. Only the stats actually listed are replaced, so setting just MaxHealth leaves
    /// everything else on the scaled template value.
    /// </summary>
    [Serializable]
    public class EnemyStatOverride
    {
        public EnemySO Enemy;

        [Tooltip("Only the stats listed here are overridden. Leave the block empty to change nothing.")]
        public StatBlock Stats = new StatBlock();

        [Tooltip("0 leaves the template's XP reward (after the level's XpMultiplier) alone.")]
        public int XpReward;

        [Tooltip("0 leaves the template's gold reward (after the level's GoldMultiplier) alone.")]
        public int GoldReward;
    }

    /// <summary>
    /// What one level does to the enemies it spawns.
    ///
    /// <para><b>An <see cref="EnemySO"/> is a template, not a stat block.</b> The same enemy turns up
    /// across the whole campaign - Floating Eye and Dragon are in all ten authored levels - against
    /// parties that range from 40 HP and no spent XP to 64 HP and 176. One authored stat block
    /// provably cannot be right in both places: whatever makes the Eye worth fighting on the first
    /// floor is furniture on the last. So the template carries the enemy's <i>identity</i> - its
    /// sprite, archetype, element, resistances, Draw table, loot - and the level it appears in
    /// carries its <i>numbers</i>.</para>
    ///
    /// <para>Three layers, cheapest first. <see cref="Difficulty"/> is the one-number dial and scales
    /// the two stats that move danger most - MaxHealth and Strength, which is what every enemy swings
    /// off. <see cref="StatScales"/> reaches the others when a level wants faster or tougher-skinned
    /// enemies specifically. <see cref="Overrides"/> sets absolute values for one enemy when the
    /// curve is not the answer.</para>
    /// </summary>
    [Serializable]
    public class LevelEnemyTuning
    {
        [Tooltip("The level's difficulty dial. Multiplies every enemy's MaxHealth and Strength - the " +
                 "two stats the danger index is most sensitive to, and the two the analyzer's own " +
                 "suggestions name. 1 = the template's authored numbers.")]
        public float Difficulty = 1f;

        [Tooltip("Per-stat multipliers on top of Difficulty, for a level that wants its enemies fast " +
                 "or armoured rather than simply bigger. Absent stats are left alone.")]
        public List<StatScale> StatScales = new List<StatScale>();

        [Tooltip("Multiplies every enemy's XP reward. A level that makes its enemies tougher should " +
                 "usually pay more for them, or later floors cost more work for the same progress.")]
        public float XpMultiplier = 1f;

        [Tooltip("Multiplies every enemy's gold reward.")]
        public float GoldMultiplier = 1f;

        [Tooltip("Absolute per-enemy values for this level, overriding the template and the scaling " +
                 "above. For the one enemy a curve does not suit.")]
        public List<EnemyStatOverride> Overrides = new List<EnemyStatOverride>();

        /// <summary>True when this tuning changes nothing, so callers can skip the work entirely.</summary>
        public bool IsIdentity
        {
            get
            {
                return Mathf.Approximately(Difficulty, 1f)
                    && Mathf.Approximately(XpMultiplier, 1f)
                    && Mathf.Approximately(GoldMultiplier, 1f)
                    && (StatScales == null || StatScales.Count == 0)
                    && (Overrides == null || Overrides.Count == 0);
            }
        }

        /// <summary>
        /// The stats this level actually fights <paramref name="enemy"/> with: the template scaled by
        /// <see cref="Difficulty"/>, then by any <see cref="StatScales"/>, then any absolute override.
        ///
        /// <para>A stat the template gives a positive value never scales to zero - a 0.5 multiplier on
        /// a Strength of 1 rounds to 1, not to a harmless enemy - and a stat the template leaves at 0
        /// stays 0, because multiplying nothing is still nothing.</para>
        /// </summary>
        public StatBlock StatsFor(EnemySO enemy)
        {
            if (enemy == null)
            {
                return new StatBlock();
            }

            var stats = enemy.BaseStats != null ? enemy.BaseStats.Clone() : new StatBlock();

            if (!Mathf.Approximately(Difficulty, 1f))
            {
                Scale(stats, StatType.MaxHealth, Difficulty);
                Scale(stats, StatType.Strength, Difficulty);
            }

            if (StatScales != null)
            {
                foreach (var scale in StatScales)
                {
                    if (scale != null && scale.Stat != StatType.None)
                    {
                        Scale(stats, scale.Stat, scale.Multiplier);
                    }
                }
            }

            var over = OverrideFor(enemy);
            if (over != null && over.Stats != null && over.Stats.Values != null)
            {
                foreach (var entry in over.Stats.Values)
                {
                    if (entry != null && entry.Type != StatType.None)
                    {
                        stats[entry.Type] = entry.Amount;
                    }
                }
            }

            return stats;
        }

        /// <summary>XP this level pays for the kill.</summary>
        public int XpFor(EnemySO enemy)
        {
            if (enemy == null)
            {
                return 0;
            }

            var over = OverrideFor(enemy);
            if (over != null && over.XpReward > 0)
            {
                return over.XpReward;
            }

            return ScaleReward(enemy.XpReward, XpMultiplier);
        }

        /// <summary>Gold this level pays for the kill.</summary>
        public int GoldFor(EnemySO enemy)
        {
            if (enemy == null)
            {
                return 0;
            }

            var over = OverrideFor(enemy);
            if (over != null && over.GoldReward > 0)
            {
                return over.GoldReward;
            }

            return ScaleReward(enemy.GoldReward, GoldMultiplier);
        }

        /// <summary>
        /// Multiplier this level applies to the base <c>Power</c> of a spell this enemy casts
        /// (<see cref="EnemySO.MagicCastChance"/>), so its magic escalates across the campaign the
        /// same way its attack does.
        ///
        /// <para>It is <see cref="Difficulty"/>, because that is the dial that scales Strength — the
        /// stat a basic attack swings off. A spell's caster contribution
        /// (<c>SpellEffect.ScalingStat</c>) already rides the scaled stat block, so this covers the
        /// authored base that otherwise would not move.</para>
        ///
        /// <para><b>An enemy with an absolute override does not scale.</b> An
        /// <see cref="Overrides"/> row means "this level's dial does not apply to this enemy" — it is
        /// how bosses are kept off the trash dial — so its spells stay on their authored power for
        /// the same reason its Strength does.</para>
        /// </summary>
        public float MagicPowerScaleFor(EnemySO enemy)
        {
            if (enemy == null)
            {
                return 1f;
            }
            return OverrideFor(enemy) != null ? 1f : Mathf.Max(0.01f, Difficulty);
        }

        private EnemyStatOverride OverrideFor(EnemySO enemy)
        {
            if (Overrides == null)
            {
                return null;
            }
            return Overrides.Find(o => o != null && o.Enemy == enemy);
        }

        private static void Scale(StatBlock stats, StatType stat, float multiplier)
        {
            int value = stats[stat];
            if (value <= 0 || Mathf.Approximately(multiplier, 1f))
            {
                return;
            }
            stats[stat] = Mathf.Max(1, Mathf.RoundToInt(value * multiplier));
        }

        private static int ScaleReward(int value, float multiplier)
        {
            if (value <= 0 || Mathf.Approximately(multiplier, 1f))
            {
                return Mathf.Max(0, value);
            }
            return Mathf.Max(1, Mathf.RoundToInt(value * multiplier));
        }

        // ------------------------------------------------------------------
        //  Static helpers, so callers never have to null-check a tuning
        // ------------------------------------------------------------------

        /// <summary>Stats for an enemy under an optional tuning; null means the template's own.</summary>
        public static StatBlock StatsFor(EnemySO enemy, LevelEnemyTuning tuning)
        {
            if (enemy == null)
            {
                return new StatBlock();
            }
            if (tuning == null)
            {
                return enemy.BaseStats != null ? enemy.BaseStats.Clone() : new StatBlock();
            }
            return tuning.StatsFor(enemy);
        }

        public static int XpFor(EnemySO enemy, LevelEnemyTuning tuning)
        {
            if (enemy == null)
            {
                return 0;
            }
            return tuning != null ? tuning.XpFor(enemy) : enemy.XpReward;
        }

        public static int GoldFor(EnemySO enemy, LevelEnemyTuning tuning)
        {
            if (enemy == null)
            {
                return 0;
            }
            return tuning != null ? tuning.GoldFor(enemy) : enemy.GoldReward;
        }

        /// <summary>Spell power multiplier for an enemy under an optional tuning; 1 for none.</summary>
        public static float MagicPowerScaleFor(EnemySO enemy, LevelEnemyTuning tuning)
        {
            if (enemy == null || tuning == null)
            {
                return 1f;
            }
            return tuning.MagicPowerScaleFor(enemy);
        }
    }
}
