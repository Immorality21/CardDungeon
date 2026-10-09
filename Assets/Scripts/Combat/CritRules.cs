using System;
using Assets.Scripts.UnitStats;
using UnityEngine;

namespace Assets.Scripts.Combat
{
    /// <summary>
    /// Critical hits - how likely, how hard, and the one roll. Basic attacks always may crit; an
    /// ability may only when it is authored to (<c>MagicSO.CanCrit</c>) or its caster's grid bought a
    /// crit upgrade for it (<c>AbilityGrant</c>), see <c>EffectResolver</c>.
    ///
    /// <para>Lived on <c>CombatManager</c> until abilities could crit (2026-10-09); the cast path
    /// cannot reach a scene manager, so the numbers moved here and <c>CombatManager</c> forwards them.</para>
    ///
    /// <para><b>Anything that rolls or models a crit must go through <see cref="ChanceFor"/></b>, or
    /// Luck silently does nothing.</para>
    /// </summary>
    public static class CritRules
    {
        /// <summary>Crit chance at zero Luck. Luck adds to this - see <see cref="ChanceFor"/>.</summary>
        public const float BaseChance = 0.12f;

        /// <summary>Most crit chance Luck can add on top of <see cref="BaseChance"/>.</summary>
        public const float MaxLuckBonus = 0.30f;

        /// <summary>
        /// Luck at which half of <see cref="MaxLuckBonus"/> is reached. Diminishing rather than
        /// linear, the stat/(stat+K) curve the defense and dodge rules use, so Luck cannot run away.
        /// </summary>
        public const float LuckConstant = 20f;

        /// <summary>Damage multiplier on a crit.</summary>
        public const float Multiplier = 1.6f;

        /// <summary>
        /// The roll every crit check uses, uniform in [0, 1). A seam for tests only - production code
        /// never assigns it.
        /// </summary>
        public static Func<float> Roll = () => UnityEngine.Random.Range(0f, 1f);

        /// <summary>Crit chance for a specific unit: the base rate plus its Luck on the curve.</summary>
        public static float ChanceFor(ICombatUnit unit)
        {
            if (unit == null)
            {
                return BaseChance;
            }
            float luck = Mathf.Max(0, unit.GetEffectiveStat(StatType.Luck));
            return BaseChance + MaxLuckBonus * (luck / (luck + LuckConstant));
        }

        /// <summary>
        /// Rolls a crit for <paramref name="attacker"/>, with <paramref name="bonus"/> (a probability, from
        /// an ability's crit upgrades) on top of its Luck-based chance.
        /// </summary>
        public static bool RollCrit(ICombatUnit attacker, float bonus = 0f)
        {
            return Roll() < ChanceFor(attacker) + bonus;
        }

        /// <summary>
        /// A positive hit made critical: the multiplier, and always at least one more than it was, so a
        /// crit on a 1-damage hit still reads as one. Zero and absorbed (negative) hits are untouched.
        /// </summary>
        public static int Apply(int damage)
        {
            return damage > 0 ? Mathf.Max(damage + 1, Mathf.RoundToInt(damage * Multiplier)) : damage;
        }
    }
}
