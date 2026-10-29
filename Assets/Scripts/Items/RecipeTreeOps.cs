using System;
using System.Collections.Generic;

namespace Assets.Scripts.Items
{
    /// <summary>One row of the Hall of Knowledge's recipe trees.</summary>
    public class RecipeTreeNode
    {
        /// <summary>The recipe this row stands for, or null for a plain base item heading a tree.</summary>
        public ItemSO Recipe;

        /// <summary>What the row is about: the recipe's result, or the base item itself.</summary>
        public ItemSO Item;

        /// <summary>How far in the row sits: 0 heads a tree, each step down a recipe adds one.</summary>
        public int Depth;

        /// <summary>Whether the player may see this row's name. A base item always is; a recipe once known.</summary>
        public bool Known;

        /// <summary>The recipes this one's result is the base of - what it can become next.</summary>
        public readonly List<RecipeTreeNode> Children = new List<RecipeTreeNode>();

        public bool IsRecipe => Recipe != null;
    }

    /// <summary>
    /// The recipe trees the Hall of Knowledge draws (<c>docs/plans/HUB.md</c> §7b): every recipe placed
    /// under the item it is made from, so "Simple Sword, then Flaming Short Sword" reads as one line of
    /// descent. Pure, so the shape is tested without a scene.
    ///
    /// <para><b>Every recipe is in the tree, known or not</b> (owner, 2026-10-29): an unknown one is a
    /// "???" row in its true place. That is the slot a hint will hang on later, and the place in the
    /// tree is already a small one - "something can be made from a Leather Cap".</para>
    ///
    /// <para>How a tree is headed: a recipe with no base heads its own; a recipe whose base no recipe
    /// makes (the Leather Gloves a Hexsilk pair is made from) sits under a <i>base item</i> row, which is
    /// always known, since the player can find or buy it; and a recipe whose base another recipe makes
    /// sits under that recipe.</para>
    /// </summary>
    public static class RecipeTreeOps
    {
        /// <summary>The trees, roots in order: the Forge's own recipes first, then by name.</summary>
        public static List<RecipeTreeNode> Build(IEnumerable<ItemSO> recipes, Func<ItemSO, bool> isKnown)
        {
            var all = CraftingOps.Recipes(recipes);
            Func<ItemSO, bool> known = isKnown ?? (_ => false);

            // Which recipe makes each item - the parent a recipe built on that item hangs under.
            var madeBy = new Dictionary<ItemSO, ItemSO>();
            foreach (var recipe in all)
            {
                if (!madeBy.ContainsKey(recipe.CraftResult))
                {
                    madeBy[recipe.CraftResult] = recipe;
                }
            }

            var placed = new HashSet<ItemSO>();
            var roots = new List<RecipeTreeNode>();
            var baseRoots = new Dictionary<ItemSO, RecipeTreeNode>();

            foreach (var recipe in all)
            {
                if (recipe.CraftBase == null)
                {
                    roots.Add(Grow(recipe, 0, all, known, placed));
                }
            }
            foreach (var recipe in all)
            {
                if (recipe.CraftBase == null || madeBy.ContainsKey(recipe.CraftBase) || placed.Contains(recipe))
                {
                    continue;
                }
                if (!baseRoots.TryGetValue(recipe.CraftBase, out var head))
                {
                    head = new RecipeTreeNode { Item = recipe.CraftBase, Depth = 0, Known = true };
                    baseRoots[recipe.CraftBase] = head;
                }
                head.Children.Add(Grow(recipe, 1, all, known, placed));
            }

            var baseHeads = new List<RecipeTreeNode>(baseRoots.Values);
            baseHeads.Sort((a, b) => string.CompareOrdinal(NameOf(a.Item), NameOf(b.Item)));
            roots.AddRange(baseHeads);

            // A cycle (A made from B, B made from A) heads nothing above; show it rather than lose it.
            foreach (var recipe in all)
            {
                if (!placed.Contains(recipe))
                {
                    roots.Add(Grow(recipe, 0, all, known, placed));
                }
            }
            return roots;
        }

        /// <summary>The trees as rows in reading order: each node, then its children beneath it.</summary>
        public static List<RecipeTreeNode> Flatten(IEnumerable<RecipeTreeNode> roots)
        {
            var rows = new List<RecipeTreeNode>();
            if (roots != null)
            {
                foreach (var root in roots)
                {
                    Walk(root, rows);
                }
            }
            return rows;
        }

        private static void Walk(RecipeTreeNode node, List<RecipeTreeNode> rows)
        {
            rows.Add(node);
            foreach (var child in node.Children)
            {
                Walk(child, rows);
            }
        }

        private static RecipeTreeNode Grow(ItemSO recipe, int depth, List<ItemSO> all, Func<ItemSO, bool> known,
            HashSet<ItemSO> placed)
        {
            placed.Add(recipe);
            var node = new RecipeTreeNode
            {
                Recipe = recipe, Item = recipe.CraftResult, Depth = depth, Known = known(recipe)
            };
            foreach (var next in all)
            {
                if (next.CraftBase == recipe.CraftResult && !placed.Contains(next))
                {
                    node.Children.Add(Grow(next, depth + 1, all, known, placed));
                }
            }
            return node;
        }

        private static string NameOf(ItemSO item)
        {
            return item == null ? string.Empty : string.IsNullOrEmpty(item.DisplayName) ? item.Key : item.DisplayName;
        }
    }
}
