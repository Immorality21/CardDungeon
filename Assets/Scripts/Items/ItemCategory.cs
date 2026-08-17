namespace Assets.Scripts.Items
{
    /// <summary>
    /// Broad kind of an <see cref="ItemSO"/>. Equipment slots into a hero's gear and grants
    /// stat <see cref="ItemBonus"/>es; consumables stack (quantity) and are spent for a
    /// one-shot <see cref="ConsumableEffectType"/> during combat.
    /// </summary>
    public enum ItemCategory
    {
        Equipment,
        Consumable
    }
}
