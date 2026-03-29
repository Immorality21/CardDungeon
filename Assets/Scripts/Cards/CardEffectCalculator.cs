using System.Collections.Generic;
using Assets.Scripts.Cards.Effects;
using Assets.Scripts.Combat;
using UnityEngine;

namespace Assets.Scripts.Cards
{
    public class CardEffectCalculator
    {
        private static readonly Color ComboNameColor = new Color(1f, 0.6f, 0f);
        private const float ComboDelay = 0.3f;

        private readonly EffectExecutorFactory _factory = new EffectExecutorFactory();

        public CardEffectResult Execute(
            CardAction action,
            CombatBuffTracker buffTracker,
            CardTagTracker tagTracker = null,
            ComboDetector comboDetector = null)
        {
            var result = new CardEffectResult();

            foreach (var effect in action.Card.Effects)
            {
                var executor = _factory.GetExecutor(effect.EffectType);
                executor.Execute(effect, action.Caster, action.Targets, buffTracker, result);
            }

            if (tagTracker != null && comboDetector != null && action.Card.Tags.Count > 0)
            {
                foreach (var target in action.Targets)
                {
                    if (!target.IsAlive)
                    {
                        continue;
                    }

                    var combo = comboDetector.DetectCombo(action.Card.Tags, target, tagTracker);
                    if (combo != null)
                    {
                        ApplyCombo(combo, target, action.Caster, buffTracker, result);
                    }
                }

                foreach (var target in action.Targets)
                {
                    tagTracker.ApplyTags(target, action.Card.Tags, action.Card.TagDuration);
                }
            }

            return result;
        }

        private void ApplyCombo(
            CardComboSO combo,
            ICombatUnit target,
            ICombatUnit caster,
            CombatBuffTracker buffTracker,
            CardEffectResult result)
        {
            result.ComboName = combo.ComboName;

            result.Entries.Add(new EffectEntry
            {
                Target = target,
                Text = combo.ComboName,
                Color = ComboNameColor,
                Delay = ComboDelay,
                PositionOffset = Vector3.up * 0.3f
            });

            foreach (var effect in combo.BonusEffects)
            {
                var comboTargets = GetComboTargets(effect.EffectType, caster, target);
                var executor = _factory.GetExecutor(effect.EffectType);
                executor.Execute(effect, caster, comboTargets, buffTracker, result, isComboEffect: true);
            }
        }

        private List<ICombatUnit> GetComboTargets(CardEffectType effectType, ICombatUnit caster, ICombatUnit target)
        {
            switch (effectType)
            {
                case CardEffectType.Heal:
                case CardEffectType.Buff:
                    return new List<ICombatUnit> { caster };
                default:
                    return new List<ICombatUnit> { target };
            }
        }
    }
}
