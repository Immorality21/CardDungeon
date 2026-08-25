using System;
using System.Collections.Generic;
using Assets.Scripts.Cards.Effects;
using Assets.Scripts.Combat;
using Assets.Scripts.Progression;
using UnityEngine;

namespace Assets.Scripts.Cards
{
    public class EffectResolver
    {
        private static readonly Color ComboNameColor = new Color(1f, 0.6f, 0f);
        private const float ComboDelay = 0.3f;

        private readonly EffectExecutorFactory _factory = new EffectExecutorFactory();

        /// <param name="powerBonus">Flat power added to the magic's Damage/Heal effects (from its upgrade level).</param>
        /// <param name="magicUpgradeLevel">The magic's upgrade level — effects with a higher UnlockLevel are skipped.</param>
        /// <param name="comboLevelLookup">Returns a combo's upgrade level by key (gates combo effects + scales combo power); null = level 0.</param>
        /// <param name="powerScale">
        /// Multiplier on each Damage/Heal effect's base Power, applied after <paramref name="powerBonus"/>.
        /// 1 leaves the authored numbers alone, which is every hero cast. It exists for <b>enemy</b>
        /// casts: an enemy's spells scale with its level's <c>EnemyTuning.Difficulty</c>, the same
        /// dial that scales the Strength its basic attack swings off, so its magic escalates across
        /// the campaign instead of staying at floor-one power. Buff/Debuff power is left alone for
        /// the same reason the upgrade bonus leaves it alone - it is a stat delta, not a damage number.
        /// </param>
        public EffectResult Execute(
            SpellcastAction action,
            CombatBuffTracker buffTracker,
            MagicTagTracker tagTracker = null,
            ComboDetector comboDetector = null,
            int powerBonus = 0,
            int magicUpgradeLevel = 0,
            Func<string, int> comboLevelLookup = null,
            float powerScale = 1f)
        {
            var result = new EffectResult();

            foreach (var effect in action.Magic.Effects)
            {
                if (effect.UnlockLevel > magicUpgradeLevel)
                {
                    continue;
                }
                var effectToUse = ApplyPowerBonus(effect, powerBonus, powerScale);
                var executor = _factory.GetExecutor(effectToUse.EffectType);
                executor.Execute(effectToUse, action.Caster, action.Targets, buffTracker, result);
            }

            if (tagTracker != null && comboDetector != null && action.Magic.Tags.Count > 0)
            {
                foreach (var target in action.Targets)
                {
                    if (!target.IsAlive)
                    {
                        continue;
                    }

                    var combo = comboDetector.DetectCombo(action.Magic.Tags, target, tagTracker);
                    if (combo != null)
                    {
                        ApplyCombo(combo, target, action.Caster, buffTracker, result, comboLevelLookup);
                    }
                }

                foreach (var target in action.Targets)
                {
                    tagTracker.ApplyTags(target, action.Magic.Tags, action.Magic.TagDuration);
                }
            }

            return result;
        }

        /// <summary>
        /// Returns a copy of the effect with a permanent card-upgrade bonus folded into its
        /// Power and then multiplied by <paramref name="powerScale"/>, for Damage/Heal effects only. Buff/Debuff power (a stat amount) is left
        /// unchanged. Returns the original effect when there is no bonus to apply.
        /// </summary>
        private SpellEffect ApplyPowerBonus(SpellEffect effect, int powerBonus, float powerScale = 1f)
        {
            bool scales = !Mathf.Approximately(powerScale, 1f);
            if (powerBonus <= 0 && !scales)
            {
                return effect;
            }

            if (effect.EffectType != SpellEffectType.Damage && effect.EffectType != SpellEffectType.Heal)
            {
                return effect;
            }

            int power = effect.Power + Mathf.Max(0, powerBonus);
            if (scales && power > 0)
            {
                power = Mathf.Max(1, Mathf.RoundToInt(power * powerScale));
            }

            return new SpellEffect
            {
                EffectType = effect.EffectType,
                Power = power,
                // Every other field has to be carried across, or the copy quietly differs from the
                // authored effect. ScalingStat was the one that mattered: it defaults to None, so an
                // upgraded magic lost its caster contribution entirely - upgrading a caster's spell
                // made it weaker by that caster's whole scaling stat, and still hit for a plausible
                // number, so nothing looked wrong.
                ScalingStat = effect.ScalingStat,
                DamageType = effect.DamageType,
                BuffType = effect.BuffType,
                Duration = effect.Duration,
                UnlockLevel = effect.UnlockLevel
            };
        }

        private void ApplyCombo(
            MagicComboSO combo,
            ICombatUnit target,
            ICombatUnit caster,
            CombatBuffTracker buffTracker,
            EffectResult result,
            Func<string, int> comboLevelLookup)
        {
            result.ComboName = combo.ComboName;
            if (!string.IsNullOrEmpty(combo.Key) && !result.TriggeredComboKeys.Contains(combo.Key))
            {
                result.TriggeredComboKeys.Add(combo.Key);
            }

            result.Entries.Add(new EffectEntry
            {
                Target = target,
                Text = combo.ComboName,
                Color = ComboNameColor,
                Delay = ComboDelay,
                PositionOffset = Vector3.up * 0.3f
            });

            int comboLevel = comboLevelLookup != null && !string.IsNullOrEmpty(combo.Key)
                ? comboLevelLookup(combo.Key)
                : 0;
            int comboPowerBonus = MetaProgressManager.MagicPowerBonusForLevel(comboLevel);

            foreach (var effect in combo.BonusEffects)
            {
                if (effect.UnlockLevel > comboLevel)
                {
                    continue;
                }
                var effectToUse = ApplyPowerBonus(effect, comboPowerBonus);
                var comboTargets = GetComboTargets(effectToUse.EffectType, caster, target);
                var executor = _factory.GetExecutor(effectToUse.EffectType);
                executor.Execute(effectToUse, caster, comboTargets, buffTracker, result, flatPower: true);
            }
        }

        private List<ICombatUnit> GetComboTargets(SpellEffectType effectType, ICombatUnit caster, ICombatUnit target)
        {
            switch (effectType)
            {
                case SpellEffectType.Heal:
                case SpellEffectType.Buff:
                    return new List<ICombatUnit> { caster };
                default:
                    return new List<ICombatUnit> { target };
            }
        }
    }
}
