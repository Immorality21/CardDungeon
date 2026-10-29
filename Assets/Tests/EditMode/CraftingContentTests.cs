using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Dungeon;
using Assets.Scripts.Enemies;
using Assets.Scripts.Items;
using NUnit.Framework;
using UnityEditor;

namespace Tests.EditMode
{
    /// <summary>
    /// Asset-integrity guards for crafting (<c>docs/plans/HUB.md</c> §7 phase 7). A recipe is authored
    /// across three places - the recipe item, the result, and the tables it drops from - and each way
    /// those drift apart looks fine in the inspector: a recipe with no source is simply never found, a
    /// craft-only item no recipe makes is simply never seen, and a Forge recipe in a drop table drops as
    /// something the player already knows.
    /// </summary>
    public class CraftingContentTests
    {
        private static List<ItemSO> AllItems()
        {
            var items = new List<ItemSO>();
            foreach (var guid in AssetDatabase.FindAssets("t:ItemSO"))
            {
                var item = AssetDatabase.LoadAssetAtPath<ItemSO>(AssetDatabase.GUIDToAssetPath(guid));
                if (item != null)
                {
                    items.Add(item);
                }
            }
            return items;
        }

        private static List<ItemSO> Recipes()
        {
            return AllItems().Where(i => i.Category == ItemCategory.Recipe).ToList();
        }

