using System;
using UnityEngine;

namespace Assets.Scripts.Items
{
    /// <summary>
    /// A price in raw stuff: this many of that material. The counterpart to <see cref="LootDrop"/> —
    /// one authors what a run <i>yields</i>, the other what the hub <i>charges</i>.
    ///
    /// <para>Deliberately its own type rather than a reused drop entry: a cost has no probability and
    /// no range, and letting a building quote a "70% chance of 2-3 iron" is exactly the kind of thing
    /// that reads fine in an inspector and is impossible to explain to a player. Buildings
    /// (<c>docs/plans/HUB.md</c> §7) and sphere-grid nodes (§4c) are the two places this is headed.</para>
    /// </summary>
    [Serializable]
    public class MaterialCost
    {
        public ItemSO Material;

        [Min(1)]
        public int Amount = 1;

        public bool IsValid => Material != null && Material.Category == ItemCategory.Material && Amount > 0;
    }
}
