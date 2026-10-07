using System;
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

        private readonly Func<CombatEvents> _events;

        /// <param name="events">The fight's event stream, read when the effect lands. Null (a room
        /// event, a test) changes health without telling anyone.</param>
        public DamageEffectExecutor(Func<CombatEvents> events = null)
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

                // Resolved per target, because PowerMode.PercentOfMaxHealth reads the bar of the unit
                // the effect lands on. Flat power comes from the definition (a combo's bonus effect, a
                // room event's outcome) rather than from whoever happened to trigger it.
                int rawAttack = SpellPower.Resolve(effect, caster, target, buffTracker, flatPower);

                // Magic (an Intelligence- or Spirit-scaled effect) is met by Spirit, everything else
                // by Endurance — DefenseRules owns the split.
                var defenseStat = DefenseRules.DefenseStatFor(effect.ScalingStat);
                int defenseBonus = buffTracker.GetBuffAmount(target, defenseStat);
                int defense = target.GetEffectiveStat(defenseStat) + defenseBonus;
                float resistanceBonus = buffTracker.GetResistanceBonus(target, effect.DamageType);
                int damage = DamageCalculator.Calculate(
                    rawAttack, defense, effect.DamageType, target.Resistances, resistanceBonus);

                var source = new HealthSource(caster, HealthCause.Ability, effect.DamageType);
                if (damage < 0)
                {
                    int heal = HealthOps.Damage(target, damage, source, _events?.Invoke()).Healed;
                    result.Entries.Add(new EffectEntry
                    {
                        Target = target,
                        Text = $"+{heal}",
                        Healed = heal,
                        Color = HealColor,
                        Delay = EffectDelay
                    });
                }
                else
                {
                    int landed = HealthOps.Damage(target, damage, source, _events?.Invoke()).Landed;

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
                        Landed = landed,
                        Effectiveness = DamageCalculator.Classify(
                            effect.DamageType, target.Resistances, resistanceBonus)
                    });
                }
            }
        }
    }
}
