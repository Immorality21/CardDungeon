using System.Collections.Generic;
using System.Linq;
using System.Text;
using Assets.Scripts.Combat;
using UnityEngine;

namespace Assets.Scripts.Cards
{
    [CreateAssetMenu(menuName = "SO/Magic")]
    public class MagicSO : ScriptableObject
    {
        public string Key;
        public string DisplayName;
        [TextArea(2, 4)]
        public string Description;
        public Sprite Icon;
        public MagicTargetType TargetType;
        public MagicRarity Rarity;
        public List<SpellEffect> Effects = new List<SpellEffect>();
        public List<MagicTag> Tags = new List<MagicTag>();
        public int TagDuration = 3;

        public bool HasEffectType(SpellEffectType type)
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
                    case SpellEffectType.Damage:
                        sb.Append($"DMG {effect.Power}");
                        if (effect.DamageType != DamageType.Normal)
                        {
                            sb.Append($" {effect.DamageType}");
                        }
                        break;
                    case SpellEffectType.Heal:
                        sb.Append($"Heal {effect.Power}");
                        break;
                    case SpellEffectType.Buff:
                        sb.Append($"+{effect.BuffType}");
                        break;
                    case SpellEffectType.Debuff:
                        sb.Append($"-{effect.BuffType}");
                        break;
                }
            }

            return sb.ToString();
        }
    }
}
