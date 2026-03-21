using System.Collections;
using System.Collections.Generic;
using Assets.Scripts.Combat;
using Assets.Scripts.Items;
using ImmoralityGaming.Fundamentals;
using UnityEngine;

namespace Assets.Scripts.Cards
{
    public static class CardExecutor
    {
        private static string _comboLog;

        public static IEnumerator Execute(
            CardAction action,
            CombatBuffTracker buffTracker,
            CardTagTracker tagTracker = null,
            ComboDetector comboDetector = null)
        {
            _comboLog = null;

            switch (action.Card.EffectType)
            {
                case CardEffectType.Damage:
                    yield return ExecuteDamage(action, buffTracker);
                    break;
                case CardEffectType.Heal:
                    yield return ExecuteHeal(action);
                    break;
                case CardEffectType.BuffAttack:
                    yield return ExecuteBuff(action, buffTracker, StatType.Attack);
                    break;
                case CardEffectType.BuffDefense:
                    yield return ExecuteBuff(action, buffTracker, StatType.Defense);
                    break;
                case CardEffectType.Debuff:
                    yield return ExecuteDebuff(action, buffTracker);
                    break;
            }

            // Check for combos on each target
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
                        yield return ExecuteCombo(combo, target, action.Caster, buffTracker);
                    }
                }

                // Apply this card's tags to targets after combo check
                foreach (var target in action.Targets)
                {
                    tagTracker.ApplyTags(target, action.Card.Tags, action.Card.TagDuration);
                }
            }
        }

        public static string GetLastLog(CardAction action)
        {
            var log = $"{action.Caster.DisplayName} plays {action.Card.DisplayName}!";
            if (!string.IsNullOrEmpty(_comboLog))
            {
                log += " " + _comboLog;
            }
            return log;
        }

        private static IEnumerator ExecuteDamage(CardAction action, CombatBuffTracker buffTracker)
        {
            int attackBonus = buffTracker.GetBuffAmount(action.Caster, StatType.Attack);
            int baseAttack = action.Caster.GetEffectiveAttack() + attackBonus;

            foreach (var target in action.Targets)
            {
                if (!target.IsAlive)
                {
                    continue;
                }

                int defenseBonus = buffTracker.GetBuffAmount(target, StatType.Defense);
                int defense = target.GetEffectiveDefense() + defenseBonus;
                int damage = Mathf.Max(1, baseAttack + action.Card.Power - defense);

                target.Stats.Health -= damage;

                ShowFloatingText(target.Transform.position, damage.ToString(), Color.white);
                yield return new WaitForSeconds(0.2f);
            }
        }

        private static IEnumerator ExecuteHeal(CardAction action)
        {
            foreach (var target in action.Targets)
            {
                if (!target.IsAlive)
                {
                    continue;
                }

                int healAmount = action.Card.Power;
                int newHealth = Mathf.Min(target.Stats.Health + healAmount, target.Stats.MaxHealth);
                int actualHeal = newHealth - target.Stats.Health;
                target.Stats.Health = newHealth;

                ShowFloatingText(target.Transform.position, actualHeal.ToString(), Color.green);
                yield return new WaitForSeconds(0.2f);
            }
        }

        private static IEnumerator ExecuteBuff(CardAction action, CombatBuffTracker buffTracker, StatType stat)
        {
            foreach (var target in action.Targets)
            {
                if (!target.IsAlive)
                {
                    continue;
                }

                buffTracker.ApplyBuff(target, stat, action.Card.Power, 3);

                string label = $"+{action.Card.Power} {stat}";
                ShowFloatingText(target.Transform.position, label, Color.cyan);
                yield return new WaitForSeconds(0.2f);
            }
        }

        private static IEnumerator ExecuteDebuff(CardAction action, CombatBuffTracker buffTracker)
        {
            foreach (var target in action.Targets)
            {
                if (!target.IsAlive)
                {
                    continue;
                }

                // Debuff reduces both attack and defense
                buffTracker.ApplyBuff(target, StatType.Attack, -action.Card.Power, 3);
                buffTracker.ApplyBuff(target, StatType.Defense, -action.Card.Power, 3);

                string label = $"-{action.Card.Power} Stats";
                ShowFloatingText(target.Transform.position, label, new Color(0.8f, 0.2f, 0.8f));
                yield return new WaitForSeconds(0.2f);
            }
        }

        private static IEnumerator ExecuteCombo(
            CardComboSO combo,
            ICombatUnit target,
            ICombatUnit caster,
            CombatBuffTracker buffTracker)
        {
            _comboLog = $"COMBO: {combo.ComboName}!";

            // Show combo name
            ShowFloatingText(
                target.Transform.position + Vector3.up * 0.3f,
                combo.ComboName,
                new Color(1f, 0.6f, 0f));
            yield return new WaitForSeconds(0.3f);

            switch (combo.BonusEffect)
            {
                case CardEffectType.Damage:
                    int damage = combo.BonusPower;
                    target.Stats.Health -= damage;
                    ShowFloatingText(target.Transform.position, damage.ToString(), new Color(1f, 0.4f, 0f));
                    break;
                case CardEffectType.Heal:
                    int heal = Mathf.Min(combo.BonusPower, caster.Stats.MaxHealth - caster.Stats.Health);
                    caster.Stats.Health += heal;
                    ShowFloatingText(caster.Transform.position, heal.ToString(), Color.green);
                    break;
                case CardEffectType.BuffAttack:
                    buffTracker.ApplyBuff(caster, StatType.Attack, combo.BonusPower, 3);
                    ShowFloatingText(caster.Transform.position, $"+{combo.BonusPower} Atk", Color.cyan);
                    break;
                case CardEffectType.BuffDefense:
                    buffTracker.ApplyBuff(caster, StatType.Defense, combo.BonusPower, 3);
                    ShowFloatingText(caster.Transform.position, $"+{combo.BonusPower} Def", Color.cyan);
                    break;
                case CardEffectType.Debuff:
                    buffTracker.ApplyBuff(target, StatType.Attack, -combo.BonusPower, 3);
                    buffTracker.ApplyBuff(target, StatType.Defense, -combo.BonusPower, 3);
                    ShowFloatingText(target.Transform.position, $"-{combo.BonusPower} Stats", new Color(0.8f, 0.2f, 0.8f));
                    break;
            }

            yield return new WaitForSeconds(0.3f);
        }

        private static void ShowFloatingText(Vector3 position, string text, Color color)
        {
            if (FloatingTextHandler.HasInstance)
            {
                FloatingTextHandler.Instance.CreateFloatingText(
                    position,
                    text,
                    color,
                    1f,
                    0.8f,
                    0.15f,
                    TextFadeMode.FadeUp);
            }
        }
    }
}
