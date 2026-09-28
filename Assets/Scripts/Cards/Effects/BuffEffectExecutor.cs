using System.Collections.Generic;
using Assets.Scripts.Cards.Buffs;
using Assets.Scripts.Combat;
using UnityEngine;

namespace Assets.Scripts.Cards.Effects
{
    public class BuffEffectExecutor : IEffectExecutor
    {
        private static readonly Color BuffColor = Color.cyan;
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

            // A percentage buff is sized per target, off that target's own stat, and replaces an
            // older percentage buff on the same stat instead of stacking (PowerMode.PercentOfTargetStat).
            if (effect.PowerMode == PowerMode.PercentOfTargetStat && handler is StatBuffHandler statHandler)
            {
                foreach (var target in targets)
                {
                    if (!target.IsAlive)
                    {
                        continue;
                    }

                    int amount = SpellPower.PercentOfStat(effect.Power, target, statHandler.Stat);
                    buffTracker.ApplyPercentBuff(target, statHandler.Stat, amount, effect.Duration);
                    result.Entries.Add(new EffectEntry
                    {
                        Target = target,
                        Text = handler.GetDisplayText(amount),
                        Color = BuffColor,
                        Delay = EffectDelay
                    });
                }
                return;
            }

            // Flat-power buffs stay as authored; a cast one adds a fraction of the caster's stat, so a
            // high-Spirit caster's shields are better without dwarfing the stat being changed.
            int magnitude = flatPower
                ? effect.Power
                : effect.Power + SpellScaling.BuffContribution(caster, effect.ScalingStat, buffTracker);

            foreach (var target in targets)
            {
                if (!target.IsAlive)
                {
                    continue;
                }

                handler.Apply(target, magnitude, effect.Duration, buffTracker);

                result.Entries.Add(new EffectEntry
                {
                    Target = target,
                    Text = handler.GetDisplayText(magnitude),
                    Color = BuffColor,
                    Delay = EffectDelay
                });
            }
        }
    }
}
