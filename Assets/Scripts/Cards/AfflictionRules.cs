using System;
using Assets.Scripts.Combat;
using UnityEngine;

namespace Assets.Scripts.Cards
{
    /// <summary>
    /// Whether a debuff takes hold - the one place that question is answered, so every path that lands
    /// one rolls it the same way: an ability or an enemy spell (<c>DebuffEffectExecutor</c>, and so
    /// combos, summons and triggered reactions too), and an enemy's Debuff action
    /// (<c>CombatManager</c> and the simulator). The balance model prices a debuff at
    /// <see cref="Chance"/> of its full value, through the same number.
    ///
    /// <para><b>Every debuff, not only statuses</b> (owner, 2026-10-09): a Silence, a Blind, a Burn
    /// or a Poison that always lands makes a fight predictable, and a solved one. The chance is
    /// authored per effect (<see cref="SpellEffect.ApplyChance"/>) and per enemy action
    /// (<c>EnemyActionEntry.ApplyChance</c>), rolled once per target. A hero's worn
    /// <see cref="StatusWard"/>s cut a status's chance (<see cref="ChanceAgainst"/>, 2026-11-14); stat
    /// cuts are never warded.</para>
    ///
    /// <para>Immunity is a separate, earlier question (<see cref="StatusImmunity"/>): an immune unit is
    /// never rolled for.</para>
    /// </summary>
    public static class AfflictionRules
    {
        /// <summary>The chance that always lands, and the default for every authored debuff.</summary>
        public const int Certain = 100;

        /// <summary>
        /// The roll every affliction check uses, uniform in [0, 1). A seam for tests only - production
        /// code never assigns it.
        /// </summary>
        public static Func<float> Roll = () => UnityEngine.Random.Range(0f, 1f);

        /// <summary>An authored percent as a probability, clamped to [0, 1].</summary>
        public static float Chance(int percent)
        {
            return Mathf.Clamp01(percent / 100f);
        }

        /// <summary>Rolls whether a debuff authored at <paramref name="percent"/> takes hold.</summary>
        public static bool Lands(int percent)
        {
            if (percent >= Certain)
            {
                return true;
            }
            return percent > 0 && Roll() < Chance(percent);
        }

        /// <summary>
        /// The chance a debuff authored at <paramref name="percent"/> lands on this target, after its
        /// partial guard (<see cref="StatusImmunity.Resistance"/>): a 75% poison on a hero with a 50%
        /// ward is 37.5%. Immunity is asked before this, by the callers.
        /// </summary>
        public static float ChanceAgainst(int percent, ICombatUnit target, BuffType type)
        {
            float chance = percent >= Certain ? 1f : Chance(percent);
            int resist = StatusImmunity.Resistance(target, type);
            return chance * (1f - resist / 100f);
        }

        /// <summary>Rolls <see cref="ChanceAgainst"/>: the target's ward cuts even a certain debuff.</summary>
        public static bool Lands(int percent, ICombatUnit target, BuffType type)
        {
            if (StatusImmunity.Resistance(target, type) <= 0)
            {
                return Lands(percent);
            }
            float chance = ChanceAgainst(percent, target, type);
            return chance > 0f && Roll() < chance;
        }
    }
}
