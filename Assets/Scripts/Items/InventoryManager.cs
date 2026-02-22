using System;
using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.IO;
using Assets.Scripts.Items.UI;
using ImmoralityGaming.Fundamentals;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Assets.Scripts.Items
{
    public class InventoryManager : SingletonBehaviour<InventoryManager>
    {
        [SerializeField]
        private List<ItemSO> _allItems;

        private FileHandler _fileHandler;
        private ItemCollectionSaveData _saveData;
        private Dictionary<SlotType, ItemSaveData> _equipped = new Dictionary<SlotType, ItemSaveData>();

        public event Action OnInventoryChanged;

        private void Start()
        {
            _fileHandler = new FileHandler();
            Load();
            RebuildEquippedCache();
        }

        public void AddItem(ItemSO item)
        {
            _saveData.Items.Add(new ItemSaveData { ItemKey = item.Key });
            Save();
            OnInventoryChanged?.Invoke();
        }

        public void RemoveItem(string itemKey)
        {
            var index = _saveData.Items.FindIndex(x => x.ItemKey == itemKey);
            if (index >= 0)
            {
                _saveData.Items.RemoveAt(index);
                Save();
                OnInventoryChanged?.Invoke();
            }
        }

        public List<ItemSaveData> GetItems()
        {
            return _saveData.Items;
        }

        public List<ItemSaveData> GetBagItems()
        {
            return _saveData.Items.Where(i => string.IsNullOrEmpty(i.EquippedSlot)).ToList();
        }

        public ItemSO GetItemSO(string key)
        {
            return _allItems.Find(x => x.Key == key);
        }

        public ItemSaveData GetEquipped(SlotType slot)
        {
            _equipped.TryGetValue(slot, out var item);
            return item;
        }

        public void Equip(ItemSaveData item, SlotType slot)
        {
            var so = GetItemSO(item.ItemKey);
            if (so == null || so.SlotType != slot)
            {
                return;
            }

            // Unequip existing item in that slot
            Unequip(slot);

            item.EquippedSlot = slot.ToString();
            _equipped[slot] = item;
            Save();
            OnInventoryChanged?.Invoke();
        }

        public void Unequip(SlotType slot)
        {
            if (_equipped.TryGetValue(slot, out var existing))
            {
                existing.EquippedSlot = null;
                _equipped.Remove(slot);
                Save();
                OnInventoryChanged?.Invoke();
            }
        }

        public Dictionary<StatType, float> ComputeRawBonuses()
        {
            var raw = new Dictionary<StatType, float>();
            foreach (StatType stat in Enum.GetValues(typeof(StatType)))
            {
                raw[stat] = 0f;
            }

            foreach (var kvp in _equipped)
            {
                var so = GetItemSO(kvp.Value.ItemKey);
                if (so == null)
                {
                    continue;
                }

                foreach (var bonus in so.Bonuses)
                {
                    if (bonus.BonusType == BonusType.Raw)
                    {
                        raw[bonus.StatType] += bonus.Value;
                    }
                }
            }

            return raw;
        }

        public Dictionary<StatType, float> ComputePercentageBonuses()
        {
            var pct = new Dictionary<StatType, float>();
            foreach (StatType stat in Enum.GetValues(typeof(StatType)))
            {
                pct[stat] = 0f;
            }

            foreach (var kvp in _equipped)
            {
                var so = GetItemSO(kvp.Value.ItemKey);
                if (so == null)
                {
                    continue;
                }

                foreach (var bonus in so.Bonuses)
                {
                    if (bonus.BonusType == BonusType.Percentage)
                    {
                        pct[bonus.StatType] += bonus.Value;
                    }
                }
            }

            return pct;
        }

        public void Save()
        {
            _fileHandler.Save(_saveData);
        }

        public void Load()
        {
            _saveData = _fileHandler.Load<ItemCollectionSaveData>();
        }

        public void TryDropItem(ItemSO item)
        {
            if (item == null)
            {
                return;
            }

            if (Random.Range(0f, 1f) < 0.5f)
            {
                AddItem(item);
                Debug.Log($"Item dropped: {item.DisplayName} ({item.Key})");
            }
        }

        private void RebuildEquippedCache()
        {
            _equipped.Clear();
            foreach (var item in _saveData.Items)
            {
                if (!string.IsNullOrEmpty(item.EquippedSlot) &&
                    Enum.TryParse<SlotType>(item.EquippedSlot, out var slot))
                {
                    _equipped[slot] = item;
                }
            }
        }
    }
}
