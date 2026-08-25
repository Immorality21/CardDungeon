using System.Collections.Generic;
using Assets.Scripts.Cards.Buffs;
using Assets.Scripts.Combat;
using Assets.Scripts.Items;
using Assets.Scripts.UnitStats;
using UnityEngine;

namespace Assets.Scripts.Cards.Effects
{
    public class DamageEffectExecutor : IEffectExecutor
    {
        private static readonly Color DamageColor = Color.white;
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
            foreach (var target in targets)
            {
                if (!target.IsAlive)
                {
                    continue;
                }

                // Resolved per target, because PowerMode.PercentOfMaxHealth reads the bar of the unit
                // the effect lands on. Flat power comes from the definition (a combo's bonus effect, a
                // room event's outcome) rather than from whoever happened to trigger it.
                int rawAttack = SpellPower.Resolve(effect, caster, target, buffTracker, flatPower);

                int defenseBonus = buffTracker.GetBuffAmount(target, StatType.Endurance);
                int defense = target.GetEffectiveStat(StatType.Endurance) + defenseBonus;
                float resistanceBonus = buffTracker.GetResistanceBonus(target, effect.DamageType);
                int damage = DamageCalculator.Calculate(
                    rawAttack, defense, effect.DamageType, target.Resistances, resistanceBonus);

                if (damage < 0)
                {
                    int heal = Mathf.Min(
                        -damage, target.GetEffectiveStat(StatType.MaxHealth) - target.Stats.Health);
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

                    foreach (var statusEffect in buffTracker.GetActiveStatusEffects(target))
                    {
                        var handler = BuffHandlerRegistry.Get(statusEffect);
                        if (handler != null && handler.IsRemovedByDamageType(effect.DamageType))
                        {
                            buffTracker.RemoveStatusEffect(target, statusEffect);
                        }
                    }

                    result.Entries.Add(new EffectEntry
                    {
                        Target = target,
                        Text = damage.ToString(),
                        Color = DamageColor,
                        Delay = EffectDelay,
                        Impact = damage,
                        Effectiveness = DamageCalculator.Classify(
                            effect.DamageType, target.Resistances, resistanceBonus)
                    });
                }
            }
        }
    }
}
