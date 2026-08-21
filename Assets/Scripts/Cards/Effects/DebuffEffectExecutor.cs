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
            int magnitude = flatPower
                ? effect.Power
                : effect.Power + SpellScaling.BuffContribution(caster, effect.ScalingStat, buffTracker);

            foreach (var target in targets)
            {
                if (!target.IsAlive)
                {
                    continue;
                }

                handler.Apply(target, -magnitude, effect.Duration, buffTracker);

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
