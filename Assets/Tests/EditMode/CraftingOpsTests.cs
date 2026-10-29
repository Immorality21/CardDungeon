using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Items;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// The crafting rules (<see cref="CraftingOps"/>) on plain item lists: what makes a recipe known,
    /// what blocks a craft and in which order, and what a craft takes and gives - including the worn
    /// base whose wearer gets the result.
    /// </summary>
    public class CraftingOpsTests
    {
        private readonly List<ItemSO> _created = new List<ItemSO>();
        private ItemSO _iron;
        private ItemSO _coal;
        private ItemSO _sword;
        private ItemSO _flameSword;
        private ItemSO _recipe;

        [SetUp]
        public void SetUp()
        {
            _iron = Make("Iron", ItemCategory.Material);
            _coal = Make("Coal", ItemCategory.Material);
            _sword = Make("Sword", ItemCategory.Equipment);
            _flameSword = Make("FlameSword", ItemCategory.Equipment);
            _recipe = Make("RecipeFlameSword", ItemCategory.Recipe);
            _recipe.CraftResult = _flameSword;
            _recipe.CraftBase = _sword;
            _recipe.CraftGold = 50;
            _recipe.CraftMaterials = new List<MaterialCost>
            {
                new MaterialCost { Material = _iron, Amount = 3 },
                new MaterialCost { Material = _coal, Amount = 1 }
            };
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var so in _created)
            {
                Object.DestroyImmediate(so);
            }
            _created.Clear();
        }

        private ItemSO Make(string key, ItemCategory category)
        {
            var so = ScriptableObject.CreateInstance<ItemSO>();
            so.Key = key;
            so.DisplayName = key;
            so.Category = category;
            so.MaxStack = category == ItemCategory.Recipe ? 1 : 99;
            _created.Add(so);
            return so;
        }

        private ItemSO Resolve(string key)
        {
            return _created.FirstOrDefault(i => i.Key == key);
        }

        /// <summary>Everything the flame sword asks for, the recipe included, with the sword in the bags.</summary>
        private List<ItemSaveData> Stocked()
        {
            return new List<ItemSaveData>
            {
                new ItemSaveData { ItemKey = "RecipeFlameSword", Quantity = 1 },
                new ItemSaveData { ItemKey = "Sword", Quantity = 1 },
                new ItemSaveData { ItemKey = "Iron", Quantity = 5 },
                new ItemSaveData { ItemKey = "Coal", Quantity = 1 }
            };
        }

        [Test]
        public void IsKnown_RecipeInTheBags_IsKnown()
        {
            Assert.IsTrue(CraftingOps.IsKnown(_recipe, Stocked()));
            Assert.IsFalse(CraftingOps.IsKnown(_recipe, new List<ItemSaveData>()));
        }

        [Test]
        public void IsKnown_ForgeRecipe_IsKnownWithNothingCarried()
        {
            _recipe.KnownWithForge = true;
            Assert.IsTrue(CraftingOps.IsKnown(_recipe, new List<ItemSaveData>()));
        }

        [Test]
        public void AlreadyKnown_OnlyEverSkipsRecipes()
        {
            var items = Stocked();
            Assert.IsTrue(CraftingOps.AlreadyKnown(_recipe, items), "a known recipe teaches nothing");
            Assert.IsFalse(CraftingOps.AlreadyKnown(_iron, items), "a material is never skipped as known");
            Assert.IsFalse(CraftingOps.AlreadyKnown(_recipe, new List<ItemSaveData>()));
        }

        [Test]
        public void Check_ReportsTheFirstBlockInOrder()
        {
            Assert.AreEqual(CraftBlock.NotKnown, CraftingOps.Check(_recipe, new List<ItemSaveData>(), 999, Resolve));

            var noSword = Stocked().Where(i => i.ItemKey != "Sword").ToList();
            Assert.AreEqual(CraftBlock.NoBase, CraftingOps.Check(_recipe, noSword, 999, Resolve));

            var shortIron = Stocked();
            shortIron.First(i => i.ItemKey == "Iron").Quantity = 2;
            Assert.AreEqual(CraftBlock.Materials, CraftingOps.Check(_recipe, shortIron, 999, Resolve));

            Assert.AreEqual(CraftBlock.Gold, CraftingOps.Check(_recipe, Stocked(), 49, Resolve));
            Assert.AreEqual(CraftBlock.None, CraftingOps.Check(_recipe, Stocked(), 50, Resolve));
        }

        [Test]
        public void Check_NoResult_IsInvalid()
        {
            _recipe.CraftResult = null;
            Assert.AreEqual(CraftBlock.Invalid, CraftingOps.Check(_recipe, Stocked(), 999, Resolve));
        }

        [Test]
        public void Craft_SpendsTheInputs_KeepsTheRecipe_AddsTheResult()
        {
            var items = Stocked();
            Assert.IsTrue(CraftingOps.Craft(items, _recipe, Resolve, out var usedBase, out var made));

            Assert.AreEqual("Sword", usedBase.ItemKey);
            Assert.IsFalse(items.Any(i => i.ItemKey == "Sword"), "the base is used up");
            Assert.AreEqual(2, items.First(i => i.ItemKey == "Iron").Quantity);
            Assert.IsFalse(items.Any(i => i.ItemKey == "Coal"), "a spent-out stack is removed");
            Assert.IsTrue(items.Any(i => i.ItemKey == "RecipeFlameSword"), "a recipe is never used up");
            Assert.AreEqual("FlameSword", made.ItemKey);
            Assert.IsTrue(string.IsNullOrEmpty(made.EquippedSlot), "a spare base makes a spare result");
        }

        [Test]
        public void Craft_WhenShort_ChangesNothing()
        {
            var items = Stocked();
            items.First(i => i.ItemKey == "Coal").Quantity = 0;
            items.RemoveAll(i => i.ItemKey == "Coal");
            int before = items.Count;

            Assert.IsFalse(CraftingOps.Craft(items, _recipe, Resolve, out _, out _));
            Assert.AreEqual(before, items.Count);
            Assert.IsTrue(items.Any(i => i.ItemKey == "Sword"), "the base survives a refused craft");
            Assert.AreEqual(5, items.First(i => i.ItemKey == "Iron").Quantity);
        }

        [Test]
        public void Craft_WornBase_ResultGoesOnTheSameHero()
        {
            var items = Stocked();
            var sword = items.First(i => i.ItemKey == "Sword");
            sword.EquippedSlot = "Weapon";
            sword.EquippedHeroKey = "Warrior";

            Assert.IsTrue(CraftingOps.Craft(items, _recipe, Resolve, out _, out var made));
            Assert.AreEqual("Weapon", made.EquippedSlot);
            Assert.AreEqual("Warrior", made.EquippedHeroKey);
        }

        [Test]
        public void FindBase_PrefersASpare_AndTheLeastGrownOne()
        {
            var worn = new ItemSaveData { ItemKey = "Sword", EquippedSlot = "Weapon", EquippedHeroKey = "Warrior" };
            var grown = new ItemSaveData { ItemKey = "Sword", Counters = new List<ItemCounter> { new ItemCounter { Key = "kills", Value = 9 } } };
            var fresh = new ItemSaveData { ItemKey = "Sword" };
            var items = new List<ItemSaveData> { worn, grown, fresh };

            Assert.AreSame(fresh, CraftingOps.FindBase(items, "Sword"));
            items.Remove(fresh);
            Assert.AreSame(grown, CraftingOps.FindBase(items, "Sword"));
            items.Remove(grown);
            Assert.AreSame(worn, CraftingOps.FindBase(items, "Sword"), "a worn copy only when there is no spare");
        }

        [Test]
        public void Recipes_ForgeOnesFirst_ThenByResultName()
        {
            var cap = Make("Cap", ItemCategory.Equipment);
            var axe = Make("Axe", ItemCategory.Equipment);
            var capRecipe = Make("RecipeCap", ItemCategory.Recipe);
            capRecipe.CraftResult = cap;
            capRecipe.KnownWithForge = true;
            var axeRecipe = Make("RecipeAxe", ItemCategory.Recipe);
            axeRecipe.CraftResult = axe;

            var ordered = CraftingOps.Recipes(new[] { _recipe, axeRecipe, _iron, capRecipe });
            CollectionAssert.AreEqual(new[] { capRecipe, axeRecipe, _recipe }, ordered);
        }
    }
}
