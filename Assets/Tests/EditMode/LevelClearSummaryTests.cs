using System.Collections.Generic;
using Assets.Scripts.Dungeon;
using Assets.Scripts.Items;
using NUnit.Framework;

namespace Tests.EditMode
{
    public class LevelClearSummaryTests
    {
        private static ItemSaveData Item(string key, int quantity = 1)
        {
            return new ItemSaveData { ItemKey = key, Quantity = quantity };
        }

        [Test]
        public void ItemGains_NewStackAndGrownStack_AreBothGains()
        {
            var before = new List<ItemSaveData> { Item("ScrapIron", 2) };
            var after = new List<ItemSaveData> { Item("ScrapIron", 5), Item("RottedTimber", 1) };

            var gains = LevelClearSummary.ItemGains(before, after, null);

            Assert.AreEqual(2, gains.Count);
            Assert.AreEqual("ScrapIron", gains[0].Key);
            Assert.AreEqual(3, gains[0].Value);
            Assert.AreEqual("RottedTimber", gains[1].Key);
            Assert.AreEqual(1, gains[1].Value);
        }

        [Test]
        public void ItemGains_PotionFoundThenDrunk_StillCountsAsFound()
        {
            // Two in the bag before, one found, one drunk: the bag reads 2 again.
            var before = new List<ItemSaveData> { Item("Potion", 2) };
            var after = new List<ItemSaveData> { Item("Potion", 2) };
            var spent = new List<ConsumableSpend> { new ConsumableSpend { ItemKey = "Potion", Count = 1 } };

            var gains = LevelClearSummary.ItemGains(before, after, spent);

            Assert.AreEqual(1, gains.Count);
            Assert.AreEqual(1, gains[0].Value);
        }

        [Test]
        public void ItemGains_PotionDrunkAndNoneFound_IsNotAGainOrALoss()
        {
            var before = new List<ItemSaveData> { Item("Potion", 2) };
            var after = new List<ItemSaveData> { Item("Potion", 1) };
            var spent = new List<ConsumableSpend> { new ConsumableSpend { ItemKey = "Potion", Count = 1 } };

            CollectionAssert.IsEmpty(LevelClearSummary.ItemGains(before, after, spent));
        }

        [Test]
        public void ItemGains_EquipmentIsOneEntryPerPiece()
        {
            var before = new List<ItemSaveData> { Item("IronSword") };
            var after = new List<ItemSaveData> { Item("IronSword"), Item("IronSword") };

            var gains = LevelClearSummary.ItemGains(before, after, null);

            Assert.AreEqual(1, gains.Count);
            Assert.AreEqual(1, gains[0].Value);
        }

        [Test]
        public void ItemGains_OldSaveQuantityZero_ReadsAsOne()
        {
            var before = new List<ItemSaveData> { new ItemSaveData { ItemKey = "IronSword", Quantity = 0 } };
            var after = new List<ItemSaveData> { Item("IronSword", 1) };

            CollectionAssert.IsEmpty(LevelClearSummary.ItemGains(before, after, null));
        }

        [Test]
        public void GoldTotal_IsFoundPlusBonus()
        {
            var summary = new LevelClearSummary { GoldFound = 20, GoldBonus = 30 };

            Assert.AreEqual(50, summary.GoldTotal);
        }
    }
}
