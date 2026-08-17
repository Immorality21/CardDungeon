namespace Assets.Scripts.Items
{
    /// <summary>
    /// What a consumable <see cref="ItemSO"/> does when used. Kept deliberately small for now
    /// (extend with e.g. CureStatus, RestoreToFull, Revive as the combat item command grows).
    /// </summary>
    public enum ConsumableEffectType
    {
        RestoreHealth
    }
}
