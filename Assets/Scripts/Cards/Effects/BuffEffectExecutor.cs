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
            bool isComboEffect = false)
        {
            var handler = BuffHandlerRegistry.Get(effect.BuffType);

            // Combo buff stay flat; a cast one adds a fraction of the caster's stat, so a
            // high-Spirit caster's shields are better without dwarfing the stat being changed.
            int magnitude = isComboEffect
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
