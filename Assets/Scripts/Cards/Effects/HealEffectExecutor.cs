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

        public void Execute(
            SpellEffect effect,
            ICombatUnit caster,
            List<ICombatUnit> targets,
            CombatBuffTracker buffTracker,
            EffectResult result,
            bool flatPower = false)
        {
            // Healing scales off the caster the same way damage does, so a Spirit build actually
            // heals for more. Flat-power heals stay as authored, matching flat damage.
            int scaled = flatPower
                ? effect.Power
                : effect.Power + SpellScaling.CasterContribution(caster, effect.ScalingStat, buffTracker);

            foreach (var target in targets)
            {
                if (!target.IsAlive)
                {
                    continue;
                }

                int healAmount = scaled;
                int newHealth = Mathf.Min(
                    target.Stats.Health + healAmount, target.GetEffectiveStat(StatType.MaxHealth));
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
