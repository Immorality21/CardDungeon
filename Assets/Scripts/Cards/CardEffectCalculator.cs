using System.Collections.Generic;
using Assets.Scripts.Combat;
using Assets.Scripts.Items;
using UnityEngine;

namespace Assets.Scripts.Cards
{
    public class CardEffectCalculator
    {
        private static readonly Color DamageColor = Color.white;
        private static readonly Color HealColor = Color.green;
        private static readonly Color BuffColor = Color.cyan;
        private static readonly Color DebuffColor = new Color(0.8f, 0.2f, 0.8f);
        private static readonly Color ComboNameColor = new Color(1f, 0.6f, 0f);
        private static readonly Color ComboDamageColor = new Color(1f, 0.4f, 0f);

        private const float EffectDelay = 0.2f;
        private const float ComboDelay = 0.3f;
        private const int BuffDuration = 3;

        public CardEffectResult Execute(
            CardAction action,
            CombatBuffTracker buffTracker,
            CardTagTracker tagTracker = null,
            ComboDetector comboDetector = null)
        {
            var result = new CardEffectResult();

            switch (action.Card.EffectType)
            {
                case CardEffectType.Damage:
                    ApplyDamage(action, buffTracker, result);
                    break;
                case CardEffectType.Heal:
                    ApplyHeal(action, result);
                    break;
                case CardEffectType.BuffAttack:
                    ApplyBuff(action, buffTracker, StatType.Attack, result);
                    break;
                case CardEffectType.BuffDefense:
                    ApplyBuff(action, buffTracker, StatType.Defense, result);
                    break;
                case CardEffectType.Debuff:
                    ApplyDebuff(action, buffTracker, result);
                    break;
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

        private void ApplyDamage(CardAction action, CombatBuffTracker buffTracker, CardEffectResult result)
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

                result.Entries.Add(new EffectEntry
                {
                    Target = target,
                    Text = damage.ToString(),
                    Color = DamageColor,
                    Delay = EffectDelay
                });
            }
        }

        private void ApplyHeal(CardAction action, CardEffectResult result)
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

                result.Entries.Add(new EffectEntry
                {
                    Target = target,
                    Text = actualHeal.ToString(),
                    Color = HealColor,
                    Delay = EffectDelay
                });
            }
        }

        private void ApplyBuff(CardAction action, CombatBuffTracker buffTracker, StatType stat, CardEffectResult result)
        {
            foreach (var target in action.Targets)
            {
                if (!target.IsAlive)
                {
                    continue;
                }

                buffTracker.ApplyBuff(target, stat, action.Card.Power, BuffDuration);

                result.Entries.Add(new EffectEntry
                {
                    Target = target,
                    Text = $"+{action.Card.Power} {stat}",
                    Color = BuffColor,
                    Delay = EffectDelay
                });
            }
        }

        private void ApplyDebuff(CardAction action, CombatBuffTracker buffTracker, CardEffectResult result)
        {
            foreach (var target in action.Targets)
            {
                if (!target.IsAlive)
                {
                    continue;
                }

                buffTracker.ApplyBuff(target, StatType.Attack, -action.Card.Power, BuffDuration);
                buffTracker.ApplyBuff(target, StatType.Defense, -action.Card.Power, BuffDuration);

                result.Entries.Add(new EffectEntry
                {
                    Target = target,
                    Text = $"-{action.Card.Power} Stats",
                    Color = DebuffColor,
                    Delay = EffectDelay
                });
            }
        }

        private void ApplyCombo(
            CardComboSO combo,
            ICombatUnit target,
            ICombatUnit caster,
            CombatBuffTracker buffTracker,
            CardEffectResult result)
        {
            result.ComboName = combo.ComboName;

            // Combo name floating text
            result.Entries.Add(new EffectEntry
            {
                Target = target,
                Text = combo.ComboName,
                Color = ComboNameColor,
                Delay = ComboDelay,
                PositionOffset = Vector3.up * 0.3f
            });

            // Apply combo bonus effect
            switch (combo.BonusEffect)
            {
                case CardEffectType.Damage:
                    target.Stats.Health -= combo.BonusPower;
                    result.Entries.Add(new EffectEntry
                    {
                        Target = target,
                        Text = combo.BonusPower.ToString(),
                        Color = ComboDamageColor,
                        Delay = ComboDelay
                    });
                    break;

                case CardEffectType.Heal:
                    int heal = Mathf.Min(combo.BonusPower, caster.Stats.MaxHealth - caster.Stats.Health);
                    caster.Stats.Health += heal;
                    result.Entries.Add(new EffectEntry
                    {
                        Target = caster,
                        Text = heal.ToString(),
                        Color = HealColor,
                        Delay = ComboDelay
                    });
                    break;

                case CardEffectType.BuffAttack:
                    buffTracker.ApplyBuff(caster, StatType.Attack, combo.BonusPower, BuffDuration);
                    result.Entries.Add(new EffectEntry
                    {
                        Target = caster,
                        Text = $"+{combo.BonusPower} Atk",
                        Color = BuffColor,
                        Delay = ComboDelay
                    });
                    break;

                case CardEffectType.BuffDefense:
                    buffTracker.ApplyBuff(caster, StatType.Defense, combo.BonusPower, BuffDuration);
                    result.Entries.Add(new EffectEntry
                    {
                        Target = caster,
                        Text = $"+{combo.BonusPower} Def",
                        Color = BuffColor,
                        Delay = ComboDelay
                    });
                    break;

                case CardEffectType.Debuff:
                    buffTracker.ApplyBuff(target, StatType.Attack, -combo.BonusPower, BuffDuration);
                    buffTracker.ApplyBuff(target, StatType.Defense, -combo.BonusPower, BuffDuration);
                    result.Entries.Add(new EffectEntry
                    {
                        Target = target,
                        Text = $"-{combo.BonusPower} Stats",
                        Color = DebuffColor,
                        Delay = ComboDelay
                    });
                    break;
            }
        }
    }
}
