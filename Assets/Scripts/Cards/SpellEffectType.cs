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
        HealthCost = 4
    }
}
