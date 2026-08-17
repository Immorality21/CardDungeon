using System.Collections.Generic;
using Assets.Scripts.Items;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// Covers <see cref="LootRoller"/> drop math: rarity ordering, over-level suppression, and the
    /// deterministic <see cref="LootRoller.ShouldDrop"/> boundary.
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

        [Test]
        public void DropChance_NullItem_IsZero()
        {
            Assert.AreEqual(0f, LootRoller.DropChance(null, 0));
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
    }
}
