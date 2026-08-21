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
            SpellEffect effect,
            ICombatUnit caster,
            List<ICombatUnit> targets,
            CombatBuffTracker buffTracker,
            EffectResult result,
            bool isComboEffect = false)
        {
            // Healing scales off the caster the same way damage does, so a Spirit build actually
            // heals for more. Combo heals stay flat, matching combo damage.
            int scaled = isComboEffect
                ? effect.Power
                : effect.Power + SpellScaling.CasterContribution(caster, effect.ScalingStat, buffTracker);

            foreach (var target in targets)
            {
                if (!target.IsAlive)
                {
                    continue;
                }

                int healAmount = scaled;
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
