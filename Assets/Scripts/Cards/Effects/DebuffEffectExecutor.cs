using System.Collections.Generic;
using Assets.Scripts.Cards.Buffs;
using Assets.Scripts.Combat;
using UnityEngine;

namespace Assets.Scripts.Cards.Effects
{
    public class DebuffEffectExecutor : IEffectExecutor
    {
        private static readonly Color DebuffColor = new Color(0.8f, 0.2f, 0.8f);
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

                handler.Apply(target, -effect.Power, effect.Duration, buffTracker);

                result.Entries.Add(new EffectEntry
                {
                    Target = target,
                    Text = handler.GetDisplayText(-effect.Power),
                    Color = DebuffColor,
                    Delay = EffectDelay
                });
            }
        }
    }
}
