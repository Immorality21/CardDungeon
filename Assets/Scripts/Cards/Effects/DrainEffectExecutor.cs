using System;
using System.Collections.Generic;
using Assets.Scripts.Combat;
using Assets.Scripts.UnitStats;
using UnityEngine;

namespace Assets.Scripts.Cards.Effects
{
    /// <summary>
    /// Heals the caster <c>Power</c> percent of the damage the cast has dealt so far - read off the
    /// result's damage entries, so it composes with any Damage effect rather than duplicating one.
    /// <see cref="EffectResolver"/> runs it after every other benefit, which is what "so far" means:
    /// the whole cast's damage, whatever order the effects were authored in.
    ///
    /// <para>Only health actually taken counts (<see cref="EffectEntry.Landed"/>): overkill, damage
    /// the caster took and absorbed hits count for nothing. Rounded down with a floor of 1 when
    /// anything landed, and clamped at the caster's maximum like any heal - a drain at full health
    /// floats nothing.</para>
    /// </summary>
    public class DrainEffectExecutor : IEffectExecutor
    {
        private static readonly Color DrainColor = new Color(0.75f, 0.2f, 0.35f);
        private const float EffectDelay = 0.3f;

        private readonly Func<CombatEvents> _events;
        private readonly Func<Func<ICombatUnit, List<ICombatUnit>>> _living;

        /// <param name="events">The fight's event stream, read when the effect lands. Null (a room
        /// event, a test) changes health without telling anyone.</param>
        /// <param name="living">The living units on a caster's side, for a drain shared with the party
        /// (<see cref="SpellEffect.DrainToParty"/>). Null heals the caster alone.</param>
        public DrainEffectExecutor(
            Func<CombatEvents> events = null,
            Func<Func<ICombatUnit, List<ICombatUnit>>> living = null)
        {
            _events = events;
            _living = living;
        }

        public void Execute(
            SpellEffect effect,
            ICombatUnit caster,
            List<ICombatUnit> targets,
            CombatBuffTracker buffTracker,
            EffectResult result,
            bool flatPower = false)
        {
            if (caster == null || !caster.IsAlive || effect.Power <= 0)
            {
                return;
            }

            int drained = Amount(effect.Power, DamageDealt(result, caster));
            if (drained <= 0)
            {
                return;
            }

            foreach (var unit in Recipients(effect, caster))
            {
                int heal = RunFear.Current.ScaleHealing(unit, drained);
                if (heal <= 0)
                {
                    continue;
                }
                int healed = HealthOps.Heal(
                    unit, heal, new HealthSource(caster, HealthCause.Ability), _events?.Invoke()).Healed;
                if (healed <= 0)
                {
                    continue;
                }
                result.Entries.Add(new EffectEntry
                {
                    Target = unit,
                    Text = $"+{healed}",
                    Healed = healed,
                    Color = DrainColor,
                    Delay = EffectDelay
                });
            }
        }

        /// <summary>Who the drain heals: the caster, or with <see cref="SpellEffect.DrainToParty"/>
        /// every living unit on the caster's side (the caster always among them).</summary>
        private List<ICombatUnit> Recipients(SpellEffect effect, ICombatUnit caster)
        {
            var recipients = new List<ICombatUnit> { caster };
            var living = effect.DrainToParty ? _living?.Invoke() : null;
            if (living == null)
            {
                return recipients;
            }
            foreach (var unit in living(caster))
            {
                if (unit != null && unit.IsAlive && !recipients.Contains(unit))
                {
                    recipients.Add(unit);
                }
            }
            return recipients;
        }

        /// <summary>What <paramref name="percent"/> of <paramref name="damage"/> drains: rounded
        /// down, at least 1 when any damage landed.</summary>
        public static int Amount(int percent, int damage)
        {
            if (percent <= 0 || damage <= 0)
            {
                return 0;
            }
            return Mathf.Max(1, Mathf.FloorToInt(damage * percent / 100f));
        }

        /// <summary>Health the cast has taken off anyone but the caster so far.</summary>
        public static int DamageDealt(EffectResult result, ICombatUnit caster)
        {
            int total = 0;
            if (result == null)
            {
                return total;
            }
            foreach (var entry in result.Entries)
            {
                if (entry != null && entry.Landed > 0 && !ReferenceEquals(entry.Target, caster))
                {
                    total += entry.Landed;
                }
            }
            return total;
        }
    }
}
