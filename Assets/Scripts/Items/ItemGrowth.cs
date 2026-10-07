using System;
using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.Items
{
    /// <summary>One counter an item instance keeps in its save entry: a key and how far it has got.</summary>
    [Serializable]
    public class ItemCounter
    {
        public string Key;
        public int Value;
    }

    /// <summary>
    /// What the game counts on a worn item. The save stores counters as generic key/value pairs
    /// (<see cref="ItemSaveData.Counters"/>), so a new kind is one member here plus one place that
    /// feeds it - nothing in the save format changes. Serialized by ordinal: append, never insert.
    /// </summary>
    public enum ItemCounterKind
    {
        /// <summary>Enemies the wearer has felled (their blow, or their poison).</summary>
        Kills = 0,

        /// <summary>Bosses the wearer has felled.</summary>
        BossKills = 1,

        /// <summary>Fights won while it was worn by a fielded hero.</summary>
        Victories = 2
    }

    /// <summary>
    /// A threshold on one of an item's counters and what reaching it grants: "after 50 kills, +3
    /// Strength". Bonuses add to the item's own <see cref="ItemSO.Bonuses"/> through the same gear
    /// path, so everything that reads gear stats sees them.
    /// </summary>
    [Serializable]
    public class ItemMilestone
    {
        [Tooltip("Which counter this milestone reads.")]
        public ItemCounterKind Counter;

        [Tooltip("Reached when the counter is at least this.")]
        [Min(1)] public int Threshold = 1;

        [Tooltip("Granted on top of the item's own bonuses once reached, for as long as it is worn.")]
        public List<ItemBonus> Bonuses = new List<ItemBonus>();
    }

    /// <summary>
    /// The rules for items that grow with use (docs/plans/EVENTS.md). Pure: the counters live on the
    /// item's save entry, so an item keeps its history when it changes hands, and is forfeited with the
    /// rest of a level's gains when the level is (the entry reverts with the inventory on a wipe).
    /// <c>InventoryManager</c> feeds the counters from the game's event stream; nothing in combat
    /// knows they exist.
    /// </summary>
    public static class ItemGrowth
    {
        /// <summary>The key a counter is stored under. Stable strings, never the enum's ordinal, so the
        /// save reads the same if the enum is ever reordered.</summary>
        public static string KeyOf(ItemCounterKind kind)
        {
            switch (kind)
            {
                case ItemCounterKind.Kills:
                    return "kills";
                case ItemCounterKind.BossKills:
                    return "boss-kills";
                case ItemCounterKind.Victories:
                    return "victories";
                default:
                    return kind.ToString();
            }
        }

        /// <summary>The value of <paramref name="key"/> on this entry (0 when it has never counted).</summary>
        public static int Get(ItemSaveData entry, string key)
        {
            if (entry?.Counters == null || string.IsNullOrEmpty(key))
            {
                return 0;
            }
            var counter = entry.Counters.Find(c => c != null && c.Key == key);
            return counter != null ? counter.Value : 0;
        }

        public static int Get(ItemSaveData entry, ItemCounterKind kind)
        {
            return Get(entry, KeyOf(kind));
        }

        /// <summary>Adds <paramref name="amount"/> to <paramref name="key"/> on this entry.</summary>
        public static void Add(ItemSaveData entry, string key, int amount)
        {
            if (entry == null || string.IsNullOrEmpty(key) || amount == 0)
            {
                return;
            }
            if (entry.Counters == null)
            {
                entry.Counters = new List<ItemCounter>();
            }
            var counter = entry.Counters.Find(c => c != null && c.Key == key);
            if (counter == null)
            {
                entry.Counters.Add(new ItemCounter { Key = key, Value = amount });
            }
            else
            {
                counter.Value += amount;
            }
        }

        /// <summary>Whether the item counts <paramref name="kind"/> at all - only an item with a
        /// milestone on it keeps the counter, so the save holds nothing nobody reads.</summary>
        public static bool Counts(ItemSO item, ItemCounterKind kind)
        {
            return item?.Milestones != null && item.Milestones.Exists(m => m != null && m.Counter == kind);
        }

        /// <summary>The milestones this entry has reached.</summary>
        public static IEnumerable<ItemMilestone> Reached(ItemSO item, ItemSaveData entry)
        {
            if (item?.Milestones == null)
            {
                yield break;
            }
            foreach (var milestone in item.Milestones)
            {
                if (milestone != null && Get(entry, milestone.Counter) >= milestone.Threshold)
                {
                    yield return milestone;
                }
            }
        }

        /// <summary>Every bonus this entry grants while worn: the item's own, then each reached
        /// milestone's. With no entry (the balance model's fresh gear) that is the item's own alone.</summary>
        public static IEnumerable<ItemBonus> BonusesOf(ItemSO item, ItemSaveData entry)
        {
            if (item == null)
            {
                yield break;
            }
            if (item.Bonuses != null)
            {
                foreach (var bonus in item.Bonuses)
                {
                    yield return bonus;
                }
            }
            foreach (var milestone in Reached(item, entry))
            {
                if (milestone.Bonuses == null)
                {
                    continue;
                }
                foreach (var bonus in milestone.Bonuses)
                {
                    yield return bonus;
                }
            }
        }
    }
}
