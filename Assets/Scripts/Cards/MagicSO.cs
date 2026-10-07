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

        [Tooltip("How a damaging hit reaches its target. Projectile: the icon flies from the caster " +
                 "(Fireball, an arrow). Strike: the icon lands on the target where it stands, no flight " +
                 "(Slash, Cleave, a bolt from the sky). Presentation only.")]
        public MagicDelivery Delivery;

        [Tooltip("Which way the icon's art points, in degrees (0 = right, 90 = up, -90 = down, 45 = up " +
                 "and to the right). A projectile turns to face where it is flying, which is only " +
                 "right if the art itself points right - the Aimed Shot arrow is drawn at 45.")]
        [Range(-180f, 180f)]
        public float ProjectileArtAngle;

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
                    case SpellEffectType.Drain:
                        sb.Append($"Drain {effect.Power}%");
                        break;
                    case SpellEffectType.RestoreCharge:
                        sb.Append($"+{effect.Power} charge");
                        break;
                }
            }

            return sb.ToString();
        }
    }
}
