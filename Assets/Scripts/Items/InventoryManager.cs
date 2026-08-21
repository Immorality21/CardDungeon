using System;
using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.IO;
using Assets.Scripts.UnitStats;
using ImmoralityGaming.Fundamentals;
using UnityEngine;

namespace Assets.Scripts.Items
{
    public class InventoryManager : SingletonBehaviour<InventoryManager>
    {
        [SerializeField]
        [Tooltip("Optional scene-wired item list. The Resources ItemCatalog is merged in on top, so " +
                 "the manager resolves every item even when auto-created in a scene with no wiring (the hub).")]
        private List<ItemSO> _allItems;

        // Key -> ItemSO lookup, built once from _allItems plus the Resources ItemCatalog.
        private Dictionary<string, ItemSO> _itemsByKey;

        private FileHandler _fileHandler;
        private ItemCollectionSaveData _saveData;
        private Dictionary<string, Dictionary<SlotType, ItemSaveData>> _equipped =
            new Dictionary<string, Dictionary<SlotType, ItemSaveData>>();

        public event Action OnInventoryChanged;

        private bool _deferSaves;

        // Load in Awake (like MetaProgressManager / PartyResourceManager) so an auto-created
        // instance — e.g. the hub MenuScene has no wired Managers prefab — is immediately usable.
        protected override void Awake()
        {
            base.Awake();
            _fileHandler = new FileHandler();
            Load();
        }

        public void SetDeferSaves(bool defer)
        {
            _deferSaves = defer;
        }

        public void CommitInventory()
        {
            Save();
        }

        public void AddItem(ItemSO item)
        {
            if (item == null)
            {
                return;
            }

            InventoryOperations.AddItem(_saveData.Items, item);

            if (!_deferSaves)
            {
                Save();
            }
            OnInventoryChanged?.Invoke();
        }

        public void RemoveItem(string itemKey)
        {
            var index = _saveData.Items.FindIndex(x => x.ItemKey == itemKey);
            if (index >= 0)
            {
                _saveData.Items.RemoveAt(index);
                if (!_deferSaves)
                {
                    Save();
                }
                OnInventoryChanged?.Invoke();
            }
        }

        /// <summary>
        /// Removes one <b>un-equipped</b> equipment entry with this key (a "bag" copy), never an
        /// item a hero has equipped. Used by selling so a hero can't be stripped by selling a
        /// duplicate. Returns true if a bag copy was found and removed.
        /// </summary>
        public bool RemoveBagEquipment(string itemKey)
        {
            var index = _saveData.Items.FindIndex(i =>
                i.ItemKey == itemKey &&
                string.IsNullOrEmpty(i.EquippedSlot) &&
                IsCategory(i, ItemCategory.Equipment));

            if (index < 0)
            {
                return false;
            }

            _saveData.Items.RemoveAt(index);
            if (!_deferSaves)
            {
                Save();
            }
            OnInventoryChanged?.Invoke();
            return true;
        }

        public List<ItemSaveData> GetItems()
        {
            return _saveData.Items;
        }

        public List<ItemSaveData> GetBagItems()
        {
            return _saveData.Items.Where(i => string.IsNullOrEmpty(i.EquippedSlot)).ToList();
        }

        /// <summary>Un-equipped equipment items (the equipment "bag").</summary>
        public List<ItemSaveData> GetBagEquipment()
        {
            return _saveData.Items
                .Where(i => string.IsNullOrEmpty(i.EquippedSlot) && IsCategory(i, ItemCategory.Equipment))
                .ToList();
        }

        /// <summary>All consumable stacks the party is carrying.</summary>
        public List<ItemSaveData> GetConsumables()
        {
            return _saveData.Items.Where(i => IsCategory(i, ItemCategory.Consumable)).ToList();
        }

        /// <summary>Total carried quantity of a consumable across stacks (0 if none).</summary>
        public int GetConsumableQuantity(string itemKey)
        {
            return InventoryOperations.GetConsumableQuantity(_saveData.Items, itemKey, GetItemSO);
        }

        /// <summary>Whether the party carries at least one usable consumable.</summary>
        public bool HasAnyConsumable()
        {
            return _saveData.Items.Any(i => IsCategory(i, ItemCategory.Consumable) && i.Quantity > 0);
        }

