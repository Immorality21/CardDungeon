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

        [Tooltip("Elemental resistance this item grants while equipped. Resistances sum across gear, " +
                 "innate resistance and temporary buffs, so a deliberately assembled set can pass 100% " +
                 "and absorb that element instead of taking it.")]
        public List<Combat.Resistance> Resistances = new List<Combat.Resistance>();

        [Header("Consumable (Category == Consumable)")]
        public ConsumableEffectType ConsumableEffect = ConsumableEffectType.RestoreHealth;
        public int ConsumableAmount;
        public int MaxStack = 99;
    }
}
