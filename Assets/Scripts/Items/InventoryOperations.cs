using System;
using System.Collections.Generic;
using Assets.Scripts.UnitStats;
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

        /// <summary>
        /// Adds one to a key's tally in a consumption ledger, creating the entry if it is new.
        /// </summary>
        public static void RecordSpend(List<ConsumableSpend> ledger, string itemKey)
        {
            if (ledger == null || string.IsNullOrEmpty(itemKey))
            {
                return;
            }

            var entry = ledger.Find(e => e != null && e.ItemKey == itemKey);
            if (entry != null)
            {
                entry.Count++;
                return;
            }

            ledger.Add(new ConsumableSpend { ItemKey = itemKey, Count = 1 });
        }

        /// <summary>Count recorded against one key, or 0.</summary>
        public static int SpendCount(List<ConsumableSpend> ledger, string itemKey)
        {
            if (ledger == null)
            {
                return 0;
            }

            var entry = ledger.Find(e => e != null && e.ItemKey == itemKey);
            return entry != null ? Mathf.Max(0, entry.Count) : 0;
        }

        /// <summary>
        /// What still has to be spent to bring <paramref name="current"/> up to <paramref name="target"/> -
        /// the difference between two ledgers, never negative.
        ///
        /// <para>This is what makes replaying a dungeon save's consumption <b>idempotent</b>, which it
        /// has to be: <c>InventoryManager</c> is a singleton that may or may not be marked
        /// <c>dontDestroyOnLoad</c> in a given scene. If it is destroyed, the resumed inventory comes
        /// back off disk at its level-start quantities and the whole ledger must be re-applied; if it
        /// survives, the potions are already gone and applying the ledger again would charge the
        /// player twice. Reconciling to a target handles both without either path having to know
        /// which case it is in.</para>
        /// </summary>
        public static List<ConsumableSpend> SpendShortfall(
            List<ConsumableSpend> current, List<ConsumableSpend> target)
        {
            var shortfall = new List<ConsumableSpend>();
            if (target == null)
            {
                return shortfall;
            }

            foreach (var entry in target)
            {
                if (entry == null || string.IsNullOrEmpty(entry.ItemKey))
                {
                    continue;
                }

                int outstanding = Mathf.Max(0, entry.Count) - SpendCount(current, entry.ItemKey);
                if (outstanding > 0)
                {
                    shortfall.Add(new ConsumableSpend { ItemKey = entry.ItemKey, Count = outstanding });
                }
            }

            return shortfall;
        }

        /// <summary>
        /// The ledger a reconcile leaves behind: every key at the higher of its two counts. A key
        /// already spent more than the target says keeps its own count, because there is no way to
        /// hand a consumable back.
        /// </summary>
        public static List<ConsumableSpend> MergeSpends(
            List<ConsumableSpend> current, List<ConsumableSpend> target)
        {
            var merged = new List<ConsumableSpend>();

            if (current != null)
            {
                foreach (var entry in current)
                {
                    if (entry != null && !string.IsNullOrEmpty(entry.ItemKey))
                    {
                        merged.Add(new ConsumableSpend
                        {
                            ItemKey = entry.ItemKey,
                            Count = Mathf.Max(0, entry.Count)
                        });
                    }
                }
            }

            if (target == null)
            {
                return merged;
            }

            foreach (var entry in target)
            {
                if (entry == null || string.IsNullOrEmpty(entry.ItemKey))
                {
                    continue;
                }

                int count = Mathf.Max(0, entry.Count);
                var existing = merged.Find(e => e.ItemKey == entry.ItemKey);
                if (existing == null)
                {
                    merged.Add(new ConsumableSpend { ItemKey = entry.ItemKey, Count = count });
                }
                else if (count > existing.Count)
                {
                    existing.Count = count;
                }
            }

            return merged;
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

        /// <summary>
        /// Sums the elemental resistance granted by a set of equipped items, one entry per damage type.
        /// Kept pure and beside <see cref="ComputeBonuses"/> so the balance model can fold gear into a
        /// hero without a live <see cref="InventoryManager"/>.
        /// </summary>
        public static List<Combat.Resistance> ComputeResistances(IEnumerable<ItemSO> equippedItems)
        {
            var totals = new Dictionary<Combat.DamageType, float>();
            if (equippedItems != null)
            {
                foreach (var so in equippedItems)
                {
                    if (so == null || so.Resistances == null)
                    {
                        continue;
                    }
                    foreach (var resistance in so.Resistances)
                    {
                        if (resistance == null)
                        {
                            continue;
                        }
                        if (!totals.ContainsKey(resistance.DamageType))
                        {
                            totals[resistance.DamageType] = 0f;
                        }
                        totals[resistance.DamageType] += resistance.Percent;
                    }
                }
            }

            var result = new List<Combat.Resistance>();
            foreach (var kvp in totals)
            {
                result.Add(new Combat.Resistance { DamageType = kvp.Key, Percent = kvp.Value });
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
