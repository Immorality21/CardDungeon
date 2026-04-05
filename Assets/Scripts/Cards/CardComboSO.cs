using System.Collections.Generic;
using Assets.Scripts.Combat;
using UnityEngine;

namespace Assets.Scripts.Cards
{
    [CreateAssetMenu(menuName = "SO/Card Combo")]
    public class CardComboSO : ScriptableObject
    {
        public string ComboName;
        [TextArea(2, 4)]
        public string Description;
        public List<CardTag> RequiredTags = new List<CardTag>();
        public List<CardEffect> BonusEffects = new List<CardEffect>();
    }
}
