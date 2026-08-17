using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.Items
{
    /// <summary>
    /// The full list of every <see cref="ItemSO"/> in the game, loaded from Resources so any scene
    /// (including the hub MenuScene, which has no wired Managers prefab) can resolve item keys via
    /// an auto-created <see cref="InventoryManager"/> — the same "auto-create + load" approach the
    /// meta/progression managers use. Lives at <c>Assets/Resources/ItemCatalog.asset</c>.
    /// </summary>
    [CreateAssetMenu(menuName = "SO/Item Catalog")]
    public class ItemCatalogSO : ScriptableObject
    {
        public const string ResourcePath = "ItemCatalog";

        public List<ItemSO> Items = new List<ItemSO>();
    }
}
