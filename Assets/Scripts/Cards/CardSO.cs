using System.Collections.Generic;
using System.Linq;
using System.Text;
using Assets.Scripts.Combat;
using UnityEngine;

namespace Assets.Scripts.Cards
{
    [CreateAssetMenu(menuName = "SO/Card")]
    public class CardSO : ScriptableObject
    {
        public string Key;
        public string DisplayName;
        [TextArea(2, 4)]
        public string Description;
        public Sprite Icon;
        public CardTargetType TargetType;
        public CardRarity Rarity;
        public List<CardEffect> Effects = new List<CardEffect>();
        public List<CardTag> Tags = new List<CardTag>();
        public int TagDuration = 3;

        public bool HasEffectType(CardEffectType type)
        {
            return Effects.Any(e => e.EffectType == type);
        }

        public string GetEffectsSummary()
        {
            if (Effects == null || Effects.Count == 0)
            {
                return "";
            }

            var sb = new StringBuilder();
            for (int i = 0; i < Effects.Count; i++)
            {
                if (i > 0)
                {
                    sb.Append(", ");
                }

                var effect = Effects[i];
                switch (effect.EffectType)
                {
                    case CardEffectType.Damage:
                        sb.Append($"DMG {effect.Power}");
                        if (effect.DamageType != DamageType.Normal)
                        {
                            sb.Append($" {effect.DamageType}");
                        }
                        break;
                    case CardEffectType.Heal:
                        sb.Append($"Heal {effect.Power}");
                        break;
                    case CardEffectType.Buff:
                        sb.Append($"+{effect.BuffType}");
                        break;
                    case CardEffectType.Debuff:
                        sb.Append($"-{effect.BuffType}");
                        break;
                }
            }

            return sb.ToString();
        }
    }
}
