using System.Collections.Generic;
using Assets.Scripts.Cards.Buffs;
using Assets.Scripts.Combat;
using UnityEngine;

namespace Assets.Scripts.Cards.Effects
{
    public class DebuffEffectExecutor : IEffectExecutor
    {
        private static readonly Color DebuffColor = new Color(0.8f, 0.2f, 0.8f);
        private const float EffectDelay = 0.2f;

        public void Execute(
            SpellEffect effect,
            ICombatUnit caster,
            List<ICombatUnit> targets,
            CombatBuffTracker buffTracker,
            EffectResult result,
            bool flatPower = false)
        {
            var handler = BuffHandlerRegistry.Get(effect.BuffType);
            if (handler == null)
            {
                // No handler for this BuffType - inert rather than a crash mid-combat.
                // BuffHandlerRegistry.Unhandled() is what surfaces these to the analyzer.
                return;
            }

            // Flat-power debuffs stay as authored; a cast one adds a fraction of the caster's
            // stat, so a high-Spirit caster's shields are better without dwarfing the stat changed.
            // A revisit's Fear level scales the base of a damage-over-time effect an enemy lays on
            // (a poison, a burn) the way it scales the enemy's spells. Only the base: the caster's
            // stat part below already rides the scaled stats. Stat debuffs are left alone - a -3
            // Endurance is not damage.
            int basePower = effect.Power;
            if (caster != null && !caster.IsHero && handler is IOverTimeBuffHandler overTime && !overTime.Heals)
            {
                basePower = RunFear.Current.ScaleEnemyOverTimePower(basePower);
            }

            int magnitude = flatPower
                ? basePower
                : basePower + SpellScaling.BuffContribution(caster, effect.ScalingStat, buffTracker);

            foreach (var target in targets)
            {
                if (!target.IsAlive)
                {
                    continue;
                }

                // Said on screen, or an immune target reads as a cast that silently did nothing.
                if (StatusImmunity.IsImmune(target, effect.BuffType))
                {
                    result.Entries.Add(new EffectEntry
                    {
                        Target = target,
                        Text = "Immune",
                        Color = Color.gray,
                        Delay = EffectDelay
                    });
                    continue;
                }

                handler.Apply(target, -magnitude, effect.Duration, buffTracker, caster);

                result.Entries.Add(new EffectEntry
                {
                    Target = target,
                    Text = handler.GetDisplayText(-magnitude),
                    Color = DebuffColor,
                    Delay = EffectDelay
                });
            }
        }
    }
}
