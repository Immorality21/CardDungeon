using System;
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

        private readonly Func<CombatEvents> _events;

        /// <param name="events">The fight's event stream, read when the effect lands. Null (a room
        /// event, a test) changes health without telling anyone.</param>
        public HealthCostEffectExecutor(Func<CombatEvents> events = null)
        {
            _events = events;
        }

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

            int paid = HealthOps.Pay(
                caster, cost, new HealthSource(caster, HealthCause.Cost), _events?.Invoke()).Landed;
            if (paid <= 0)
            {
                return;
            }

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
