using System.Collections.Generic;
using ImmoralityGaming.Fundamentals;
using UnityEngine;

namespace Assets.Scripts.Cards
{
    /// <summary>
    /// Scene-wired catalog of every magic definition in the game, keyed by <see cref="MagicSO.Key"/>.
    /// Used to resolve saved magic keys back to definitions when restoring equipped slots, and to
    /// list upgradeable magic in the hub Forge. Populate <c>_allMagic</c> in the inspector (the same
    /// assets the old collection catalog referenced).
    /// </summary>
    public class MagicCatalog : SingletonBehaviour<MagicCatalog>
    {
        [SerializeField]
        private List<MagicSO> _allMagic = new List<MagicSO>();

        public IReadOnlyList<MagicSO> AllMagic => _allMagic;

        public MagicSO GetMagic(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return null;
            }
            return _allMagic.Find(m => m != null && m.Key == key);
        }
    }
}
