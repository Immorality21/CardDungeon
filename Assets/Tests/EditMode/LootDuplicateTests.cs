using System.Collections.Generic;
using Assets.Scripts.Items;
using Assets.Scripts.Rooms;
using Assets.Scripts.Rooms.Events;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// Gear the party already owns turns up less in a find (owner, 2026-11-15: four Leather Caps
    /// before the Mage): <see cref="LootRoller.DuplicateFactor"/>, the copy count, and the two finds
    /// that use it - a cache's one item and an event's "one of".
    /// </summary>
    public class LootDuplicateTests
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

        private ItemSO Gear(string key)
        {
            var so = ScriptableObject.CreateInstance<ItemSO>();
            so.Key = key;
            so.Category = ItemCategory.Equipment;
            so.Rarity = ItemRarity.Common;
            so.ItemLevel = 1;
            _created.Add(so);
            return so;
        }

        [Test]
        public void DuplicateFactor_FallsPerCopy_ForGearOnly()
        {
            var cap = Gear("cap");
            Assert.AreEqual(1f, LootRoller.DuplicateFactor(cap, 0));
            Assert.AreEqual(0.4f, LootRoller.DuplicateFactor(cap, 1), 1e-5f);
            Assert.AreEqual(0.16f, LootRoller.DuplicateFactor(cap, 2), 1e-5f);

            var material = Gear("ore");
            material.Category = ItemCategory.Material;
            Assert.AreEqual(1f, LootRoller.DuplicateFactor(material, 5));
        }

        [Test]
        public void CopiesOwned_CountsLooseAndWorn()
        {
            var cap = Gear("cap");
            var items = new List<ItemSaveData>
            {
                new ItemSaveData { ItemKey = "cap" },
                new ItemSaveData { ItemKey = "cap", EquippedHeroKey = "Warrior", EquippedSlot = "Head" },
                new ItemSaveData { ItemKey = "sword" },
            };

            Assert.AreEqual(2, InventoryOperations.CopiesOwned(cap, items));
        }

        [Test]
        public void PickTreasureItem_OwnedCopyCanFailWhereANewOneDrops()
        {
            var cap = Gear("cap");
            // A roll of 0.5: under the 60% a common level-1 piece has, over the 24% once one is owned.
            Assert.AreEqual(cap, RoomKindRewards.PickTreasureItem(new[] { cap }, 0, () => 0.5f));
            Assert.IsNull(RoomKindRewards.PickTreasureItem(new[] { cap }, 0, () => 0.5f, 0, _ => 1));
        }

        [Test]
        public void PickOneOf_FavoursWhatIsNotOwned()
        {
            var cap = Gear("cap");
            var gloves = Gear("gloves");
            var options = new List<ItemSO> { cap, gloves };
            System.Func<ItemSO, int> owned = i => i == cap ? 3 : 0;

            int caps = 0;
            for (int i = 0; i < 100; i++)
            {
                if (RoomEventRunner.PickOneOf(options, owned, (i + 0.5f) / 100f) == cap)
                {
                    caps++;
                }
            }

            // 3 caps owned: weight 0.064 against 1, so about 6 in 100.
            Assert.Less(caps, 10);
            Assert.Greater(caps, 0);
        }

        [Test]
        public void PickOneOf_WithNothingOwned_IsEven()
        {
            var a = Gear("a");
            var b = Gear("b");
            var options = new List<ItemSO> { a, b };

            Assert.AreEqual(a, RoomEventRunner.PickOneOf(options, null, 0.25f));
            Assert.AreEqual(b, RoomEventRunner.PickOneOf(options, null, 0.75f));
        }
    }
}
