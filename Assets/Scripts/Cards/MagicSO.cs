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

        [Tooltip("Scales the threat this ability earns from the damage and healing it lands " +
                 "(1 = the default: a point of damage is a point of threat, a point of healing half " +
                 "a point). Above 1 makes enemies turn on the caster faster; 0 lets it land unnoticed.")]
        [Min(0f)]
        public float ThreatMultiplier = 1f;

        [Tooltip("Flat threat added every time this is cast, on top of what its damage and healing " +
                 "earn. How an ability that deals nothing - a taunt, a provoking shout - still draws " +
                 "the enemy's eye.")]
        [Min(0)]
        public int BonusThreat;

        public bool HasEffectType(SpellEffectType type)
        {
            return Effects.Any(e => e.EffectType == type);
        }

        /// <summary>
        /// True when this ability is a physical attack, and so can be dodged: it deals damage scaled
        /// off a physical stat (Strength, Agility). A dodge makes the whole ability miss that target,
        /// not just its damage — a dodged Sunder lands no Endurance cut either. Magic, flat and
        /// percentage damage cannot be dodged. See <see cref="DefenseRules"/>.
        /// </summary>
        public bool IsPhysicalAttack()
        {
            return Effects.Any(e => e.IsPhysicalHit);
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
                    case SpellEffectType.TurnDelay:
                        sb.Append($"Delay {effect.Power}%");
                        break;
                }
            }

            return sb.ToString();
        }
    }
}
