using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Assets.Scripts.Cards
{
    /// <summary>Every summon in the game, loaded from Resources so the hub and the dungeon resolve
    /// keys without scene wiring (same pattern as <c>EnemyCatalogSO</c>).</summary>
    [CreateAssetMenu(menuName = "SO/Summon Catalog")]
    public class SummonCatalogSO : ScriptableObject
    {
        public const string ResourcePath = "SummonCatalog";

        public List<SummonSO> Summons = new List<SummonSO>();

        private static SummonCatalogSO _cached;

        public static SummonCatalogSO Load()
        {
            if (_cached == null)
            {
                _cached = UnityEngine.Resources.Load<SummonCatalogSO>(ResourcePath);
            }
            return _cached;
        }

        public SummonSO Find(string key)
        {
            return string.IsNullOrEmpty(key) ? null : Summons.FirstOrDefault(s => s != null && s.Key == key);
        }

        /// <summary>Key to definition through the Resources catalog; null when there is none.</summary>
        public static SummonSO Resolve(string key)
        {
            var catalog = Load();
            return catalog != null ? catalog.Find(key) : null;
        }
    }
}
