using System.Collections.Generic;
using Assets.Scripts.IO;
using ImmoralityGaming.Fundamentals;
using UnityEngine;

namespace Assets.Scripts.Items
{
    public class InventoryManager : SingletonBehaviour<InventoryManager>
    {
        [SerializeField]
        private List<ItemSO> _allItems;

        private FileHandler _fileHandler;
        private ItemCollectionSaveData _saveData;

        private void Start()
        {
            _fileHandler = new FileHandler();
            Load();
        }

        public void AddItem(ItemSO item)
        {
            _saveData.Items.Add(new ItemSaveData { ItemKey = item.Key });
            Save();
        }

        public void RemoveItem(string itemKey)
        {
            var index = _saveData.Items.FindIndex(x => x.ItemKey == itemKey);
            if (index >= 0)
            {
                _saveData.Items.RemoveAt(index);
                Save();
            }
        }

        public List<ItemSaveData> GetItems()
        {
            return _saveData.Items;
        }

        public ItemSO GetItemSO(string key)
        {
            return _allItems.Find(x => x.Key == key);
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
    }
}
