using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Assets.Scripts.Cards
{
    /// <summary>Every Ultra in the game, loaded from Resources so grid nodes resolve by key without
    /// scene wiring - the same pattern as <see cref="SummonCatalogSO"/>.</summary>
    [CreateAssetMenu(menuName = "SO/Ultra Catalog")]
    public class UltraCatalogSO : ScriptableObject
    {
        public const string ResourcePath = "UltraCatalog";

        public List<UltraSO> Ultras = new List<UltraSO>();

        private static UltraCatalogSO _cached;

        public static UltraCatalogSO Load()
        {
            if (_cached == null)
            {
                _cached = UnityEngine.Resources.Load<UltraCatalogSO>(ResourcePath);
            }
            return _cached;
        }

        public UltraSO Find(string key)
        {
            return string.IsNullOrEmpty(key) ? null : Ultras.FirstOrDefault(u => u != null && u.Key == key);
        }

        /// <summary>Key to definition through the Resources catalog; null when there is none.</summary>
        public static UltraSO Resolve(string key)
        {
            var catalog = Load();
            return catalog != null ? catalog.Find(key) : null;
        }
    }
}
