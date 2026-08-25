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

                // A percentage effect reads as "30%" rather than "30", or the summary claims a 10%
                // cloak costs 10 flat health.
                string power = effect.PowerMode == PowerMode.PercentOfMaxHealth
                    ? $"{effect.Power}%"
                    : effect.Power.ToString();

                switch (effect.EffectType)
                {
                    case SpellEffectType.Damage:
                        sb.Append($"DMG {power}");
                        if (effect.DamageType != DamageType.Normal)
                        {
                            sb.Append($" {effect.DamageType}");
                        }
                        break;
                    case SpellEffectType.Heal:
                        sb.Append($"Heal {power}");
                        break;
                    case SpellEffectType.Buff:
                        sb.Append($"+{effect.BuffType}");
                        break;
                    case SpellEffectType.Debuff:
                        sb.Append($"-{effect.BuffType}");
                        break;
                    case SpellEffectType.HealthCost:
                        sb.Append($"Costs {power} HP");
                        break;
                }
            }

            return sb.ToString();
        }
    }
}
