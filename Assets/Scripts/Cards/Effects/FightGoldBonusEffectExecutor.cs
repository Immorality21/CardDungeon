using System;
using System.Collections.Generic;
using Assets.Scripts.Combat;
using UnityEngine;
using ImmoralityGaming.Localization;

namespace Assets.Scripts.Cards.Effects
{
    /// <summary>
    /// The Rogue's Fence (<see cref="SpellEffectType.FightGoldBonus"/>): every kill and steal for the rest
    /// of the fight pays <c>Power</c> percent more gold. The fight keeps the bonus
    /// (<see cref="EffectResolver.AddGoldBonus"/>); without one the cast is inert.
    /// </summary>
    public class FightGoldBonusEffectExecutor : IEffectExecutor
    {
        private static readonly Color GoldColor = new Color(1f, 0.85f, 0.3f);

        private readonly Func<Action<int>> _addGoldBonus;

        public FightGoldBonusEffectExecutor(Func<Action<int>> addGoldBonus)
        {
            _addGoldBonus = addGoldBonus;
        }

        public void Execute(
            SpellEffect effect,
            ICombatUnit caster,
            List<ICombatUnit> targets,
            CombatBuffTracker buffTracker,
            EffectResult result,
            bool flatPower = false)
        {
            _addGoldBonus?.Invoke()?.Invoke(effect.Power);
            if (caster != null)
            {
                result.Entries.Add(new EffectEntry
                {
                    Target = caster,
                    Text = Loc.Format("combat.float.gold-bonus", ("power", effect.Power)),
                    Color = GoldColor,
                    Delay = 0.2f
                });
            }
        }
    }
}
