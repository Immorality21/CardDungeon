using System.Collections.Generic;
using Assets.Scripts.Combat;
using UnityEngine;

namespace Assets.Scripts.Cards
{
    [CreateAssetMenu(menuName = "SO/Magic Combo")]
    public class MagicComboSO : ScriptableObject
    {
        // Stable identifier for discovery + upgrade tracking. ComboName is display-only
        // (not guaranteed unique), so Key is what the meta-progression keys off.
        public string Key;
        public string ComboName;
        [TextArea(2, 4)]
        public string Description;
        public Sprite Icon;
        public List<MagicTag> RequiredTags = new List<MagicTag>();
        public List<SpellEffect> BonusEffects = new List<SpellEffect>();
    }
}
