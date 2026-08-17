using System;

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
    }
}
