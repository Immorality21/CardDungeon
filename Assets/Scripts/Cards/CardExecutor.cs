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
        public static IEnumerator Execute(CardAction action, CombatBuffTracker buffTracker)
        {
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
        }

        public static string GetLastLog(CardAction action)
        {
            return $"{action.Caster.DisplayName} plays {action.Card.DisplayName}!";
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
