using System;
using System.Collections.Generic;
using Assets.Scripts.Combat;
using UnityEngine;

namespace Assets.Scripts.Cards.Effects
{
    /// <summary>
    /// <see cref="SpellEffectType.Intercept"/>: the caster steps in front of each ally target - single-target
    /// blows aimed at them land on the caster instead until the caster's next turn (<see cref="GuardTable.Cover"/>)
    /// - and braces, taking <c>Power</c> percent less from every hit for one turn
    /// (<see cref="CombatBuffTracker.ApplyResistanceToAll"/>). The Paladin's Divine Intervention.
    /// </summary>
    public class InterceptEffectExecutor : IEffectExecutor
    {
        private static readonly Color InterceptColor = new Color(1f, 0.9f, 0.55f);
        private const float EffectDelay = 0.2f;

        private readonly Func<CombatEvents> _events;

        public InterceptEffectExecutor(Func<CombatEvents> events)
        {
            _events = events;
        }

        public void Execute(SpellEffect effect, ICombatUnit caster, List<ICombatUnit> targets,
            CombatBuffTracker buffTracker, EffectResult result, bool flatPower = false)
        {
            if (caster == null || !caster.IsAlive || targets == null)
            {
                return;
            }
            var events = _events?.Invoke();
            foreach (var target in targets)
            {
                if (target == null || !target.IsAlive || ReferenceEquals(target, caster))
                {
                    continue;
                }
                events?.Guards.Cover(target, caster);
                result.Entries.Add(new EffectEntry
                {
                    Target = target,
                    Text = "Protected",
                    Color = InterceptColor,
                    Delay = EffectDelay
                });
            }
            if (effect.Power > 0)
            {
                buffTracker?.ApplyResistanceToAll(caster, effect.Power, 1);
            }
        }
    }

    /// <summary>
    /// <see cref="SpellEffectType.Retaliate"/>: readies the caster to strike back - the first enemy blow
    /// that lands on them before their next turn is answered with <c>Power</c> + the caster's
    /// <c>ScalingStat</c> as <c>DamageType</c> (<see cref="RetaliationTable"/>, fired by
    /// <see cref="RetaliationTracker"/>). The second half of Divine Intervention.
    /// </summary>
    public class RetaliateEffectExecutor : IEffectExecutor
    {
        private readonly Func<CombatEvents> _events;

        public RetaliateEffectExecutor(Func<CombatEvents> events)
        {
            _events = events;
        }

        public void Execute(SpellEffect effect, ICombatUnit caster, List<ICombatUnit> targets,
            CombatBuffTracker buffTracker, EffectResult result, bool flatPower = false)
        {
            var events = _events?.Invoke();
            if (caster == null || !caster.IsAlive || events == null)
            {
                return;
            }
            var strike = effect.Clone();
            strike.EffectType = SpellEffectType.Damage;
            events.Retaliations.Arm(caster, strike);
        }
    }
}
