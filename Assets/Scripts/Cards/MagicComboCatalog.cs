using System.Collections.Generic;
using ImmoralityGaming.Fundamentals;
using UnityEngine;

namespace Assets.Scripts.Cards
{
    /// <summary>
    /// Scene-wired catalog of every magic combo in the game, keyed by <see cref="MagicComboSO.Key"/>.
    /// Single source of truth for the combo list: the combat <c>CombatManager</c> builds its
    /// <c>ComboDetector</c> from this when present, and the hub Forge lists combos from it.
    /// Populate <c>_allCombos</c> in the inspector, in both the menu and game scenes (same as
    /// <see cref="MagicCatalog"/>).
    /// </summary>
    public class MagicComboCatalog : SingletonBehaviour<MagicComboCatalog>
    {
        [SerializeField]
        private List<MagicComboSO> _allCombos = new List<MagicComboSO>();

        public IReadOnlyList<MagicComboSO> AllCombos => _allCombos;

        public MagicComboSO GetCombo(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return null;
            }
            return _allCombos.Find(c => c != null && c.Key == key);
        }
    }
}
