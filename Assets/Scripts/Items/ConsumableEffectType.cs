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
        CureStatus = 1,

        /// <summary>
        /// Gives <c>ItemSO.ConsumableCharges</c> charges back to the drinker's most-spent ability -
        /// the Energy Potion (playtest 3 finding 5). Charges are a run resource only a refuge
        /// refills, which is why it is a scarce belt slot and not a drop.
        /// </summary>
        RestoreCharge = 2,

        /// <summary>Heals like <see cref="RestoreHealth"/> and restores charges like <see cref="RestoreCharge"/> - the Elixir.</summary>
        HealAndCharge = 3
    }
}
