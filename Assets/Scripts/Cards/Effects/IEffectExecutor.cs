using System.Collections.Generic;
using Assets.Scripts.Combat;

namespace Assets.Scripts.Cards.Effects
{
    public interface IEffectExecutor
    {
        void Execute(
            CardEffect effect,
            ICombatUnit caster,
            List<ICombatUnit> targets,
            CombatBuffTracker buffTracker,
            CardEffectResult result,
            bool isComboEffect = false);
    }
}
