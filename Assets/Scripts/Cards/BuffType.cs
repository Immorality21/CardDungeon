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
        Haste = 14,
        // Over-time effects. These carry a per-turn amount in CombatBuff.Amount and are resolved by
        // CombatBuffTracker.ResolveOverTime, which asks the registry whether a handler implements
        // IOverTimeBuffHandler. Appended, because BuffType is serialized by ordinal into every magic
        // and combo asset.
        Burning = 15,
        Poisoned = 16,
        Bleeding = 17,
        Regenerating = 18,
        // Command gate: blocks casting for a hero and every CastMagic action for an enemy.
        Silenced = 19,
        // Miss chance: a blinded unit's physical hits miss half the time (DefenseRules.BlindMissChance).
        Blinded = 20,
        // Resistance to every element, Normal included: Power% less from every hit, before defense.
        // The Warrior's Take Cover. Not a status - it rides the resistance channel.
        Guarded = 21,
        // A pool of points soaking damage before the bar (Combat.BarrierTable): the Cleric's Sacred
        // Shield and the Guardian Angel. Not a status - it lives on the fight's event stream.
        Barrier = 22,
        // The Warlock's Leeching Curse: a share of every hit the curser's side lands on the unit heals
        // the curser (Cards.LeechTracker). The percent rides in CombatBuff.Amount, the curser in
        // CombatBuff.Source. A status, so it is curable, immune-able and on the HP bar; it never ticks.
        Leeched = 23,
        // The Cultist's Mass Hysteria: on its next turn the unit may strike one of its own allies
        // instead (ConfusedBuffHandler.Chance). A status, so it is curable and immune-able.
        Confused = 24,
        // The Ranger's Hunter's Mark: the unit takes Amount percent more from every hit, read where
        // resistance is (CombatBuffTracker.GetResistanceBonus). The percent rides in CombatBuff.Amount.
        // A status: curable, immune-able, on the HP bar; it never ticks.
        Marked = 25,
        // The Rogue's Vanish: enemies cannot pick the unit as a target (CombatManager.TargetableHeroes).
        Hidden = 26,
        // The Rogue's Shadowstep and Smoke and Mirrors: the next Amount physical blows aimed at the unit
        // miss outright (EvasiveBuffHandler.TryEvade), before its Luck dodge is rolled.
        Evasive = 27,
        // The Rogue's Death Mark: every damaging hit on the unit is a critical one, and harder than a
        // normal crit (CritRules.MarkedMultiplier).
        DeathMarked = 28
    }
}
