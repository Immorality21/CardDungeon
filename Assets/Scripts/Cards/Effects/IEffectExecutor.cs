using System.Collections.Generic;
using Assets.Scripts.Combat;

namespace Assets.Scripts.Cards.Effects
{
    public interface IEffectExecutor
    {
        /// <param name="flatPower">
        /// Use <c>effect.Power</c> as authored, without adding the caster's scaling stat. True for
        /// power that belongs to the definition rather than to whoever triggered it: a combo's bonus
        /// effects, and a room event's outcome (where there is no caster at all).
        /// </param>
        void Execute(
            SpellEffect effect,
            ICombatUnit caster,
            List<ICombatUnit> targets,
            CombatBuffTracker buffTracker,
            EffectResult result,
            bool flatPower = false);
    }
}
