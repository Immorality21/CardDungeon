using System;
using System.Collections.Generic;
using Assets.Scripts.Combat;
using UnityEngine;

namespace Assets.Scripts.Cards.Effects
{
    /// <summary>
    /// Pushes each target's next turn back on the clock by <c>Power</c> percent of its own full turn
    /// (<see cref="TurnManager.Delay"/>). Exatrix's claws are the first author of it (§4b).
    ///
    /// <para>The clock is the fight's <see cref="TurnManager"/>, handed in by the resolver that owns
    /// it - the live fight's and the simulator's alike. Through a resolver with no clock the effect
    /// is inert and says nothing, rather than claiming a delay that never happened.</para>
    /// </summary>
    public class TurnDelayEffectExecutor : IEffectExecutor
    {
        private static readonly Color DelayColor = new Color(0.55f, 0.45f, 0.95f);
        private const float EffectDelay = 0.2f;

        private readonly Func<TurnManager> _clock;

        public TurnDelayEffectExecutor(Func<TurnManager> clock)
        {
            _clock = clock;
        }

        public void Execute(
            SpellEffect effect,
            ICombatUnit caster,
            List<ICombatUnit> targets,
            CombatBuffTracker buffTracker,
            EffectResult result,
            bool flatPower = false)
        {
            var clock = _clock != null ? _clock() : null;
            if (clock == null || effect.Power <= 0)
            {
                return;
            }

            foreach (var target in targets)
            {
                if (target == null || !target.IsAlive)
                {
                    continue;
                }

                float moved = clock.Delay(target, effect.Power / 100f);
                result.Entries.Add(new EffectEntry
                {
                    Target = target,
                    // At the cap the hit still lands; only the delay does not, and the player should
                    // see why the turn order did not move.
                    Text = moved > 0f ? "Delayed" : "Can't delay further",
                    Color = moved > 0f ? DelayColor : Color.gray,
                    Delay = EffectDelay
                });
            }
        }
    }
}
