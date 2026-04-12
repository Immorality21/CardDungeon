using System.Collections.Generic;
using Assets.Scripts.Cards.Buffs;
using Assets.Scripts.Combat;
using UnityEngine;

namespace Assets.Scripts.Cards.Effects
{
    public class BuffEffectExecutor : IEffectExecutor
    {
        private static readonly Color BuffColor = Color.cyan;
        private const float EffectDelay = 0.2f;

        public void Execute(
            CardEffect effect,
            ICombatUnit caster,
            List<ICombatUnit> targets,
            CombatBuffTracker buffTracker,
            CardEffectResult result,
            bool isComboEffect = false)
        {
            var handler = BuffHandlerRegistry.Get(effect.BuffType);

            foreach (var target in targets)
            {
                if (!target.IsAlive)
                {
                    continue;
                }

                handler.Apply(target, effect.Power, effect.Duration, buffTracker);

                result.Entries.Add(new EffectEntry
                {
                    Target = target,
                    Text = handler.GetDisplayText(effect.Power),
                    Color = BuffColor,
                    Delay = EffectDelay
                });
            }
        }
    }
}
