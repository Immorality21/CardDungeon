namespace Assets.Scripts.UnitStats
{
    /// <summary>
    /// Every stat a unit can have, in one enum. This is the single source of truth: gear bonuses,
    /// buffs, level-up gains, spell scaling, hero/enemy authoring and the balance model's power
    /// weights all key off it.
    ///
    /// <para><b>Adding a stat is a member here plus a row in <see cref="StatCatalog"/></b>, and
    /// nothing else — labels, recruit pricing, power weights, authoring defaults, the inspector
    /// drawer and every analyzer table all read that row. Forget it and <c>StatCatalogTests</c>
    /// fails, which is the point: each of those used to be its own hand-kept list that failed
    /// silently and differently.</para>
    ///
    /// <para><see cref="None"/> is 0 by convention, so a default-initialised or freshly deserialized
    /// field means "unset" rather than silently meaning Strength.</para>
    ///
    /// <para><b>Serialized by ordinal</b> in item, magic, hero and enemy assets. Append only —
    /// reordering or inserting shifts every existing asset's meaning, and any such change has to
    /// rewrite those assets in the same commit.</para>
    ///
    /// <para><see cref="MaxHealth"/> is a stat because gear and level-ups raise it. *Current* health
    /// is not: it is a consumable resource and lives on <see cref="Stats"/> as its own field.</para>
    /// </summary>
    public enum StatType
    {
        None = 0,

        /// <summary>Physical power. Scales melee-flavoured attacks and spells.</summary>
        Strength = 1,

        /// <summary>Damage reduction, through the diminishing curve in <c>DamageCalculator</c>.</summary>
        Endurance = 2,

        /// <summary>Turn frequency — <c>TurnManager</c> schedules on it.</summary>
        Agility = 3,

        /// <summary>Scales offensive/arcane spell power.</summary>
        Intelligence = 4,

        /// <summary>Scales restorative and protective spell power.</summary>
        Spirit = 5,

        /// <summary>Raises crit chance, and improves stat checks on room events.</summary>
        Luck = 6,

        /// <summary>Size of the health bar. Current health is a resource, not a stat.</summary>
        MaxHealth = 7
    }
}
