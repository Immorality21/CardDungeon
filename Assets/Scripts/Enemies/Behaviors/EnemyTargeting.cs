using System.Collections.Generic;
using Assets.Scripts.Cards;
using Assets.Scripts.Combat;
using Assets.Scripts.Items;
using UnityEngine;

namespace Assets.Scripts.Enemies.Behaviors
{
    /// <summary>Shared target-selection helpers for enemy behaviors.</summary>
    public static class EnemyTargeting
    {
        public static ICombatUnit PickRandom(List<ICombatUnit> units)
        {
            if (units == null || units.Count == 0)
            {
                return null;
            }
            return units[Random.Range(0, units.Count)];
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
                if (unit == null || !unit.IsAlive || unit.Stats.MaxHealth <= 0)
                {
                    continue;
                }

                float ratio = (float)unit.Stats.Health / unit.Stats.MaxHealth;
                if (ratio < bestRatio)
                {
                    bestRatio = ratio;
                    best = unit;
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
