using System;
using Assets.Scripts.UnitStats;
using UnityEngine;

namespace Assets.Scripts.Combat
{
    /// <summary>
    /// Which stat defends against a hit, and whether the hit can be dodged — the one place both
    /// questions are answered, so the game and the balance model cannot drift *(decided 2026-10-03)*.
    ///
    /// <para><b>Physical and magic are told apart by the stat the effect scales off.</b> An effect
    /// scaled by Intelligence or Spirit is magic and Spirit defends against it; everything else —
    /// basic attacks, Strength/Agility abilities, flat damage — is physical and Endurance defends.
    /// The split is clean, not blended: a high-Endurance front-liner is soft against casters, and a
    /// stone construct with no Spirit is weak to magic. That is the point — it gives Spirit a
    /// defensive job beside healing.</para>
    ///
    /// <para><b>Luck dodges physical hits, never magic</b> (the FFX model). The chance follows the
    /// same stat/(stat+K) curve as crit and defense, so Luck never runs away and there is one curve
    /// to learn. Base chance is 0: a unit with no Luck never dodges, which is every enemy that is not
    /// authored to. Damage-over-time ticks, combo bonuses and room-event damage are not attacks and
    /// cannot be dodged.</para>
    /// </summary>
    public static class DefenseRules
    {
        /// <summary>Ceiling the dodge chance approaches as Luck grows.</summary>
        public const float MaxLuckDodge = 0.30f;

        /// <summary>Luck at which half of <see cref="MaxLuckDodge"/> is reached (10 Luck ≈ 10%).</summary>
        public const float LuckDodgeConstant = 20f;

        /// <summary>
        /// The roll every dodge check uses, uniform in [0, 1). A seam for tests only — production code
        /// never assigns it.
        /// </summary>
        public static Func<float> Roll = () => UnityEngine.Random.Range(0f, 1f);

        /// <summary>True for a stat whose scaled effects are magic: Intelligence and Spirit.</summary>
        public static bool IsMagic(StatType scalingStat)
        {
            return scalingStat == StatType.Intelligence || scalingStat == StatType.Spirit;
        }

        /// <summary>The stat that defends against an effect scaled by <paramref name="scalingStat"/>.</summary>
        public static StatType DefenseStatFor(StatType scalingStat)
        {
            return IsMagic(scalingStat) ? StatType.Spirit : StatType.Endurance;
        }

        /// <summary>Chance <paramref name="target"/> dodges a physical hit.</summary>
        public static float DodgeChanceFor(ICombatUnit target)
        {
            if (target == null)
            {
                return 0f;
            }
            float luck = Mathf.Max(0, target.GetEffectiveStat(StatType.Luck));
            return MaxLuckDodge * (luck / (luck + LuckDodgeConstant));
        }

        /// <summary>Rolls a dodge for a physical hit on <paramref name="target"/>.</summary>
        public static bool RollDodge(ICombatUnit target)
        {
            float chance = DodgeChanceFor(target);
            return chance > 0f && Roll() < chance;
        }
    }
}
