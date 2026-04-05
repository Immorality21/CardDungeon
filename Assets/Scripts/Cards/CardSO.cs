using System.Collections.Generic;
using System.Linq;
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
    }
}
