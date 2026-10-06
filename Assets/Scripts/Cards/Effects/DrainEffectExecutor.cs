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

            int heal = RunHeat.Current.ScaleHealing(caster, Amount(effect.Power, DamageDealt(result, caster)));
            if (heal <= 0)
            {
                return;
            }

            int max = caster.GetEffectiveStat(StatType.MaxHealth);
            int healed = Mathf.Min(heal, Mathf.Max(0, max - caster.Stats.Health));
            if (healed <= 0)
            {
                return;
            }
            caster.Stats.Health += healed;

            result.Entries.Add(new EffectEntry
            {
                Target = caster,
                Text = $"+{healed}",
                Color = DrainColor,
                Delay = EffectDelay
            });
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
