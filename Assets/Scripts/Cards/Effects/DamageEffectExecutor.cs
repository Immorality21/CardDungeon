using System.Collections.Generic;
using Assets.Scripts.Combat;
using Assets.Scripts.Items;
using UnityEngine;

namespace Assets.Scripts.Cards.Effects
{
    public class DamageEffectExecutor : IEffectExecutor
    {
        private static readonly Color DamageColor = Color.white;
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
            int rawAttack;
            if (isComboEffect)
            {
                rawAttack = effect.Power;
            }
            else
            {
                int attackBonus = buffTracker.GetBuffAmount(caster, StatType.Attack);
                rawAttack = caster.GetEffectiveAttack() + attackBonus + effect.Power;
            }

            foreach (var target in targets)
            {
                if (!target.IsAlive)
                {
                    continue;
                }

                int defenseBonus = buffTracker.GetBuffAmount(target, StatType.Defense);
                int defense = target.GetEffectiveDefense() + defenseBonus;
                int damage = DamageCalculator.Calculate(rawAttack, defense, effect.DamageType, target.Resistances);

                if (damage < 0)
                {
                    int heal = Mathf.Min(-damage, target.Stats.MaxHealth - target.Stats.Health);
                    target.Stats.Health += heal;
                    result.Entries.Add(new EffectEntry
                    {
                        Target = target,
                        Text = $"+{heal}",
                        Color = HealColor,
                        Delay = EffectDelay
                    });
                }
                else
                {
                    target.Stats.Health -= damage;

                    // Fire damage thaws frozen targets
                    if (effect.DamageType == DamageType.Fire)
                    {
                        buffTracker.RemoveStatusEffect(target, BuffType.Frozen);
                    }

                    result.Entries.Add(new EffectEntry
                    {
                        Target = target,
                        Text = damage.ToString(),
                        Color = DamageColor,
                        Delay = EffectDelay
                    });
                }
            }
        }
    }
}
