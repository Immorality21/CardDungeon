namespace Assets.Scripts.Cards
{
    /// <summary>
    /// What a buff or debuff effect does. Three kinds live here: plain stat changes (which map to a
    /// <c>StatType</c> in <c>BuffHandlerRegistry</c>), elemental resistances, and status effects.
    ///
    /// <para><see cref="None"/> is 0 by convention. Serialized by ordinal in magic and combo assets,
    /// so append only.</para>
    /// </summary>
    public enum BuffType
    {
        None = 0,

        // Stat changes - one per scalable StatType.
        Strength = 1,
        Endurance = 2,
        Agility = 3,
        Intelligence = 4,
        Spirit = 5,
        Luck = 6,

        // Elemental resistances.
        FireResistance = 7,
        IceResistance = 8,
        LightningResistance = 9,
        HolyResistance = 10,
        ShadowResistance = 11,

        // Status effects.
        Frozen = 12,
        Slow = 13,
        Haste = 14
    }
}