        private static List<T> LoadAll<T>() where T : UnityEngine.ScriptableObject
        {
            return AssetDatabase.FindAssets($"t:{typeof(T).Name}")
                .Select(g => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(a => a != null)
                .ToList();
        }

        /// <summary>Every recipe that some drop table or floor clear can hand over.</summary>
        private static HashSet<ItemSO> DroppedRecipes()
        {
            var dropped = new HashSet<ItemSO>();
            void Add(IEnumerable<LootDrop> table)
            {
                if (table == null)
                {
                    return;
                }
                foreach (var drop in table)
                {
                    if (drop != null && drop.Item != null && drop.Item.Category == ItemCategory.Recipe)
                    {
                        dropped.Add(drop.Item);
                    }
                }
            }
            foreach (var enemy in LoadAll<EnemySO>())
            {
                Add(enemy.LootTable);
                Add(enemy.SalvageTable);
            }
            foreach (var level in LoadAll<LevelDefinitionSO>())
            {
                Add(level.MaterialTable);
                Add(level.GuaranteedMaterials);
                Add(level.RecipeTable);
            }
            return dropped;
        }

        [Test]
        public void EveryRecipe_MakesSomething_FromRealMaterials()
        {
            var broken = new List<string>();
            foreach (var recipe in Recipes())
            {
                if (recipe.CraftResult == null || recipe.CraftResult.Category == ItemCategory.Recipe)
                {
                    broken.Add($"{recipe.name}: no result");
                }
                if (recipe.CraftMaterials == null || recipe.CraftMaterials.Count == 0)
                {
                    broken.Add($"{recipe.name}: costs no materials");
                }
                else if (recipe.CraftMaterials.Any(m => m == null || !m.IsValid))
                {
                    broken.Add($"{recipe.name}: a material line is empty or not a material");
                }
                if (recipe.CraftBase != null && recipe.CraftBase.Category != ItemCategory.Equipment)
                {
                    broken.Add($"{recipe.name}: the base item is not equipment");
                }
                if (recipe.CraftBase != null && recipe.CraftBase == recipe.CraftResult)
                {
                    broken.Add($"{recipe.name}: makes its own base");
                }
            }
            CollectionAssert.IsEmpty(broken, string.Join("; ", broken));
        }

        /// <summary>A recipe piles onto itself rather than taking a row per copy.</summary>
        [Test]
        public void EveryRecipe_StacksToOne()
        {
            var wrong = Recipes().Where(r => r.MaxStack != 1).Select(r => r.name).ToList();
            CollectionAssert.IsEmpty(wrong, "Recipe(s) with MaxStack other than 1: " + string.Join(", ", wrong));
        }

        /// <summary>
        /// Every recipe is either one the Forge teaches or one something drops - and never both, since a
        /// known recipe dropping is a wasted drop the game would have to skip.
        /// </summary>
        [Test]
        public void EveryRecipe_HasExactlyOneWayToBeKnown()
        {
            var dropped = DroppedRecipes();
            var unreachable = Recipes().Where(r => !r.KnownWithForge && !dropped.Contains(r)).Select(r => r.name).ToList();
            var both = Recipes().Where(r => r.KnownWithForge && dropped.Contains(r)).Select(r => r.name).ToList();

            CollectionAssert.IsEmpty(unreachable,
                "Recipe(s) nothing drops and the Forge does not teach: " + string.Join(", ", unreachable));
            CollectionAssert.IsEmpty(both,
                "Forge recipe(s) also in a drop table, where they would drop already known: " + string.Join(", ", both));
        }

        /// <summary>The owner's ask (2026-10-29): building the Forge teaches a few very basic recipes.</summary>
        [Test]
        public void TheForge_TeachesAFewBasicRecipes()
        {
            var starters = Recipes().Where(r => r.KnownWithForge).ToList();
            Assert.That(starters.Count, Is.InRange(2, 3), "The Forge should teach two or three basic recipes.");
            var fancy = starters.Where(r => r.CraftResult != null && r.CraftResult.Rarity != ItemRarity.Common)
                .Select(r => r.name).ToList();
            CollectionAssert.IsEmpty(fancy, "Forge recipe(s) for more than a common item: " + string.Join(", ", fancy));
        }

        /// <summary>A craft-only item no recipe makes can never be obtained at all.</summary>
        [Test]
        public void EveryCraftOnlyItem_IsMadeByARecipe()
        {
            var made = new HashSet<ItemSO>(Recipes().Select(r => r.CraftResult).Where(r => r != null));
            var orphans = AllItems().Where(i => i.CraftOnly && !made.Contains(i)).Select(i => i.name).ToList();
            CollectionAssert.IsEmpty(orphans, "Craft-only item(s) no recipe makes: " + string.Join(", ", orphans));
        }

        /// <summary>The hub resolves keys through the Resources catalog, so both halves of a craft must be in it.</summary>
        [Test]
        public void EveryRecipeAndResult_IsInTheResourcesItemCatalog()
        {
            var catalog = UnityEngine.Resources.Load<ItemCatalogSO>(ItemCatalogSO.ResourcePath);
            Assert.IsNotNull(catalog);
            var listed = new HashSet<ItemSO>(catalog.Items.Where(i => i != null));

            var missing = new List<string>();
            foreach (var recipe in Recipes())
            {
                if (!listed.Contains(recipe))
                {
                    missing.Add(recipe.name);
                }
                if (recipe.CraftResult != null && !listed.Contains(recipe.CraftResult))
                {
                    missing.Add(recipe.CraftResult.name);
                }
            }
            CollectionAssert.IsEmpty(missing, "Not in the Resources ItemCatalog: " + string.Join(", ", missing.Distinct()));
        }

        /// <summary>A recipe on a floor is authored with explicit odds, like a material.</summary>
        [Test]
        public void EveryRecipeDrop_SetsAnExplicitChance()
        {
            var implicitChance = new List<string>();
            foreach (var enemy in LoadAll<EnemySO>())
            {
                implicitChance.AddRange(enemy.LootTable
                    .Where(d => d != null && d.Item != null && d.Item.Category == ItemCategory.Recipe && !d.HasExplicitChance)
                    .Select(d => $"{enemy.name}: {d.Item.name}"));
            }
            foreach (var level in LoadAll<LevelDefinitionSO>())
            {
                implicitChance.AddRange(level.RecipeTable
                    .Where(d => d != null && d.Item != null && !d.HasExplicitChance)
                    .Select(d => $"{level.name}: {d.Item.name}"));
                implicitChance.AddRange(level.RecipeTable
                    .Where(d => d != null && d.Item != null && d.Item.Category != ItemCategory.Recipe)
                    .Select(d => $"{level.name}: {d.Item.name} is not a recipe"));
            }
            CollectionAssert.IsEmpty(implicitChance, string.Join("; ", implicitChance));
        }
    }
}
