using System;
using System.Collections.Generic;

namespace Assets.Scripts.Items
{
    [Serializable]
    public class ItemSaveData
    {
        public string ItemKey;
        public string EquippedSlot;
        public string EquippedHeroKey;

        // Stack size for consumables (equipment is always one entry per item, quantity 1).
        // Old saves predate this field: JsonUtility leaves it 0, normalized to 1 on load.
        public int Quantity = 1;

        /// <summary>
        /// What this one item has counted - kills made wearing it, say - as generic key/value pairs
        /// (<see cref="ItemGrowth"/>). Per entry, so each sword keeps its own history and keeps it when
        /// handed to another hero. Empty for anything without milestones.
        /// </summary>
        public List<ItemCounter> Counters = new List<ItemCounter>();
    }
}
