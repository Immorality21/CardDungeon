using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Items;
using Assets.Scripts.UnitStats;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// Covers the pure inventory logic in <see cref="InventoryOperations"/> — stacking, consuming,
    /// quantity migration, belt top-up, materials (the hub's raw stuff, and paying a price in it),
    /// and equipment bonus totals — without touching the disk-backed
    /// <see cref="InventoryManager"/> singleton.
    /// </summary>
    public class InventoryTests
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

        private ItemSO MakeConsumable(string key, int maxStack = 99)
        {
            var so = ScriptableObject.CreateInstance<ItemSO>();
            so.Key = key;
            so.DisplayName = key;
            so.Category = ItemCategory.Consumable;
            so.ConsumableEffect = ConsumableEffectType.RestoreHealth;
            so.ConsumableAmount = 5;
            so.MaxStack = maxStack;
            _created.Add(so);
            return so;
        }

        private ItemSO MakeEquipment(string key, SlotType slot, params ItemBonus[] bonuses)
        {
            var so = ScriptableObject.CreateInstance<ItemSO>();
            so.Key = key;
            so.DisplayName = key;
            so.Category = ItemCategory.Equipment;
            so.SlotType = slot;
            so.Bonuses = bonuses.ToList();
            _created.Add(so);
            return so;
        }

        private ItemSO MakeMaterial(string key, int maxStack = 999)
        {
            var so = ScriptableObject.CreateInstance<ItemSO>();
            so.Key = key;
            so.DisplayName = key;
            so.Category = ItemCategory.Material;
            so.MaxStack = maxStack;
            _created.Add(so);
            return so;
        }

        private static MaterialCost Cost(ItemSO material, int amount)
        {
            return new MaterialCost { Material = material, Amount = amount };
        }

        private System.Func<string, ItemSO> Resolver()
        {
            return key => _created.Find(s => s.Key == key);
        }

        // --- Stacking -----------------------------------------------------------------

        [Test]
        public void AddItem_Consumable_StacksIntoExistingPile()
        {
            var potion = MakeConsumable("Potion");
            var items = new List<ItemSaveData>();

            InventoryOperations.AddItem(items, potion);
            InventoryOperations.AddItem(items, potion);
            InventoryOperations.AddItem(items, potion);

            Assert.AreEqual(1, items.Count, "consumables should collapse into one stack");
            Assert.AreEqual(3, items[0].Quantity);
        }

        [Test]
        public void AddItem_Consumable_ClampsToMaxStack()
        {
            var potion = MakeConsumable("Potion", maxStack: 2);
            var items = new List<ItemSaveData>();

            InventoryOperations.AddItem(items, potion);
            InventoryOperations.AddItem(items, potion);
            InventoryOperations.AddItem(items, potion);

            Assert.AreEqual(2, items[0].Quantity, "should not exceed MaxStack");
        }

        [Test]
        public void AddItem_Equipment_AddsDistinctEntries()
        {
            var sword = MakeEquipment("Sword", SlotType.MainHand);
            var items = new List<ItemSaveData>();

            InventoryOperations.AddItem(items, sword);
            InventoryOperations.AddItem(items, sword);

            Assert.AreEqual(2, items.Count, "equipment is one entry per item, never stacked");
            Assert.IsTrue(items.All(i => i.Quantity == 1));
        }

        // --- Consuming ----------------------------------------------------------------

        [Test]
        public void TryConsume_DecrementsThenRemovesAtZero()
        {
            var potion = MakeConsumable("Potion");
            var items = new List<ItemSaveData> { new ItemSaveData { ItemKey = "Potion", Quantity = 2 } };

            Assert.IsTrue(InventoryOperations.TryConsume(items, "Potion", Resolver()));
            Assert.AreEqual(1, items[0].Quantity);

            Assert.IsTrue(InventoryOperations.TryConsume(items, "Potion", Resolver()));
            Assert.AreEqual(0, items.Count, "empty stack is removed");
        }

        [Test]
        public void TryConsume_NoneCarried_ReturnsFalse()
        {
            MakeConsumable("Potion");
            var items = new List<ItemSaveData>();
            Assert.IsFalse(InventoryOperations.TryConsume(items, "Potion", Resolver()));
        }

        [Test]
        public void TryConsume_Equipment_ReturnsFalse()
        {
            MakeEquipment("Sword", SlotType.MainHand);
            var items = new List<ItemSaveData> { new ItemSaveData { ItemKey = "Sword", Quantity = 1 } };
            Assert.IsFalse(InventoryOperations.TryConsume(items, "Sword", Resolver()),
                "equipment is not consumable");
            Assert.AreEqual(1, items.Count);
        }

        // --- Migration ----------------------------------------------------------------

        [Test]
        public void NormalizeQuantities_ZeroBecomesOne()
        {
            var items = new List<ItemSaveData>
            {
                new ItemSaveData { ItemKey = "A", Quantity = 0 }, // legacy save (no field)
                new ItemSaveData { ItemKey = "B", Quantity = 5 }
            };

            InventoryOperations.NormalizeQuantities(items);

            Assert.AreEqual(1, items[0].Quantity);
            Assert.AreEqual(5, items[1].Quantity, "existing positive quantities untouched");
        }

        // --- Belt top-up --------------------------------------------------------------

        [Test]
        public void TopUpConsumableToCap_FillsToCap()
        {
            var potion = MakeConsumable("Potion");
            var items = new List<ItemSaveData> { new ItemSaveData { ItemKey = "Potion", Quantity = 1 } };

            InventoryOperations.TopUpConsumableToCap(items, potion, 4, Resolver());

            Assert.AreEqual(4, InventoryOperations.GetConsumableQuantity(items, "Potion", Resolver()));
        }

        [Test]
        public void TopUpConsumableToCap_NeverReducesSurplus()
        {
            var potion = MakeConsumable("Potion");
            var items = new List<ItemSaveData> { new ItemSaveData { ItemKey = "Potion", Quantity = 7 } };

            InventoryOperations.TopUpConsumableToCap(items, potion, 4, Resolver());

            Assert.AreEqual(7, InventoryOperations.GetConsumableQuantity(items, "Potion", Resolver()),
                "top-up must not shrink a stack already above the cap");
        }

        [Test]
        public void TopUpConsumableToCap_FromEmpty_CreatesStack()
        {
            var potion = MakeConsumable("Potion");
            var items = new List<ItemSaveData>();

            InventoryOperations.TopUpConsumableToCap(items, potion, 3, Resolver());

            Assert.AreEqual(3, InventoryOperations.GetConsumableQuantity(items, "Potion", Resolver()));
        }

        // --- Materials ----------------------------------------------------------------

        [Test]
        public void AddItem_Material_StacksAndTakesACount()
        {
            var iron = MakeMaterial("ScrapIron");
            var items = new List<ItemSaveData>();

            InventoryOperations.AddItem(items, iron, 3);
            InventoryOperations.AddItem(items, iron, 2);

            Assert.AreEqual(1, items.Count, "materials pile into one entry, like consumables");
            Assert.AreEqual(5, InventoryOperations.GetMaterialQuantity(items, "ScrapIron", Resolver()));
        }

        [Test]
        public void AddItem_Material_ClampsToMaxStack()
        {
            var iron = MakeMaterial("ScrapIron", maxStack: 4);
            var items = new List<ItemSaveData>();

            InventoryOperations.AddItem(items, iron, 10);

            Assert.AreEqual(4, InventoryOperations.GetMaterialQuantity(items, "ScrapIron", Resolver()));
        }

        [Test]
        public void AddItem_Equipment_WithACount_MakesSeparateEntries()
        {
            // An entry carries which hero has it equipped, so two swords can never be one pile.
            var sword = MakeEquipment("Sword", SlotType.MainHand);
            var items = new List<ItemSaveData>();

            InventoryOperations.AddItem(items, sword, 3);

            Assert.AreEqual(3, items.Count);
            Assert.IsTrue(items.All(i => i.Quantity == 1));
        }

        [Test]
        public void GetMaterialQuantity_IgnoresAConsumableOfTheSameKey()
        {
            var potion = MakeConsumable("Overlap");
            var items = new List<ItemSaveData>();
            InventoryOperations.AddItem(items, potion, 5);

            Assert.AreEqual(0, InventoryOperations.GetMaterialQuantity(items, "Overlap", Resolver()));
        }

        [Test]
        public void SpendMaterial_TakesFromThePileAndRemovesItAtZero()
        {
            var iron = MakeMaterial("ScrapIron");
            var items = new List<ItemSaveData>();
            InventoryOperations.AddItem(items, iron, 3);

            Assert.IsTrue(InventoryOperations.SpendMaterial(items, "ScrapIron", 2, Resolver()));
            Assert.AreEqual(1, InventoryOperations.GetMaterialQuantity(items, "ScrapIron", Resolver()));

            Assert.IsTrue(InventoryOperations.SpendMaterial(items, "ScrapIron", 1, Resolver()));
            Assert.IsEmpty(items);
        }

        [Test]
        public void SpendMaterial_ShortOfTheAsking_SpendsNothing()
        {
            var iron = MakeMaterial("ScrapIron");
            var items = new List<ItemSaveData>();
            InventoryOperations.AddItem(items, iron, 2);

            Assert.IsFalse(InventoryOperations.SpendMaterial(items, "ScrapIron", 3, Resolver()));
            Assert.AreEqual(2, InventoryOperations.GetMaterialQuantity(items, "ScrapIron", Resolver()),
                "a refused price must leave the pile untouched");
        }

        [Test]
        public void SpendMaterials_IsAllOrNothingAcrossTheWholePrice()
        {
            // The half-paid building is the failure mode this guards: one affordable line and one
            // that is not must take nothing at all.
            var iron = MakeMaterial("ScrapIron");
            var timber = MakeMaterial("RottedTimber");
            var items = new List<ItemSaveData>();
            InventoryOperations.AddItem(items, iron, 5);
            InventoryOperations.AddItem(items, timber, 1);

            var price = new List<MaterialCost> { Cost(iron, 2), Cost(timber, 4) };

            Assert.IsFalse(InventoryOperations.CanAfford(items, price, Resolver()));
            Assert.IsFalse(InventoryOperations.SpendMaterials(items, price, Resolver()));
            Assert.AreEqual(5, InventoryOperations.GetMaterialQuantity(items, "ScrapIron", Resolver()));
            Assert.AreEqual(1, InventoryOperations.GetMaterialQuantity(items, "RottedTimber", Resolver()));

            InventoryOperations.AddItem(items, timber, 3);
            Assert.IsTrue(InventoryOperations.SpendMaterials(items, price, Resolver()));
            Assert.AreEqual(3, InventoryOperations.GetMaterialQuantity(items, "ScrapIron", Resolver()));
            Assert.AreEqual(0, InventoryOperations.GetMaterialQuantity(items, "RottedTimber", Resolver()));
        }

        [Test]
        public void CanAfford_AnEmptyPrice_IsAlwaysAffordable()
        {
            Assert.IsTrue(InventoryOperations.CanAfford(new List<ItemSaveData>(), null, Resolver()));
            Assert.IsTrue(InventoryOperations.CanAfford(new List<ItemSaveData>(), new List<MaterialCost>(), Resolver()));
        }

        // --- Equipment bonuses --------------------------------------------------------

        [Test]
        public void ComputeBonuses_SumsOnlyMatchingBonusType()
        {
            var sword = MakeEquipment("Sword", SlotType.MainHand,
                new ItemBonus { StatType = StatType.Strength, BonusType = BonusType.Raw, Value = 4 });
            var ring = MakeEquipment("Ring", SlotType.Necklace,
                new ItemBonus { StatType = StatType.Strength, BonusType = BonusType.Raw, Value = 2 },
                new ItemBonus { StatType = StatType.Strength, BonusType = BonusType.Percentage, Value = 10 });

            var raw = InventoryOperations.ComputeBonuses(new[] { sword, ring }, BonusType.Raw);
            var pct = InventoryOperations.ComputeBonuses(new[] { sword, ring }, BonusType.Percentage);

            Assert.AreEqual(6f, raw[StatType.Strength]);
            Assert.AreEqual(10f, pct[StatType.Strength]);
            Assert.AreEqual(0f, raw[StatType.Endurance]);
        }
    }
}
