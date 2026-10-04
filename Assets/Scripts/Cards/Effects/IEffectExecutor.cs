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

    /// <summary>
    /// An executor that needs more of the cast than the effect and its targets - which slot it came
    /// out of, which the player picked. <see cref="EffectResolver"/> calls this overload for a hero,
    /// enemy or summon cast; the plain one still serves combos and room events, where there is no
    /// cast to read. Passing the action rather than parking it on the shared resolver keeps the
    /// resolver free of per-cast state.
    /// </summary>
    public interface ICastAwareEffectExecutor : IEffectExecutor
    {
        void Execute(
            SpellEffect effect,
            SpellcastAction action,
            List<ICombatUnit> targets,
            CombatBuffTracker buffTracker,
            EffectResult result);
    }
}
