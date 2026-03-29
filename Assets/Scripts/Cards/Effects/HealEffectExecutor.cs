using System.Collections.Generic;
using Assets.Scripts.Combat;
using UnityEngine;

namespace Assets.Scripts.Cards.Effects
{
    public class HealEffectExecutor : IEffectExecutor
    {
        private static readonly Color HealColor = Color.green;
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

                int healAmount = effect.Power;
                int newHealth = Mathf.Min(target.Stats.Health + healAmount, target.Stats.MaxHealth);
                int actualHeal = newHealth - target.Stats.Health;
                target.Stats.Health = newHealth;

                result.Entries.Add(new EffectEntry
                {
                    Target = target,
                    Text = actualHeal.ToString(),
                    Color = HealColor,
                    Delay = EffectDelay
                });
            }
        }
    }
}
