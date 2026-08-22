namespace Assets.Scripts.Heroes
{
    /// <summary>
    /// How wide the fielded party is allowed to be. Pure math and constants, so the balance model and
    /// the tests can reason about party width without a live <c>MetaProgressManager</c>.
    ///
    /// <para>The cap is <b>earned, not given</b>: a save starts able to field <see cref="BaseCap"/>
    /// heroes and buys its way to <see cref="MaxCap"/> with Gold. Party width is itself a progression
    /// axis - and it needs a price, because with even-split XP going wide is a real trade (safety and
    /// faster clears against a quarter of the XP per hero) that the player should have to commit to
    /// rather than drift into by recruiting.</para>
    /// </summary>
    public static class PartySlots
    {
        /// <summary>Heroes a fresh save can field at once.</summary>
        public const int BaseCap = 2;

        /// <summary>The hard ceiling - combat fan-out and the turn order are authored around four.</summary>
        public const int MaxCap = 4;

        /// <summary>Slots buyable on top of <see cref="BaseCap"/>.</summary>
        public const int MaxBonus = MaxCap - BaseCap;

        private const int BaseCost = 300;
        private const int CostIncrement = 300;

        /// <summary>The cap a save with <paramref name="bonus"/> purchased slots can field.</summary>
        public static int CapForBonus(int bonus)
        {
            if (bonus < 0)
            {
                bonus = 0;
            }
            if (bonus > MaxBonus)
            {
                bonus = MaxBonus;
            }
            return BaseCap + bonus;
        }

        /// <summary>
        /// Gold cost of the next slot (from <paramref name="currentBonus"/> to +1), or 0 when the cap
        /// is already at <see cref="MaxCap"/>. Priced against a hero recruitment rather than a
        /// restock: the slot is permanent, and it is what makes the recruitment worth making.
        /// </summary>
        public static int CostForNext(int currentBonus)
        {
            if (currentBonus < 0)
            {
                currentBonus = 0;
            }
            if (currentBonus >= MaxBonus)
            {
                return 0;
            }
            return BaseCost + (currentBonus * CostIncrement);
        }
    }
}
