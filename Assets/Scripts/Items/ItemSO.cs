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

        [Tooltip("Flat amount restored, before ConsumablePercent is added. Keep it small: this is " +
                 "the part that does NOT grow, so a potion carried by flat alone goes stale the " +
                 "moment a hero buys health on the grid.")]
        public int ConsumableAmount;

        [Range(0f, 1f)]
        [Tooltip("Fraction of the TARGET's max health restored, added to ConsumableAmount. This is " +
                 "the half that keeps a potion worth drinking as heroes grow - a flat 5 HP was " +
                 "worth a fifth of an opening hero and a rounding error by the endgame.")]
        public float ConsumablePercent;

        [Tooltip("Stack ceiling for anything that stacks - consumables and materials. Equipment " +
                 "ignores it: a sword is always one entry so it can carry its own equipped slot.")]
        public int MaxStack = 99;

        /// <summary>
        /// What this consumable actually restores to a target with <paramref name="maxHealth"/>:
        /// the flat amount plus the scaling fraction, rounded up, and never zero for an item that
        /// claims to heal at all.
        ///
        /// <para><b>One rule, one place.</b> Combat, the balance model and the tooltip all ask
        /// here, because a healing number derived twice is a healing number that disagrees with
        /// itself - and the balance model's <c>SustainPool</c> is built on this answer.</para>
        ///
        /// <para><b>Why a potion needs the percentage at all.</b> A healing item competes against
        /// the <i>turn</i> it costs, not against the health bar: using one is a whole combat action
        /// while the enemy keeps swinging. A flat 5 HP against hits of 6.8-14.7 meant drinking a
        /// potion lost the party health on net, which is why they went unused. See
        /// <c>docs/BALANCING.md</c>.</para>
        /// </summary>
        public int HealAmountFor(int maxHealth)
        {
            if (ConsumableEffect != ConsumableEffectType.RestoreHealth)
            {
                return 0;
            }

            float scaled = ConsumablePercent * Mathf.Max(0, maxHealth);
            int total = Mathf.Max(0, ConsumableAmount) + Mathf.CeilToInt(scaled);
            if (total <= 0 && (ConsumableAmount > 0 || ConsumablePercent > 0f))
            {
                return 1;
            }
            return total;
        }

        /// <summary>
        /// Whether this item piles into one inventory entry with a quantity, rather than taking a
        /// row of its own. Consumables and materials do; equipment never can, because an entry
        /// carries which hero has it equipped.
        /// </summary>
        public bool Stacks => Category == ItemCategory.Consumable || Category == ItemCategory.Material;
    }
}
