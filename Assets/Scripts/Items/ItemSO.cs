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

        [TextArea(2, 4)]
        [Tooltip("One line of flavour, shown where the item is looked at rather than used - the hub " +
                 "Materials tab today. Optional; blank simply shows nothing.")]
        public string Description;

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

        [Tooltip("Stack ceiling for anything that stacks - consumables and materials. Equipment " +
                 "ignores it: a sword is always one entry so it can carry its own equipped slot.")]
        public int MaxStack = 99;

        /// <summary>
        /// Whether this item piles into one inventory entry with a quantity, rather than taking a
        /// row of its own. Consumables and materials do; equipment never can, because an entry
        /// carries which hero has it equipped.
        /// </summary>
        public bool Stacks => Category == ItemCategory.Consumable || Category == ItemCategory.Material;
    }
}
