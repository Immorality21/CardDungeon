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
        TurnDelay = 5
    }
}
