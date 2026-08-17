using System;
using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.Items
{
    /// <summary>
    /// Pure inventory logic (stacking, consuming, quantity normalization, equipment bonus totals)
    /// operating on plain lists — no MonoBehaviour, singleton, or disk access — so it is unit
    /// testable in the same spirit as <c>DamageCalculator</c> / <c>EffectResolver</c>.
    /// <see cref="InventoryManager"/> wraps these with its save/event side effects.
    /// </summary>
    public static class InventoryOperations
    {
        /// <summary>Old saves predate Quantity; JsonUtility leaves it 0. Treat non-positive as one.</summary>
        public static void NormalizeQuantities(List<ItemSaveData> items)
        {
            if (items == null)
            {
                return;
            }
            foreach (var item in items)
            {
                if (item.Quantity <= 0)
                {
                    item.Quantity = 1;
                }
            }
        }

        /// <summary>
        /// Adds an item: consumables stack into an existing un-equipped pile of the same key
        /// (clamped to <see cref="ItemSO.MaxStack"/>); equipment is a distinct one-per-entry item.
        /// </summary>
        public static void AddItem(List<ItemSaveData> items, ItemSO item)
        {
            if (items == null || item == null)
            {
                return;
            }

            if (item.Category == ItemCategory.Consumable)
            {
                var stack = items.Find(
                    i => i.ItemKey == item.Key && string.IsNullOrEmpty(i.EquippedSlot));
                if (stack != null)
                {
                    int max = item.MaxStack > 0 ? item.MaxStack : int.MaxValue;
                    stack.Quantity = Mathf.Min(stack.Quantity + 1, max);
                    return;
                }
            }

            items.Add(new ItemSaveData { ItemKey = item.Key, Quantity = 1 });
        }

        /// <summary>Spends one unit of a consumable, removing the stack at zero. False if none carried.</summary>
        public static bool TryConsume(List<ItemSaveData> items, string itemKey, Func<string, ItemSO> resolve)
        {
            if (items == null)
            {
                return false;
            }

            var stack = items.Find(
                i => i.ItemKey == itemKey && i.Quantity > 0 && IsCategory(i, ItemCategory.Consumable, resolve));
            if (stack == null)
            {
                return false;
            }

            stack.Quantity--;
            if (stack.Quantity <= 0)
            {
                items.Remove(stack);
            }
            return true;
        }

        /// <summary>Total carried quantity of a consumable across stacks.</summary>
        public static int GetConsumableQuantity(List<ItemSaveData> items, string itemKey, Func<string, ItemSO> resolve)
        {
            if (items == null)
            {
                return 0;
            }

            int total = 0;
            foreach (var item in items)
            {
                if (item.ItemKey == itemKey && IsCategory(item, ItemCategory.Consumable, resolve))
                {
                    total += item.Quantity;
                }
            }
            return total;
        }

        /// <summary>Refills a consumable up to a total <paramref name="cap"/>; never reduces a surplus.</summary>
        public static void TopUpConsumableToCap(List<ItemSaveData> items, ItemSO item, int cap, Func<string, ItemSO> resolve)
        {
            if (items == null || item == null || item.Category != ItemCategory.Consumable || cap <= 0)
            {
                return;
            }

            int have = GetConsumableQuantity(items, item.Key, resolve);
            if (have >= cap)
            {
                return;
            }

            var stack = items.Find(
                i => i.ItemKey == item.Key && string.IsNullOrEmpty(i.EquippedSlot) && IsCategory(i, ItemCategory.Consumable, resolve));
            if (stack == null)
            {
                stack = new ItemSaveData { ItemKey = item.Key, Quantity = 0 };
                items.Add(stack);
            }
            stack.Quantity += cap - have;
        }

        /// <summary>Sums equipment bonuses of the given <paramref name="bonusType"/> across equipped items.</summary>
        public static Dictionary<StatType, float> ComputeBonuses(IEnumerable<ItemSO> equippedItems, BonusType bonusType)
        {
            var result = new Dictionary<StatType, float>();
            foreach (StatType stat in Enum.GetValues(typeof(StatType)))
            {
                result[stat] = 0f;
            }

            if (equippedItems == null)
            {
                return result;
            }

            foreach (var so in equippedItems)
            {
                if (so == null)
                {
                    continue;
                }
                foreach (var bonus in so.Bonuses)
                {
                    if (bonus.BonusType == bonusType)
                    {
                        result[bonus.StatType] += bonus.Value;
                    }
                }
            }

            return result;
        }

        private static bool IsCategory(ItemSaveData item, ItemCategory category, Func<string, ItemSO> resolve)
        {
            if (resolve == null)
            {
                return false;
            }
            var so = resolve(item.ItemKey);
            return so != null && so.Category == category;
        }
    }
}
