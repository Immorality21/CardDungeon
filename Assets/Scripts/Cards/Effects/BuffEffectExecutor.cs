using System.Collections.Generic;
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
            foreach (var target in targets)
            {
                if (!target.IsAlive)
                {
                    continue;
                }

                if (effect.BuffType == BuffType.Frozen)
                {
                    buffTracker.ApplyStatusEffect(target, BuffType.Frozen, effect.Duration);
                    result.Entries.Add(new EffectEntry
                    {
                        Target = target,
                        Text = "Frozen!",
                        Color = BuffColor,
                        Delay = EffectDelay
                    });
                }
                else
                {
                    var stat = BuffTypeMapper.ToStatType(effect.BuffType);
                    if (stat.HasValue)
                    {
                        buffTracker.ApplyBuff(target, stat.Value, effect.Power, effect.Duration);
                    }

                    result.Entries.Add(new EffectEntry
                    {
                        Target = target,
                        Text = $"+{effect.Power} {effect.BuffType}",
                        Color = BuffColor,
                        Delay = EffectDelay
                    });
                }
            }
        }
    }
}
