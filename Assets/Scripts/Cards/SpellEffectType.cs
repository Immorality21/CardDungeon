namespace Assets.Scripts.Cards
{
    public enum SpellEffectType
    {
        Damage = 0,
        Heal = 1,
        Buff = 2,
        Debuff = 3,

        /// <summary>
        /// Charges the <b>caster</b> health, ignoring defense and resistance. Appended, because these
        /// values are serialized by ordinal into every magic and combo asset. Resolved after the
        /// effects it pays for — see <see cref="EffectResolver"/>.
        /// </summary>
        HealthCost = 4,

        /// <summary>
        /// Pushes each target's next turn back by <c>Power</c> percent of its own full turn - a true
        /// delay, landing on the clock at once, unlike Slow, which only stretches the turns after the
        /// next. Capped so no unit is ever more than one extra full turn away from acting
        /// (<see cref="Combat.TurnManager.Delay"/>). Needs a clock: inert through a resolver that has
        /// none (<see cref="EffectResolver.Clock"/>). Appended, for the reason above.
        /// </summary>
        TurnDelay = 5,

        /// <summary>
        /// Heals the <b>caster</b> <c>Power</c> percent of the damage the rest of this cast dealt -
        /// the Warlock's Drain Life is a Damage effect plus this one. Resolved after every other
        /// benefit, so it reads the cast's damage whatever order the effects are authored in, and
        /// before the costs. No caster stat and no upgrade bonus go into the percentage.
        /// Appended, for the reason above.
        /// </summary>
        Drain = 6,

        /// <summary>
        /// Gives <c>Power</c> charges back to one ability the target carries, capped at its maximum:
        /// the Warlock's Life Tap. The player picks which (<see cref="SpellcastAction.ChargeSlot"/>);
        /// with no pick the most-spent slot is chosen. Needs a charge bank
        /// (<see cref="EffectResolver.Charges"/>): inert through a resolver that has none.
        /// Appended, for the reason above.
        /// </summary>
        RestoreCharge = 7,

        /// <summary>
        /// The Tinkerer's Disassemble: takes a <see cref="Combat.UnitTraits.Mechanical"/> target apart
        /// outright, with a chance that grows with how many of that enemy the Bestiary has recorded
        /// killed (<see cref="Combat.DisassembleOps"/>). A success removes it as a kill credited to the
        /// caster and also yields its salvage (<c>EnemySO.SalvageTable</c>); a failure, or a target that
        /// is not a machine, does nothing. <c>Power</c> is unused. Appended, for the reason above.
        /// </summary>
        Disassemble = 8,

        /// <summary>
        /// Brings a fallen hero back to their feet at <c>Power</c> percent of their max health (at least
        /// 1), back on the clock - the Cleric's Revive. Single-target casts revive the fallen ally picked
        /// (<see cref="MagicTargetType.FallenAlly"/>); a party-wide one revives every fallen hero of the
        /// caster's side (the Phoenix). Needs the fight's list of the fallen
        /// (<see cref="EffectResolver.FallenAllies"/>). Appended, for the reason above.
        /// </summary>
        Revive = 9,

        /// <summary>
        /// Clears debuffs from each target: curable statuses and stat cuts, at most <c>Power</c> of them,
        /// or every one when <c>Power</c> is 0 (Miracle of Light, the awakened Benediction). Appended.
        /// </summary>
        Cure = 10,

        /// <summary>
        /// Destroys each target standing at or under <c>Power</c> percent of its max health, if it has
        /// <see cref="SpellEffect.BonusVsTrait"/> (any unit when that is None). Bosses are never destroyed.
        /// Put it after a damage effect and the blow sets it up (Miracle of Righteousness). Appended.
        /// </summary>
        Execute = 11,

        /// <summary>
        /// The caster steps in front of each ally target: single-target blows aimed at them land on the
        /// caster until the caster's next turn, and the caster takes <c>Power</c> percent less from every
        /// hit for one turn. The Paladin's Divine Intervention. Appended.
        /// </summary>
        Intercept = 12,

        /// <summary>
        /// Readies the caster to strike back once at the first enemy whose blow lands on them before their
        /// next turn: <c>Power</c> + <c>ScalingStat</c> as <c>DamageType</c>. Appended.
        /// </summary>
        Retaliate = 13,

        /// <summary>
        /// Thorns on each target: <c>Power</c> percent of every enemy blow that lands on it goes back at
        /// the attacker as <c>DamageType</c>, for <c>Duration</c> of its turns
        /// (<see cref="Combat.ThornsTable"/>). The Mage's cloaks. No caster stat. Appended.
        /// </summary>
        Thorns = 14
    }
}
