using System.Collections.Generic;
using Assets.Scripts.Combat;
using UnityEngine;

namespace Assets.Scripts.Cards
{
    [CreateAssetMenu(menuName = "SO/Magic Combo")]
    public class MagicComboSO : ScriptableObject
    {
        public string ComboName;
        [TextArea(2, 4)]
        public string Description;
        public List<MagicTag> RequiredTags = new List<MagicTag>();
        public List<SpellEffect> BonusEffects = new List<SpellEffect>();
    }
}
