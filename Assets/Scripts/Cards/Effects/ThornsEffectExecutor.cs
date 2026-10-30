using System;
using System.Collections.Generic;
using Assets.Scripts.Combat;
using UnityEngine;
using ImmoralityGaming.Localization;

namespace Assets.Scripts.Cards.Effects
{
    /// <summary>
    /// <see cref="SpellEffectType.Thorns"/>: each target returns <c>Power</c> percent of every enemy blow
    /// that lands on it, as <c>DamageType</c>, for <c>Duration</c> of its turns
    /// (<see cref="ThornsTable"/>, struck back by <see cref="RetaliationTracker"/>). The Mage's cloaks.
    /// </summary>
    public class ThornsEffectExecutor : IEffectExecutor
    {
        private static readonly Color ThornsColor = new Color(1f, 0.75f, 0.45f);
        private const float EffectDelay = 0.2f;

        private readonly Func<CombatEvents> _events;

        public ThornsEffectExecutor(Func<CombatEvents> events)
        {
            _events = events;
        }

        public void Execute(SpellEffect effect, ICombatUnit caster, List<ICombatUnit> targets,
            CombatBuffTracker buffTracker, EffectResult result, bool flatPower = false)
        {
            var events = _events?.Invoke();
            if (events == null || targets == null || effect.Power <= 0)
            {
                return;
            }
            int turns = Mathf.Max(1, effect.Duration);
            foreach (var target in targets)
            {
                if (target == null || !target.IsAlive)
                {
                    continue;
                }
                events.Thorns.Add(target, effect.Power, effect.DamageType, turns);
                result.Entries.Add(new EffectEntry
                {
                    Target = target,
                    Text = Loc.Format("combat.float.thorns", ("power", effect.Power)),
                    Color = ThornsColor,
                    Delay = EffectDelay
                });
            }
        }
    }
}
