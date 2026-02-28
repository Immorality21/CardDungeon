using System;
using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.IO;
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
        private Dictionary<string, Dictionary<SlotType, ItemSaveData>> _equipped =
            new Dictionary<string, Dictionary<SlotType, ItemSaveData>>();

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

        public ItemSaveData GetEquipped(SlotType slot, string heroKey)
        {
            if (_equipped.TryGetValue(heroKey, out var slots))
            {
                slots.TryGetValue(slot, out var item);
                return item;
            }
            return null;
        }

        public void Equip(ItemSaveData item, SlotType slot, string heroKey)
        {
            var so = GetItemSO(item.ItemKey);
            if (so == null || so.SlotType != slot)
            {
                return;
            }

            // Unequip existing item in that slot for this hero
            Unequip(slot, heroKey);

            item.EquippedSlot = slot.ToString();
            item.EquippedHeroKey = heroKey;

            if (!_equipped.ContainsKey(heroKey))
            {
                _equipped[heroKey] = new Dictionary<SlotType, ItemSaveData>();
            }
            _equipped[heroKey][slot] = item;

            Save();
            OnInventoryChanged?.Invoke();
        }

        public void Unequip(SlotType slot, string heroKey)
        {
            if (_equipped.TryGetValue(heroKey, out var slots))
            {
                if (slots.TryGetValue(slot, out var existing))
                {
                    existing.EquippedSlot = null;
                    existing.EquippedHeroKey = null;
                    slots.Remove(slot);
                    Save();
                    OnInventoryChanged?.Invoke();
                }
            }
        }

        public Dictionary<StatType, float> ComputeRawBonuses(string heroKey)
        {
            var raw = new Dictionary<StatType, float>();
            foreach (StatType stat in Enum.GetValues(typeof(StatType)))
            {
                raw[stat] = 0f;
            }

            if (!_equipped.TryGetValue(heroKey, out var slots))
            {
                return raw;
            }

            foreach (var kvp in slots)
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

        public Dictionary<StatType, float> ComputePercentageBonuses(string heroKey)
        {
            var pct = new Dictionary<StatType, float>();
            foreach (StatType stat in Enum.GetValues(typeof(StatType)))
            {
                pct[stat] = 0f;
            }

            if (!_equipped.TryGetValue(heroKey, out var slots))
            {
                return pct;
            }

            foreach (var kvp in slots)
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
                    var heroKey = item.EquippedHeroKey ?? "";
                    if (!_equipped.ContainsKey(heroKey))
                    {
                        _equipped[heroKey] = new Dictionary<SlotType, ItemSaveData>();
                    }
                    _equipped[heroKey][slot] = item;
                }
            }
        }
    }
}