        /// <summary>
        /// Spends one unit of a consumable, removing the stack when it hits zero. Returns false if
        /// none are carried. Respects deferred saves (spending happens in-dungeon).
        /// </summary>
        public bool TryConsume(string itemKey)
        {
            if (!InventoryOperations.TryConsume(_saveData.Items, itemKey, GetItemSO))
            {
                return false;
            }

            if (!_deferSaves)
            {
                Save();
            }
            OnInventoryChanged?.Invoke();
            return true;
        }

        /// <summary>
        /// Refills a consumable up to <paramref name="cap"/> total quantity (the "belt" top-up run
        /// on fresh dungeon entry). Never reduces an existing surplus.
        /// </summary>
        public void TopUpConsumableToCap(ItemSO item, int cap)
        {
            int before = item != null ? GetConsumableQuantity(item.Key) : 0;
            InventoryOperations.TopUpConsumableToCap(_saveData.Items, item, cap, GetItemSO);
            if (item == null || GetConsumableQuantity(item.Key) == before)
            {
                return;
            }

            if (!_deferSaves)
            {
                Save();
            }
            OnInventoryChanged?.Invoke();
        }

        private bool IsCategory(ItemSaveData item, ItemCategory category)
        {
            var so = GetItemSO(item.ItemKey);
            return so != null && so.Category == category;
        }

        public ItemSO GetItemSO(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return null;
            }
            EnsureItemLookup();
            _itemsByKey.TryGetValue(key, out var so);
            return so;
        }

        /// <summary>
        /// Builds the key→ItemSO lookup from the scene-wired list plus the Resources catalog.
        /// Idempotent; the catalog lets the hub (no wired manager) still resolve every item.
        /// </summary>
        private void EnsureItemLookup()
        {
            if (_itemsByKey != null)
            {
                return;
            }

            _itemsByKey = new Dictionary<string, ItemSO>();

            if (_allItems != null)
            {
                foreach (var so in _allItems)
                {
                    AddToLookup(so);
                }
            }

            // Qualify UnityEngine.Resources — the project has its own Assets.Scripts.Resources namespace.
            var catalog = UnityEngine.Resources.Load<ItemCatalogSO>(ItemCatalogSO.ResourcePath);
            if (catalog != null)
            {
                foreach (var so in catalog.Items)
                {
                    AddToLookup(so);
                }
            }
        }

        private void AddToLookup(ItemSO so)
        {
            if (so != null && !string.IsNullOrEmpty(so.Key) && !_itemsByKey.ContainsKey(so.Key))
            {
                _itemsByKey[so.Key] = so;
            }
        }

        /// <summary>Every item known to the catalog (used by the hub inventory to list acquirable gear).</summary>
        public IEnumerable<ItemSO> AllItems()
        {
            EnsureItemLookup();
            return _itemsByKey.Values;
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
            if (so == null || so.Category != ItemCategory.Equipment || so.SlotType != slot)
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

            if (!_deferSaves)
            {
                Save();
            }
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
                    if (!_deferSaves)
                    {
                        Save();
                    }
                    OnInventoryChanged?.Invoke();
                }
            }
        }

        public Dictionary<StatType, float> ComputeRawBonuses(string heroKey)
        {
            return ComputeBonuses(heroKey, BonusType.Raw);
        }

        public Dictionary<StatType, float> ComputePercentageBonuses(string heroKey)
        {
            return ComputeBonuses(heroKey, BonusType.Percentage);
        }

        /// <summary>Every equipment ScriptableObject a hero currently has equipped.</summary>
        public List<ItemSO> GetEquippedItems(string heroKey)
        {
            var equipped = new List<ItemSO>();
            if (_equipped.TryGetValue(heroKey, out var slots))
            {
                foreach (var kvp in slots)
                {
                    var so = GetItemSO(kvp.Value.ItemKey);
                    if (so != null)
                    {
                        equipped.Add(so);
                    }
                }
            }
            return equipped;
        }

        /// <summary>Elemental resistance a hero's equipped gear grants, summed per damage type.</summary>
        public List<Combat.Resistance> ComputeResistances(string heroKey)
        {
            return InventoryOperations.ComputeResistances(GetEquippedItems(heroKey));
        }

        private Dictionary<StatType, float> ComputeBonuses(string heroKey, BonusType bonusType)
        {
            return InventoryOperations.ComputeBonuses(GetEquippedItems(heroKey), bonusType);
        }

        public void Save()
        {
            _fileHandler.Save(_saveData);
        }

        public void Load()
        {
            _saveData = _fileHandler.Load<ItemCollectionSaveData>();
            InventoryOperations.NormalizeQuantities(_saveData.Items);
            RebuildEquippedCache();
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
