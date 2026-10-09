using System;
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
    /// (<c>EnemyActionEntry.ApplyChance</c>), rolled once per target. Nothing on the target resists
    /// it yet - a flat chance keeps it readable, and a stat that shrugs debuffs off would be added
    /// here, in one place, if play asks for one.</para>
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
    }
}
