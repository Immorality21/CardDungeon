namespace Assets.Scripts.Items
{
    /// <summary>
    /// Broad kind of an <see cref="ItemSO"/>. Equipment slots into a hero's gear and grants
    /// stat <see cref="ItemBonus"/>es; consumables stack (quantity) and are spent for a
    /// one-shot <see cref="ConsumableEffectType"/> during combat; materials stack too but are
    /// never used in the field - they are the raw stuff a run brings home and the hub spends;
    /// recipes say what the Forge can turn that stuff into.
    /// </summary>
    public enum ItemCategory
    {
        Equipment,
        Consumable,

        /// <summary>
        /// Raw stuff: wood, iron, hide. Not a currency and not a
        /// <c>PartyResourceType</c> - materials are open-ended, per-type and stackable, which is
        /// exactly what the item system already models. Dropped by enemies (<c>EnemySO.LootTable</c>)
        /// and found in caches (<c>LevelDefinitionSO.MaterialTable</c>), banked on a level clear like
        /// every other drop, and spent at the hub. See <c>docs/plans/HUB.md</c> §7 machinery piece 1.
        /// </summary>
        Material,

        /// <summary>
        /// How to make something at the Forge. Found like loot (drop tables, a floor's
        /// <c>RecipeTable</c>) and banked like loot, and once in the bags it is <b>known</b>: crafting
        /// never uses it up, and a known recipe stops dropping. Never equipped or used in the field.
        /// See <see cref="CraftingOps"/>.
        /// </summary>
        Recipe,

        /// <summary>
        /// Has no direct use: it only opens something a room event asks for
        /// (<c>RoomEventOption.RequiredItem</c>), which spends it. Kept between runs until then, and
        /// listed with the materials. Banked on a level clear like every other find.
        /// </summary>
        KeyItem
    }
}
