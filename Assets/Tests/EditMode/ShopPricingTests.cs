using Assets.Scripts.Items;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    public class ShopPricingTests
    {
        private static ItemSO Item(ItemRarity rarity, int level)
        {
            var so = ScriptableObject.CreateInstance<ItemSO>();
            so.Rarity = rarity;
            so.ItemLevel = level;
            return so;
        }

        [Test]
        public void BuyPrice_RisesWithRarity()
        {
            int common = ShopPricing.BuyPrice(Item(ItemRarity.Common, 1));
            int rare = ShopPricing.BuyPrice(Item(ItemRarity.Rare, 1));
            int legendary = ShopPricing.BuyPrice(Item(ItemRarity.Legendary, 1));

            Assert.Less(common, rare);
            Assert.Less(rare, legendary);
        }

        [Test]
        public void BuyPrice_RisesWithItemLevel()
        {
            int lvl1 = ShopPricing.BuyPrice(Item(ItemRarity.Uncommon, 1));
            int lvl5 = ShopPricing.BuyPrice(Item(ItemRarity.Uncommon, 5));

            Assert.Greater(lvl5, lvl1);
        }

        [Test]
        public void SellPrice_IsLessThanBuyPrice()
        {
            var item = Item(ItemRarity.Epic, 3);
            Assert.Less(ShopPricing.SellPrice(item), ShopPricing.BuyPrice(item));
        }

        [Test]
        public void SellPrice_IsBuyPriceTimesSellFraction()
        {
            var item = Item(ItemRarity.Rare, 2);
            int expected = Mathf.Max(1, Mathf.RoundToInt(ShopPricing.BuyPrice(item) * ShopPricing.SellFraction));
            Assert.AreEqual(expected, ShopPricing.SellPrice(item));
        }

        [Test]
        public void Prices_HandleNullAndAreAtLeastOne()
        {
            Assert.AreEqual(0, ShopPricing.BuyPrice(null));
            Assert.AreEqual(0, ShopPricing.SellPrice(null));
            Assert.GreaterOrEqual(ShopPricing.BuyPrice(Item(ItemRarity.Common, 1)), 1);
        }
    }
}
