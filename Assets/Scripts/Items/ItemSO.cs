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
        public ItemRarity Rarity;
        public int ItemLevel = 1;

        [Header("Category")]
        public ItemCategory Category = ItemCategory.Equipment;

        [Header("Equipment (Category == Equipment)")]
        public SlotType SlotType;
        public List<ItemBonus> Bonuses = new List<ItemBonus>();

        [Header("Consumable (Category == Consumable)")]
        public ConsumableEffectType ConsumableEffect = ConsumableEffectType.RestoreHealth;
        public int ConsumableAmount;
        public int MaxStack = 99;
    }
}
