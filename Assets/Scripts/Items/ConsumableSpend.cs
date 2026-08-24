using System;

namespace Assets.Scripts.Items
{
    /// <summary>
    /// How many of one consumable the level being played has spent. The unit of the per-dungeon
    /// consumption ledger (<c>InventoryManager.GetDungeonConsumption</c>), written into
    /// <c>DungeonSaveData.ConsumablesSpent</c>.
    ///
    /// <para>A <b>delta, not a snapshot</b>, and deliberately so. The inventory itself is committed
    /// only on level clear, but the player can reach the hub while a run is paused and buy, sell or
    /// equip - so a dungeon save that restored absolute quantities would silently undo whatever they
    /// did there. Recording what the level spent composes with anything else that happened.</para>
    /// </summary>
    [Serializable]
    public class ConsumableSpend
    {
        public string ItemKey;
        public int Count;
    }
}
