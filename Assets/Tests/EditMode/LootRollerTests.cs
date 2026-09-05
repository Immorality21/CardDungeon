using System.Collections.Generic;
using Assets.Scripts.Items;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// Covers <see cref="LootRoller"/> drop math: rarity ordering, over-level suppression, the
    /// deterministic <see cref="LootRoller.ShouldDrop"/> boundary, and the drop-table layer on top of
    /// it - flat chance overrides, quantity ranges, and rolling a whole table.
    /// </summary>
    public class LootRollerTests
    {
        private readonly List<ItemSO> _created = new List<ItemSO>();

        [TearDown]
        public void TearDown()
        {
            foreach (var so in _created)
            {
                Object.DestroyImmediate(so);
            }
            _created.Clear();
        }

        private ItemSO MakeItem(ItemRarity rarity, int itemLevel)
        {
            var so = ScriptableObject.CreateInstance<ItemSO>();
            so.Key = $"{rarity}_{itemLevel}";
            so.Rarity = rarity;
            so.ItemLevel = itemLevel;
            _created.Add(so);
            return so;
        }

        private ItemSO MakeMaterial(string key)
        {
            var so = ScriptableObject.CreateInstance<ItemSO>();
            so.Key = key;
            so.DisplayName = key;
            so.Category = ItemCategory.Material;
            so.Rarity = ItemRarity.Common;
            so.ItemLevel = 1;
            so.MaxStack = 999;
            _created.Add(so);
            return so;
        }

        /// <summary>A roll source that hands out a fixed script of values, so a table roll is exact.</summary>
        private static System.Func<float> Rolls(params float[] values)
        {
            int index = 0;
            return () => values[Mathf.Min(index++, values.Length - 1)];
        }

        [Test]
        public void DropChance_NullItem_IsZero()
        {
            Assert.AreEqual(0f, LootRoller.DropChance((ItemSO)null, 0));
            Assert.AreEqual(0f, LootRoller.DropChance((LootDrop)null, 0));
        }

        [Test]
        public void DropChance_RarerItem_DropsLessOften()
        {
            // All at-depth (item level 1, run level 0 -> depth 1) so only rarity differs.
            float common = LootRoller.DropChance(MakeItem(ItemRarity.Common, 1), 0);
            float rare = LootRoller.DropChance(MakeItem(ItemRarity.Rare, 1), 0);
            float legendary = LootRoller.DropChance(MakeItem(ItemRarity.Legendary, 1), 0);

            Assert.Greater(common, rare);
            Assert.Greater(rare, legendary);
        }

        [Test]
        public void DropChance_OverLevelItem_IsSuppressed()
        {
            var item = MakeItem(ItemRarity.Common, 4);

            float atDepth = LootRoller.DropChance(item, 3);   // depth 4 == item level 4 -> full
            float shallow = LootRoller.DropChance(item, 0);   // depth 1, 3 levels over -> suppressed

            Assert.Greater(atDepth, shallow);
            Assert.Less(shallow, atDepth * 0.5f, "each level over should roughly halve the chance");
        }

        [Test]
        public void DropChance_ItemAtOrBelowDepth_NotBoosted()
        {
            var item = MakeItem(ItemRarity.Common, 1);

            // Deeper than the item's level must not exceed the base rarity chance.
            float deep = LootRoller.DropChance(item, 5);
            float atDepth = LootRoller.DropChance(item, 0);

            Assert.AreEqual(atDepth, deep, 0.0001f, "under-level items stay at base rarity chance");
        }

        [Test]
        public void ShouldDrop_RollBelowChance_Drops()
        {
            var item = MakeItem(ItemRarity.Common, 1); // chance 0.6 at depth 1
            Assert.IsTrue(LootRoller.ShouldDrop(item, 0, 0.5f));
            Assert.IsFalse(LootRoller.ShouldDrop(item, 0, 0.7f));
        }

        // ============================================================
        //  DROP TABLES
        // ============================================================

        [Test]
        public void DropChance_EntryWithoutExplicitChance_UsesRarityAndDepth()
        {
            var item = MakeItem(ItemRarity.Rare, 4);
            var drop = new LootDrop { Item = item };

            Assert.AreEqual(LootRoller.DropChance(item, 0), LootRoller.DropChance(drop, 0), 0.0001f);
            Assert.AreEqual(LootRoller.DropChance(item, 3), LootRoller.DropChance(drop, 3), 0.0001f);
        }

        [Test]
        public void DropChance_EntryWithExplicitChance_IgnoresRarityAndDepth()
        {
            // A material is gated by *which* monster carries it, not by how deep the player is - so
            // an explicit chance must not move with depth the way a gear drop does.
            var drop = new LootDrop { Item = MakeItem(ItemRarity.Legendary, 9), Chance = 0.4f };

            Assert.AreEqual(0.4f, LootRoller.DropChance(drop, 0), 0.0001f);
            Assert.AreEqual(0.4f, LootRoller.DropChance(drop, 7), 0.0001f);
        }

        [Test]
        public void RollQuantity_StackingItem_SpansTheAuthoredRange()
        {
            var drop = new LootDrop { Item = MakeMaterial("Iron"), MinQuantity = 2, MaxQuantity = 4 };

            Assert.AreEqual(2, LootRoller.RollQuantity(drop, 0f));
            Assert.AreEqual(3, LootRoller.RollQuantity(drop, 0.5f));
            Assert.AreEqual(4, LootRoller.RollQuantity(drop, 0.999f));
        }

        [Test]
        public void RollQuantity_NonStackingItem_IsAlwaysOne()
        {
            // Equipment carries an equipped slot per entry, so it can never be a pile - a quantity
            // range authored on a sword has to be ignored rather than silently duplicated.
            var drop = new LootDrop { Item = MakeItem(ItemRarity.Common, 1), MinQuantity = 3, MaxQuantity = 5 };

            Assert.AreEqual(1, LootRoller.RollQuantity(drop, 0f));
            Assert.AreEqual(1, LootRoller.RollQuantity(drop, 0.9f));
        }

        [Test]
        public void ExpectedQuantity_IsChanceTimesMeanQuantity()
        {
            var drop = new LootDrop { Item = MakeMaterial("Iron"), Chance = 0.5f, MinQuantity = 2, MaxQuantity = 4 };

            Assert.AreEqual(1.5f, LootRoller.ExpectedQuantity(drop, 0), 0.0001f);
        }

        [Test]
        public void Roll_EveryEntryRollsIndependently()
        {
            // A table is a list of things this kill can yield, not a pick-one: a monster drops both
            // its gear and the scrap it is made of.
            var gear = new LootDrop { Item = MakeItem(ItemRarity.Common, 1), Chance = 1f };
            var material = new LootDrop { Item = MakeMaterial("Iron"), Chance = 1f, MinQuantity = 2, MaxQuantity = 2 };

            var awards = LootRoller.Roll(new List<LootDrop> { gear, material }, 0, Rolls(0f));

            Assert.AreEqual(2, awards.Count);
            Assert.AreEqual(1, awards[0].Quantity);
            Assert.AreEqual(2, awards[1].Quantity);
        }

        [Test]
        public void Roll_SkipsEntriesWhoseRollMisses()
        {
            var always = new LootDrop { Item = MakeMaterial("Iron"), Chance = 1f };
            var never = new LootDrop { Item = MakeMaterial("Gold"), Chance = 0.1f };

            // 0.5 misses the 0.1 entry and hits the 1.0 one; the quantity roll follows each hit.
            var awards = LootRoller.Roll(new List<LootDrop> { always, never }, 0, Rolls(0.5f));

            Assert.AreEqual(1, awards.Count);
            Assert.AreEqual("Iron", awards[0].Item.Key);
        }

        [Test]
        public void Roll_NullTableOrEntries_YieldsNothing()
        {
            Assert.IsEmpty(LootRoller.Roll(null, 0, Rolls(0f)));
            Assert.IsEmpty(LootRoller.Roll(new List<LootDrop> { null, new LootDrop() }, 0, Rolls(0f)));
        }
    }
}
