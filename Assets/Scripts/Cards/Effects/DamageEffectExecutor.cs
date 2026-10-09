using System;
using System.Collections.Generic;
using Assets.Scripts.Cards.Buffs;
using Assets.Scripts.Combat;
using Assets.Scripts.Items;
using Assets.Scripts.UnitStats;
using UnityEngine;

namespace Assets.Scripts.Cards.Effects
{
    public class DamageEffectExecutor : ICastAwareEffectExecutor
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

        /// <summary>A cast: the targets the resolver rolled a crit for are struck critically.</summary>
        public void Execute(
            SpellEffect effect,
            SpellcastAction action,
            List<ICombatUnit> targets,
            CombatBuffTracker buffTracker,
            EffectResult result)
        {
            Resolve(effect, action.Caster, targets, buffTracker, result, false, action.CritTargets);
        }

        /// <summary>No cast to read (a combo's bonus, a reaction, a room event): never a crit.</summary>
        public void Execute(
            SpellEffect effect,
            ICombatUnit caster,
            List<ICombatUnit> targets,
            CombatBuffTracker buffTracker,
            EffectResult result,
            bool flatPower = false)
        {
            Resolve(effect, caster, targets, buffTracker, result, flatPower, null);
        }

        private void Resolve(
            SpellEffect effect,
            ICombatUnit caster,
            List<ICombatUnit> targets,
            CombatBuffTracker buffTracker,
            EffectResult result,
            bool flatPower,
            List<ICombatUnit> critTargets)
        {
            foreach (var aimed in targets)
            {
                if (!aimed.IsAlive)
                {
                    continue;
                }

                // A guarded unit's blows land on its guard (a mech its rider, GuardTable). When the
                // guard is a target of this same effect it is struck on its own, so a blast that
                // reaches both does not hit the mech twice. An enemy's cover only intercepts
                // single-target blows, so an area effect reaches the covered unit (GuardTable).
                var guards = _events?.Invoke()?.Guards;
                var target = guards == null
                    ? aimed
                    : targets.Count > 1 ? guards.RedirectAreaHit(aimed) : guards.Redirect(aimed);
                if (!ReferenceEquals(target, aimed) && targets.Contains(target))
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

                // The crit was rolled against the unit the caster aimed at, so a blow its guard takes
                // instead is still the critical one.
                bool crit = damage > 0 && critTargets != null && critTargets.Contains(aimed);
                if (crit)
                {
                    damage = CritRules.Apply(damage);
                }

                var source = new HealthSource(caster, HealthCause.Ability, effect.DamageType, crit);
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
                        Crit = crit,
                        Effectiveness = DamageCalculator.Classify(
                            effect.DamageType, target.Resistances, resistanceBonus)
                    });
                }
            }
        }
    }
}
