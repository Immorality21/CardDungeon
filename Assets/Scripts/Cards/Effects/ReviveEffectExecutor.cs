using System;
using System.Collections.Generic;
using Assets.Scripts.Combat;
using Assets.Scripts.UnitStats;
using UnityEngine;

namespace Assets.Scripts.Cards.Effects
{
    /// <summary>
    /// <see cref="SpellEffectType.Revive"/>: a fallen hero stands back up at <c>Power</c> percent of their
    /// max health (at least 1). The bar is set through <see cref="HealthOps"/> with
    /// <see cref="HealthCause.Revive"/>, then <see cref="UnitRevived"/> goes out so the fight's owner puts
    /// them back on the clock and the stage - the live fight and the simulator alike.
    ///
    /// <para>Who: a single-target cast revives the fallen ally it was aimed at; a cast on the whole party
    /// (the Phoenix) revives every fallen hero of the caster's side, read from
    /// <see cref="EffectResolver.FallenAllies"/>. A standing target is left alone.</para>
    /// </summary>
    public class ReviveEffectExecutor : ICastAwareEffectExecutor
    {
        private static readonly Color ReviveColor = new Color(1f, 0.92f, 0.55f);
        private const float EffectDelay = 0.25f;

        private readonly Func<CombatEvents> _events;
        private readonly Func<Func<ICombatUnit, List<ICombatUnit>>> _fallen;

        public ReviveEffectExecutor(Func<CombatEvents> events, Func<Func<ICombatUnit, List<ICombatUnit>>> fallen)
        {
            _events = events;
            _fallen = fallen;
        }

        public void Execute(SpellEffect effect, SpellcastAction action, List<ICombatUnit> targets,
            CombatBuffTracker buffTracker, EffectResult result)
        {
            var caster = action != null ? action.Caster : null;
            bool wholeParty = action != null && action.Magic != null && action.Magic.TargetType == MagicTargetType.AllAllies;
            Revive(effect, caster, wholeParty ? FallenOf(caster) : targets, result);
        }

        public void Execute(SpellEffect effect, ICombatUnit caster, List<ICombatUnit> targets,
            CombatBuffTracker buffTracker, EffectResult result, bool flatPower = false)
        {
            Revive(effect, caster, targets, result);
        }

        private List<ICombatUnit> FallenOf(ICombatUnit caster)
        {
            var fallen = _fallen?.Invoke();
            return fallen != null ? fallen(caster) ?? new List<ICombatUnit>() : new List<ICombatUnit>();
        }

        /// <summary>The bar a revived unit stands up with: <paramref name="percent"/> of its max, at least 1.</summary>
        public static int HealthFor(ICombatUnit unit, int percent)
        {
            int max = unit.GetEffectiveStat(StatType.MaxHealth);
            return Mathf.Clamp(Mathf.FloorToInt(max * Mathf.Max(0, percent) / 100f), 1, Mathf.Max(1, max));
        }

        private void Revive(SpellEffect effect, ICombatUnit caster, List<ICombatUnit> targets, EffectResult result)
        {
            if (targets == null)
            {
                return;
            }
            var events = _events?.Invoke();
            foreach (var target in new List<ICombatUnit>(targets))
            {
                if (target == null || target.IsAlive || !target.IsHero)
                {
                    continue;
                }
                int health = HealthFor(target, effect.Power);
                HealthOps.Set(target, health, new HealthSource(caster, HealthCause.Revive), events);
                events?.Publish(new UnitRevived { Unit = target, By = caster });
                result.Entries.Add(new EffectEntry
                {
                    Target = target,
                    Text = "Revived!",
                    Healed = health,
                    Color = ReviveColor,
                    Delay = EffectDelay
                });
            }
        }
    }
}
