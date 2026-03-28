using System.Collections.Generic;
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
        public CardEffectType EffectType;
        public DamageType DamageType;
        public int Power;
        public CardRarity Rarity;
        public List<string> Tags = new List<string>();
        public int TagDuration = 3;
    }
}
