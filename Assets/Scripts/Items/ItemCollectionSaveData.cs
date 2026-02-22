using System;
using System.Collections.Generic;
using Assets.Scripts.IO;

namespace Assets.Scripts.Items
{
    [Serializable]
    public class ItemCollectionSaveData : IWriteable
    {
        public List<ItemSaveData> Items = new List<ItemSaveData>();

        public string GetFileName()
        {
            return "ItemCollection";
        }
    }
}
