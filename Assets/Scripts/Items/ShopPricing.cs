using UnityEngine;

namespace Assets.Scripts.Items
{
    /// <summary>
    /// Pure gold-pricing math for the merchant: what a piece of gear costs to buy and what it
    /// fetches when sold back (at a loss). Price scales with rarity and item level. Deterministic
    /// and stateless so it's unit-testable in the same spirit as <c>LootRoller</c>/<c>DamageCalculator</c>.
    /// </summary>
    public static class ShopPricing
    {
        // Fraction of the buy price the merchant pays when buying gear back from the player.
        public const float SellFraction = 0.4f;

        // Each item level above 1 adds this fraction of the base cost.
        private const float LevelStep = 0.25f;

        /// <summary>Gold cost to buy <paramref name="item"/> from the merchant.</summary>
        public static int BuyPrice(ItemSO item)
        {
            if (item == null)
            {
                return 0;
            }

            float levelFactor = 1f + LevelStep * Mathf.Max(0, item.ItemLevel - 1);
            return Mathf.Max(1, Mathf.RoundToInt(RarityBaseCost(item.Rarity) * levelFactor));
        }

        /// <summary>Gold the player receives for selling <paramref name="item"/> (buy price × sell fraction).</summary>
        public static int SellPrice(ItemSO item)
        {
            if (item == null)
            {
                return 0;
            }
            return Mathf.Max(1, Mathf.RoundToInt(BuyPrice(item) * SellFraction));
        }

        private static int RarityBaseCost(ItemRarity rarity)
        {
            switch (rarity)
            {
                case ItemRarity.Common:
                    return 20;
                case ItemRarity.Uncommon:
                    return 45;
                case ItemRarity.Rare:
                    return 90;
                case ItemRarity.Epic:
                    return 180;
                case ItemRarity.Legendary:
                    return 350;
                default:
                    return 20;
            }
        }
    }
}
