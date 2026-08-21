using System;
using System.Collections.Generic;
using Assets.Scripts.UnitStats;
using UnityEngine;

namespace Assets.Scripts.Balance
{
    /// <summary>
    /// How much one point of a stat is worth in the power score. Design intent, not a fact about the
    /// game, which is why it lives in <see cref="BalanceRulesSO"/> as editable data rather than as
    /// constants — and as a list rather than a field per stat, so a new <see cref="StatType"/> costs
    /// nothing here.
    /// </summary>
    [Serializable]
    public class StatWeight
    {
        public StatType Stat;
        public float Weight;

        public StatWeight() { }

        public StatWeight(StatType stat, float weight)
        {
            Stat = stat;
            Weight = weight;
        }

        /// <summary>
        /// Seeded from <see cref="StatCatalog"/> so the rules asset starts from the same per-stat
        /// table everything else reads, and a new stat arrives with a weight instead of a zero.
        /// Designers can still tune the asset afterwards - that is the point of it being data.
        /// </summary>
        public static List<StatWeight> Defaults()
        {
            var weights = new List<StatWeight>();
            foreach (var definition in StatCatalog.All)
            {
                weights.Add(new StatWeight(definition.Type, definition.PowerWeight));
            }
            return weights;
        }
    }
}
