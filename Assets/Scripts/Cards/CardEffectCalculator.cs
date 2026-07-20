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
            ComboDetector comboDetector = null,
            int powerBonus = 0)
        {
            var result = new CardEffectResult();

            foreach (var effect in action.Card.Effects)
            {
                var effectToUse = ApplyPowerBonus(effect, powerBonus);
                var executor = _factory.GetExecutor(effectToUse.EffectType);
                executor.Execute(effectToUse, action.Caster, action.Targets, buffTracker, result);
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

        /// <summary>
        /// Returns a copy of the effect with a permanent card-upgrade bonus folded into its
        /// Power, for Damage/Heal effects only. Buff/Debuff power (a stat amount) is left
        /// unchanged. Returns the original effect when there is no bonus to apply.
        /// </summary>
        private CardEffect ApplyPowerBonus(CardEffect effect, int powerBonus)
        {
            if (powerBonus <= 0)
            {
                return effect;
            }

            if (effect.EffectType != CardEffectType.Damage && effect.EffectType != CardEffectType.Heal)
            {
                return effect;
            }

            return new CardEffect
            {
                EffectType = effect.EffectType,
                Power = effect.Power + powerBonus,
                DamageType = effect.DamageType,
                BuffType = effect.BuffType,
                Duration = effect.Duration
            };
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
