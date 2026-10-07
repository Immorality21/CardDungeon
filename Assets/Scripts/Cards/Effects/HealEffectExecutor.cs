using System;
using System.Collections.Generic;
using Assets.Scripts.Combat;
using UnityEngine;
using Assets.Scripts.UnitStats;

namespace Assets.Scripts.Cards.Effects
{
    public class HealEffectExecutor : IEffectExecutor
    {
        private static readonly Color HealColor = Color.green;
        private const float EffectDelay = 0.2f;

        private readonly Func<CombatEvents> _events;

        /// <param name="events">The fight's event stream, read when the effect lands. Null (a room
        /// event, a test) changes health without telling anyone.</param>
        public HealEffectExecutor(Func<CombatEvents> events = null)
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
            foreach (var target in targets)
            {
                if (!target.IsAlive)
                {
                    continue;
                }

                // Healing scales off the caster the same way damage does, so a Spirit build actually
                // heals for more. Flat-power heals stay as authored, matching flat damage; a
                // percentage heal reads the bar of whoever it lands on, so it is resolved per target.
                int healAmount = RunFear.Current.ScaleHealing(
                    target, SpellPower.Resolve(effect, caster, target, buffTracker, flatPower));
                int actualHeal = HealthOps.Heal(
                    target, healAmount, new HealthSource(caster, HealthCause.Ability), _events?.Invoke()).Healed;

                result.Entries.Add(new EffectEntry
                {
                    Target = target,
                    Text = actualHeal.ToString(),
                    Healed = actualHeal,
                    Color = HealColor,
                    Delay = EffectDelay
                });
            }
        }
    }
}
