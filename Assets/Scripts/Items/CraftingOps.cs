using System;
using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.Items
{
    /// <summary>Why a recipe cannot be made right now, the first reason found, in this order.</summary>
    public enum CraftBlock
    {
        None = 0,

        /// <summary>The recipe is malformed: no result, or not a recipe at all.</summary>
        Invalid,

        /// <summary>The player has not found the recipe.</summary>
        NotKnown,

        /// <summary>The base item is in neither the bags nor on a hero.</summary>
        NoBase,

        /// <summary>Some material line is short.</summary>
        Materials,

        /// <summary>The purse is short of the recipe's gold.</summary>
        Gold
    }

    /// <summary>
    /// Pure crafting rules (<c>docs/plans/HUB.md</c> §7 phase 7): what a recipe asks for, whether it is
    /// known, and turning the inputs into the result on a plain item list. No singleton, no disk, no
    /// gold - <see cref="InventoryManager.TryCraft"/> wraps this with the purse, the save and the events,
    /// the same split <see cref="InventoryOperations"/> makes.
    ///
    /// <para><b>A recipe is an item, and owning it is knowing it.</b> It drops through the ordinary
    /// tables, is banked on a floor clear and forfeited with a wiped floor like every other find, and is
    /// never used up. The only recipes known without one in the bags are the Forge's own
    /// (<see cref="ItemSO.KnownWithForge"/>), which the Forge screen can only be opened to see once the
    /// Forge stands.</para>
    /// </summary>
    public static class CraftingOps
    {
        /// <summary>Whether <paramref name="recipe"/> is known: one of the Forge's own, or carried.</summary>
        public static bool IsKnown(ItemSO recipe, IList<ItemSaveData> items)
        {
            if (recipe == null || recipe.Category != ItemCategory.Recipe)
            {
                return false;
            }
            if (recipe.KnownWithForge)
            {
                return true;
            }
            if (items == null || string.IsNullOrEmpty(recipe.Key))
            {
                return false;
            }
            foreach (var entry in items)
            {
                if (entry != null && entry.ItemKey == recipe.Key)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Whether a drop of <paramref name="item"/> should be skipped because it would teach nothing: a
        /// recipe already known. Every drop site asks it beside <c>RunFear.Withholds</c>, so a rare recipe
        /// stops taking a drop slot once it is found.
        /// </summary>
        public static bool AlreadyKnown(ItemSO item, IList<ItemSaveData> items)
        {
            return item != null && item.Category == ItemCategory.Recipe && IsKnown(item, items);
        }

        /// <summary>
        /// Every recipe in <paramref name="catalog"/>, known first then not, the Forge's own at the top
        /// of the known ones and each group by its result's name. Malformed rows are dropped.
        /// </summary>
        public static List<ItemSO> Recipes(IEnumerable<ItemSO> catalog)
        {
            var recipes = new List<ItemSO>();
            if (catalog == null)
            {
                return recipes;
            }
            foreach (var item in catalog)
            {
                if (item != null && item.Category == ItemCategory.Recipe && item.CraftResult != null
                    && !recipes.Contains(item))
                {
                    recipes.Add(item);
                }
            }
            recipes.Sort((a, b) =>
            {
                int starter = b.KnownWithForge.CompareTo(a.KnownWithForge);
                return starter != 0 ? starter : string.CompareOrdinal(ResultName(a), ResultName(b));
            });
            return recipes;
        }

        /// <summary>The result's name, for sorting and for the Forge's rows.</summary>
        public static string ResultName(ItemSO recipe)
        {
            if (recipe == null || recipe.CraftResult == null)
            {
                return string.Empty;
            }
            return string.IsNullOrEmpty(recipe.CraftResult.DisplayName) ? recipe.CraftResult.Key : recipe.CraftResult.DisplayName;
        }

        /// <summary>Total carried of one item key, worn or not, across stacks.</summary>
        public static int CountOf(IList<ItemSaveData> items, string itemKey)
        {
            if (items == null || string.IsNullOrEmpty(itemKey))
            {
                return 0;
            }
            int total = 0;
            foreach (var entry in items)
            {
                if (entry != null && entry.ItemKey == itemKey)
                {
                    total += Mathf.Max(1, entry.Quantity);
                }
            }
            return total;
        }

        /// <summary>
        /// The copy of <paramref name="baseKey"/> a craft would use up: a spare in the bags first (the
        /// least grown, so the one that has been counting kills is kept, as selling does), else a worn
        /// copy, whose wearer then gets the result. Null when the party has none.
        /// </summary>
        public static ItemSaveData FindBase(IList<ItemSaveData> items, string baseKey)
        {
            if (items == null || string.IsNullOrEmpty(baseKey))
            {
                return null;
            }

            ItemSaveData spare = null;
            ItemSaveData worn = null;
            int leastGrown = int.MaxValue;
            foreach (var entry in items)
            {
                if (entry == null || entry.ItemKey != baseKey)
                {
                    continue;
                }
                if (!string.IsNullOrEmpty(entry.EquippedSlot))
                {
                    worn = worn ?? entry;
                    continue;
                }
                int growth = Growth(entry);
                if (growth < leastGrown)
                {
                    leastGrown = growth;
                    spare = entry;
                }
            }
            return spare ?? worn;
        }

        /// <summary>
        /// The first thing standing between the player and <paramref name="recipe"/>, or
        /// <see cref="CraftBlock.None"/> when it can be made now.
        /// </summary>
        public static CraftBlock Check(ItemSO recipe, IList<ItemSaveData> items, int gold, Func<string, ItemSO> resolve)
        {
            if (recipe == null || recipe.Category != ItemCategory.Recipe || recipe.CraftResult == null)
            {
                return CraftBlock.Invalid;
            }
            if (!IsKnown(recipe, items))
            {
                return CraftBlock.NotKnown;
            }
            if (recipe.CraftBase != null && FindBase(items, recipe.CraftBase.Key) == null)
            {
                return CraftBlock.NoBase;
            }
            if (!InventoryOperations.CanAfford(AsList(items), recipe.CraftMaterials, resolve))
            {
                return CraftBlock.Materials;
            }
            if (gold < Mathf.Max(0, recipe.CraftGold))
            {
                return CraftBlock.Gold;
            }
            return CraftBlock.None;
        }

        /// <summary>
        /// Makes <paramref name="recipe"/>'s result on <paramref name="items"/>: uses up the base item,
        /// spends the materials and adds the result. Gold is the caller's - pass the purse to
        /// <see cref="Check"/> first. All or nothing: every input is checked before anything changes.
        /// </summary>
        /// <param name="usedBase">The base entry that was consumed, or null when the recipe has none.</param>
        /// <param name="made">The result's new entry: the equipment piece, or the stack it piled onto.</param>
        public static bool Craft(List<ItemSaveData> items, ItemSO recipe, Func<string, ItemSO> resolve,
            out ItemSaveData usedBase, out ItemSaveData made)
        {
            usedBase = null;
            made = null;
            if (items == null || Check(recipe, items, int.MaxValue, resolve) != CraftBlock.None)
            {
                return false;
            }

            if (recipe.CraftBase != null)
            {
                usedBase = FindBase(items, recipe.CraftBase.Key);
                if (usedBase.Quantity > 1)
                {
                    usedBase.Quantity--;
                }
                else
                {
                    items.Remove(usedBase);
                }
            }

            if (!InventoryOperations.SpendMaterials(items, recipe.CraftMaterials, resolve))
            {
                // Check said they were there; restore the base rather than lose it if that ever drifts.
                if (usedBase != null && !items.Contains(usedBase))
                {
                    items.Add(usedBase);
                }
                usedBase = null;
                return false;
            }

            var result = recipe.CraftResult;
            if (result.Stacks)
            {
                InventoryOperations.AddItem(items, result, 1);
                made = items.Find(i => i.ItemKey == result.Key && string.IsNullOrEmpty(i.EquippedSlot));
                return true;
            }

            made = new ItemSaveData { ItemKey = result.Key, Quantity = 1 };
            // A worn base hands its slot to what it became, when the result fits the same slot: forging
            // the sword in your hand leaves the new one in your hand.
            if (usedBase != null && !string.IsNullOrEmpty(usedBase.EquippedSlot)
                && result.Category == ItemCategory.Equipment
                && recipe.CraftBase.Category == ItemCategory.Equipment
                && result.SlotType == recipe.CraftBase.SlotType)
            {
                made.EquippedSlot = usedBase.EquippedSlot;
                made.EquippedHeroKey = usedBase.EquippedHeroKey;
            }
            items.Add(made);
            return true;
        }

        private static int Growth(ItemSaveData entry)
        {
            int growth = 0;
            if (entry.Counters != null)
            {
                foreach (var counter in entry.Counters)
                {
                    growth += counter != null ? counter.Value : 0;
                }
            }
            return growth;
        }

        private static List<ItemSaveData> AsList(IList<ItemSaveData> items)
        {
            return items as List<ItemSaveData> ?? (items != null ? new List<ItemSaveData>(items) : new List<ItemSaveData>());
        }
    }
}
