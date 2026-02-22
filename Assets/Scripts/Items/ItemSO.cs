using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.Items
{
    [CreateAssetMenu(menuName = "SO/Item")]
    public class ItemSO : ScriptableObject
    {
        public string Key;
        public string DisplayName;
        public Sprite Icon;
        public SlotType SlotType;
        public ItemRarity Rarity;
        public int ItemLevel = 1;
        public List<ItemBonus> Bonuses = new List<ItemBonus>();
    }
}
