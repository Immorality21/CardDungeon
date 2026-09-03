namespace Assets.Scripts.Items
{
    /// <summary>
    /// What a consumable <see cref="ItemSO"/> does when used.
    ///
    /// <para>Serialized by ordinal on item assets — <b>append only</b>.</para>
    /// </summary>
    public enum ConsumableEffectType
    {
        RestoreHealth = 0,

        /// <summary>
        /// Clears every curable status effect from the target — burn, poison, bleed, freeze, slow,
        /// silence. See <c>BuffHandlerRegistry.IsCurable</c> for why the party's own Haste and
        /// Regeneration are deliberately left alone.
        ///
        /// <para>This is the counterplay half of the over-time layer, not a nicety: without a cure,
        /// an enemy's damage-over-time is a one-way ratchet the player has no answer to, which reads
        /// as unfair rather than tactical.</para>
        /// </summary>
        CureStatus = 1
    }
}
