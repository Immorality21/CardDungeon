using System.Collections.Generic;
using Assets.Scripts.Combat;
using UnityEngine;

namespace Assets.Scripts.Cards.Effects
{
    /// <summary>
    /// Charges the caster health as the price of a spell — what makes a defensive cast like Fire
    /// Cloak a decision rather than a free buff. Ignores <paramref name="targets"/> entirely: the
    /// cost is always paid by whoever cast, whatever the magic's target type says.
    ///
    /// <para>The cast is gated on affordability before it is ever submitted
    /// (<see cref="SpellPower.CanAfford"/>), so this keeps a <b>1 HP floor</b> purely as a safety
    /// net: a bug must not be able to kill a hero through their own spell, because the cast path has
    /// no death handling to run them through.</para>
    /// </summary>
    public class HealthCostEffectExecutor : IEffectExecutor
    {
        private static readonly Color CostColor = new Color(0.9f, 0.35f, 0.35f);
        private const float EffectDelay = 0.2f;

        public void Execute(
            SpellEffect effect,
            ICombatUnit caster,
            List<ICombatUnit> targets,
            CombatBuffTracker buffTracker,
            EffectResult result,
            bool flatPower = false)
        {
            if (caster == null || !caster.IsAlive)
            {
                return;
            }

            int cost = SpellPower.ResolveHealthCost(effect, caster);
            if (cost <= 0)
            {
                return;
            }

            int paid = Mathf.Min(cost, Mathf.Max(0, caster.Stats.Health - 1));
            if (paid <= 0)
            {
                return;
            }

            caster.Stats.Health -= paid;

            result.Entries.Add(new EffectEntry
            {
                Target = caster,
                Text = $"-{paid} HP",
                Color = CostColor,
                Delay = EffectDelay
            });
        }
    }
}
