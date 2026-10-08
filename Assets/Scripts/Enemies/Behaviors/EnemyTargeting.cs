using System.Collections.Generic;
using Assets.Scripts.Cards;
using Assets.Scripts.Combat;
using Assets.Scripts.Items;
using Assets.Scripts.UnitStats;
using UnityEngine;

namespace Assets.Scripts.Enemies.Behaviors
{
    /// <summary>Shared target-selection helpers for enemy behaviors.</summary>
    public static class EnemyTargeting
    {
        /// <summary>
        /// A living hero, biased toward whoever has drawn the most threat but never certain - see
        /// <see cref="ThreatTable"/> for the formula. A null table is even odds.
        /// </summary>
        public static ICombatUnit PickByThreat(List<ICombatUnit> units, ThreatTable threat, float roll)
        {
            return ThreatTable.Pick(units, threat, roll);
        }

        /// <summary>Living unit with the lowest health fraction, or null if all are at full health.</summary>
        public static ICombatUnit MostWounded(List<ICombatUnit> units)
        {
            if (units == null)
            {
                return null;
            }

            ICombatUnit best = null;
            float bestRatio = 1f;

            foreach (var unit in units)
            {
                int maxHealth = unit != null ? unit.GetEffectiveStat(StatType.MaxHealth) : 0;
                if (unit == null || !unit.IsAlive || maxHealth <= 0)
                {
                    continue;
                }

                float ratio = (float)unit.Stats.Health / maxHealth;
                if (ratio < bestRatio)
                {
                    bestRatio = ratio;
                    best = unit;
                }
            }

            return best;
        }

        /// <summary>
        /// The living unit with the highest attack power that does not already carry a positive buff on
        /// <paramref name="stat"/>; ties go to the larger health bar (the boss over its escort).
        /// </summary>
        public static ICombatUnit StrongestWithoutBuff(List<ICombatUnit> units, CombatBuffTracker tracker, StatType stat)
        {
            if (units == null)
            {
                return null;
            }

            ICombatUnit best = null;
            foreach (var unit in units)
            {
                if (unit == null || !unit.IsAlive)
                {
                    continue;
                }
                if (tracker != null && tracker.GetBuffAmount(unit, stat) > 0)
                {
                    continue;
                }
                if (best == null
                    || unit.GetEffectiveAttackPower() > best.GetEffectiveAttackPower()
                    || (unit.GetEffectiveAttackPower() == best.GetEffectiveAttackPower()
                        && unit.GetEffectiveStat(StatType.MaxHealth) > best.GetEffectiveStat(StatType.MaxHealth)))
                {
                    best = unit;
                }
            }
            return best;
        }

        /// <summary>
        /// The ally a guard should cover: living, not already under anyone's guard, the lowest health
        /// fraction first and the smallest bar on a tie - so with everyone whole it shields the frailest.
        /// </summary>
        public static ICombatUnit CoverCandidate(List<ICombatUnit> allies, GuardTable guards)
        {
            if (allies == null)
            {
                return null;
            }

            ICombatUnit best = null;
            float bestRatio = float.MaxValue;
            int bestMax = int.MaxValue;
            foreach (var unit in allies)
            {
                int max = unit != null ? unit.GetEffectiveStat(StatType.MaxHealth) : 0;
                if (unit == null || !unit.IsAlive || max <= 0)
                {
                    continue;
                }
                if (guards != null && guards.GuardOf(unit) != null)
                {
                    continue;
                }
                float ratio = (float)unit.Stats.Health / max;
                if (ratio < bestRatio - 0.0001f || (Mathf.Abs(ratio - bestRatio) <= 0.0001f && max < bestMax))
                {
                    best = unit;
                    bestRatio = ratio;
                    bestMax = max;
                }
            }
            return best;
        }

        /// <summary>First living unit that doesn't already have a negative buff on the given stat.</summary>
        public static ICombatUnit FirstWithoutDebuff(List<ICombatUnit> units, CombatBuffTracker tracker, StatType stat)
        {
            if (units == null)
            {
                return null;
            }

            foreach (var unit in units)
            {
                if (unit == null || !unit.IsAlive)
                {
                    continue;
                }

                if (tracker == null || tracker.GetBuffAmount(unit, stat) >= 0)
                {
                    return unit;
                }
            }

            return null;
        }
    }
}
