using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Items;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>The Hall of Knowledge's recipe trees (<see cref="RecipeTreeOps"/>): where each recipe hangs and what is shown.</summary>
    public class RecipeTreeOpsTests
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

        private ItemSO Item(string key)
        {
            var so = ScriptableObject.CreateInstance<ItemSO>();
            so.Key = key;
            so.DisplayName = key;
            so.Category = ItemCategory.Equipment;
            _created.Add(so);
            return so;
        }

        private ItemSO Recipe(ItemSO result, ItemSO from = null, bool forge = false)
        {
            var so = Item("Recipe" + result.Key);
            so.Category = ItemCategory.Recipe;
            so.CraftResult = result;
            so.CraftBase = from;
            so.KnownWithForge = forge;
            return so;
        }

        private static string Describe(RecipeTreeNode node)
        {
            return new string('-', node.Depth) + (node.Known ? node.Item.Key : "???");
        }

        [Test]
        public void Build_HangsEachRecipeUnderWhatItIsMadeFrom()
        {
            var sword = Item("Sword");
            var flame = Item("Flame");
            var inferno = Item("Inferno");
            var gloves = Item("Gloves");
            var hexsilk = Item("Hexsilk");
            var swordRecipe = Recipe(sword, forge: true);
            var flameRecipe = Recipe(flame, sword);
            var infernoRecipe = Recipe(inferno, flame);
            var hexRecipe = Recipe(hexsilk, gloves);

            var known = new HashSet<ItemSO> { swordRecipe, flameRecipe, hexRecipe, infernoRecipe };
            var rows = RecipeTreeOps.Flatten(RecipeTreeOps.Build(
                new[] { hexRecipe, infernoRecipe, flameRecipe, swordRecipe }, known.Contains));

            CollectionAssert.AreEqual(
                new[] { "Sword", "-Flame", "--Inferno", "Gloves", "-Hexsilk" },
                rows.Select(Describe).ToArray());
            Assert.IsFalse(rows[3].IsRecipe, "a base no recipe makes heads its tree as a plain item");
        }

        [Test]
        public void Build_AnUnknownRecipe_KeepsItsPlace_AsUnknown()
        {
            var cap = Item("Cap");
            var hood = Item("Hood");
            var capRecipe = Recipe(cap, forge: true);
            var hoodRecipe = Recipe(hood, cap);

            var rows = RecipeTreeOps.Flatten(RecipeTreeOps.Build(
                new[] { capRecipe, hoodRecipe }, r => r == capRecipe));

            CollectionAssert.AreEqual(new[] { "Cap", "-???" }, rows.Select(Describe).ToArray());
            Assert.AreSame(hoodRecipe, rows[1].Recipe, "the unknown row still carries its recipe, for a hint later");
        }

        [Test]
        public void Build_AnUnknownTree_StillShowsItsBaseItem()
        {
            var iron = Item("IronSword");
            var voidBlade = Item("VoidBlade");
            var voidRecipe = Recipe(voidBlade, iron);

            var rows = RecipeTreeOps.Flatten(RecipeTreeOps.Build(new[] { voidRecipe }, _ => false));

            CollectionAssert.AreEqual(new[] { "IronSword", "-???" }, rows.Select(Describe).ToArray());
        }

        [Test]
        public void Build_ACycle_IsShownNotLost()
        {
            var a = Item("A");
            var b = Item("B");
            var aFromB = Recipe(a, b);
            var bFromA = Recipe(b, a);

            var rows = RecipeTreeOps.Flatten(RecipeTreeOps.Build(new[] { aFromB, bFromA }, _ => true));

            Assert.AreEqual(2, rows.Count(r => r.IsRecipe), "both recipes appear exactly once");
        }
    }
}
