using System;
using UnityEngine;

namespace Assets.Scripts.Items
{
    /// <summary>
    /// One line of a drop table: an item, how likely it is, and how much of it falls. Used by
    /// <c>EnemySO.LootTable</c> (what a monster is made of) and <c>LevelDefinitionSO.MaterialTable</c>
    /// (what the place is made of, found in caches).
    ///
    /// <para><b>Two chance regimes, on purpose.</b> Leave <see cref="Chance"/> at 0 and the entry
    /// rolls through <see cref="LootRoller.DropChance(ItemSO,int)"/> — rarity and run depth — which is
    /// how every gear drop behaved before drop tables existed and how it still behaves. Set it above
    /// zero and it is that flat probability instead, which is what materials want: a material is
    /// gated by <i>where</i> the player has been, not by how deep they are, so a boss can be authored
    /// to always yield its signature stuff and a trash mob to yield its scrap a third of the time.</para>
    /// </summary>
    [Serializable]
    public class LootDrop
    {
        public ItemSO Item;

        [Tooltip("Flat drop probability. Leave at 0 to fall back on the rarity + run-depth math in " +
                 "LootRoller, which is what gear uses. 1 is a guaranteed drop.")]
        [Range(0f, 1f)]
        public float Chance;

        [Tooltip("Fewest units dropped when this entry hits. Only stacking items (consumables and " +
                 "materials) can exceed 1 - equipment is always one entry per drop.")]
        [Min(1)]
        public int MinQuantity = 1;

        [Tooltip("Most units dropped when this entry hits, inclusive.")]
        [Min(1)]
        public int MaxQuantity = 1;

        /// <summary>Whether this entry sets its own flat probability instead of using rarity + depth.</summary>
        public bool HasExplicitChance => Chance > 0f;
    }

    /// <summary>
    /// What one drop-table entry actually paid out: the item and how many of it. Separate from
    /// <see cref="LootDrop"/> because a table is authored content and an award is a rolled result -
    /// the victory summary and the bestiary want the latter.
    /// </summary>
    public readonly struct LootAward
    {
        public readonly ItemSO Item;
        public readonly int Quantity;

        public LootAward(ItemSO item, int quantity)
        {
            Item = item;
            Quantity = quantity;
        }

        public bool IsEmpty => Item == null || Quantity <= 0;
    }
}
