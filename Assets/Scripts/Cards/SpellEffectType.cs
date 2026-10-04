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
        RestoreCharge = 7
    }
}
