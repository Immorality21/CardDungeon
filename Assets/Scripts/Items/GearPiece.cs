using System.Collections.Generic;

namespace Assets.Scripts.Items
{
    /// <summary>
    /// One copy of a piece of equipment: its definition and its own save entry. The entry carries the
    /// copy's counters (<see cref="ItemGrowth"/>), so two copies of the same <see cref="ItemSO"/> can
    /// grant different stats. Anything that shows the stats a hero actually fights with works from
    /// pieces; the balance model, which fights with fresh gear, keeps plain <see cref="ItemSO"/> lists.
    /// </summary>
    public sealed class GearPiece
    {
        public readonly ItemSO Item;

        /// <summary>This copy's save entry, or null for a fresh copy (a shop's stock).</summary>
        public readonly ItemSaveData Entry;

        public GearPiece(ItemSO item, ItemSaveData entry)
        {
            Item = item;
            Entry = entry;
        }

        /// <summary>Every bonus this copy grants: the item's own plus each milestone it has reached.</summary>
        public IEnumerable<ItemBonus> Bonuses => ItemGrowth.BonusesOf(Item, Entry);

        /// <summary>The definitions alone, for what growth does not touch (resistances, names).</summary>
        public static List<ItemSO> ItemsOf(IEnumerable<GearPiece> gear)
        {
            var result = new List<ItemSO>();
            if (gear == null)
            {
                return result;
            }
            foreach (var piece in gear)
            {
                if (piece?.Item != null)
                {
                    result.Add(piece.Item);
                }
            }
            return result;
        }
    }
}
