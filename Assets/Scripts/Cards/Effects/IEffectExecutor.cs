using System.Collections.Generic;
using Assets.Scripts.Combat;

namespace Assets.Scripts.Cards.Effects
{
    public interface IEffectExecutor
    {
        void Execute(
            SpellEffect effect,
            ICombatUnit caster,
            List<ICombatUnit> targets,
            CombatBuffTracker buffTracker,
            EffectResult result,
            bool isComboEffect = false);
    }
}
