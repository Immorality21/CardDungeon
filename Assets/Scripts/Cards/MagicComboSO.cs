using System.Collections.Generic;
using Assets.Scripts.Combat;
using UnityEngine;
using ImmoralityGaming.Localization;

namespace Assets.Scripts.Cards
{
    [CreateAssetMenu(menuName = "SO/Magic Combo")]
    public class MagicComboSO : ScriptableObject
    {
        // Stable identifier for discovery + upgrade tracking. ComboName is display-only
        // (not guaranteed unique), so Key is what the meta-progression keys off.
        [TextKeys("combo", "name", "description")]
        public string Key;
        [System.NonSerialized] private string _comboName;

        /// <summary>From the text table (<c>combo.{key}.name</c>). Set only on instances built in code.</summary>
        public string ComboName
        {
            get { return _comboName ?? Loc.AssetText("combo", Key, this, "name"); }
            set { _comboName = value; }
        }
        [System.NonSerialized] private string _description;

        /// <summary>From the text table (<c>combo.{key}.description</c>). Set only on instances built in code.</summary>
        public string Description
        {
            get { return _description ?? Loc.AssetText("combo", Key, this, "description"); }
            set { _description = value; }
        }
        public Sprite Icon;
        public List<MagicTag> RequiredTags = new List<MagicTag>();
        public List<SpellEffect> BonusEffects = new List<SpellEffect>();
    }
}
