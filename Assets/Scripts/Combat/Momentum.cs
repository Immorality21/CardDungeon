using System;
using UnityEngine;

namespace Assets.Scripts.Combat
{
    /// <summary>
    /// The Warrior's signature (his vision, 2026-10-10): every turn in a row on which he lands a hit
    /// adds a stack, each stack worth <see cref="PercentPerStack"/> of his Strength for the rest of the
    /// fight, up to <see cref="MaxStacks"/>; a turn on which he lands nothing - he guarded, healed,
    /// missed, was frozen - drops them all. The Paladin endures, the Warrior builds. Authored on the
    /// hero (<c>HeroSO.Momentum</c>), worked by <c>Cards.MomentumTracker</c> off the fight's events.
    /// </summary>
    [Serializable]
    public class MomentumRule
    {
        [Tooltip("Strength added per stack, as a percentage of the hero's own Strength (at least +1 a " +
                 "stack). 0 = this hero has no Momentum.")]
        [Min(0)] public int PercentPerStack;

        [Tooltip("The most stacks one fight can build.")]
        [Min(0)] public int MaxStacks;

        public bool IsActive => PercentPerStack > 0 && MaxStacks > 0;
    }

    /// <summary>A unit that may build Momentum: a hero (from their definition) and the simulator's stand-in.</summary>
    public interface IHasMomentum
    {
        /// <summary>The unit's rule, or null / inactive for a unit without one.</summary>
        MomentumRule Momentum { get; }
    }

    /// <summary>The pure arithmetic of <see cref="MomentumRule"/>. Covered by <c>MomentumTests</c>.</summary>
    public static class MomentumOps
    {
        /// <summary>The stacks after a turn: one more (capped) when a hit landed, none when it did not.</summary>
        public static int Next(int stacks, bool landedHit, MomentumRule rule)
        {
            if (rule == null || !rule.IsActive || !landedHit)
            {
                return 0;
            }
            return Mathf.Clamp(stacks + 1, 0, rule.MaxStacks);
        }

        /// <summary>
        /// The Strength <paramref name="stacks"/> are worth to a hero with <paramref name="strength"/>:
        /// <see cref="MomentumRule.PercentPerStack"/> of it per stack, rounded, and never less than one
        /// point a stack - so a stack always shows on a small number.
        /// </summary>
        public static int StrengthBonus(int strength, int stacks, MomentumRule rule)
        {
            if (rule == null || !rule.IsActive || stacks <= 0)
            {
                return 0;
            }
            int percent = Mathf.RoundToInt(Mathf.Max(0, strength) * rule.PercentPerStack * stacks / 100f);
            return Mathf.Max(stacks, percent);
        }
    }
}
