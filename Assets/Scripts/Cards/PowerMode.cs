namespace Assets.Scripts.Cards
{
    /// <summary>
    /// How a <see cref="SpellEffect"/>'s <c>Power</c> is read. Applies to Damage, Heal and
    /// HealthCost effects; buff and debuff magnitudes are stat deltas and ignore it.
    ///
    /// <para><see cref="BasePower"/> is 0, so every asset authored before this field existed keeps
    /// its exact behaviour — the mode is purely additive.</para>
    /// </summary>
    public enum PowerMode
    {
        /// <summary>Power is a base the caster's <c>ScalingStat</c> is added to. Today's behaviour.</summary>
        BasePower = 0,

        /// <summary>Exactly <c>Power</c>, with no caster contribution. Same rule as a combo's bonus effect.</summary>
        Flat = 1,

        /// <summary>
        /// <c>Power</c> is a percentage of the max health of the unit the effect lands on — the target
        /// for Damage/Heal, the caster for HealthCost. Rounds down, floor of 1, so a percentage effect
        /// scales with the party for the rest of the game's life instead of going stale.
        /// </summary>
        PercentOfMaxHealth = 2
    }
}
