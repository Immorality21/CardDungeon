namespace Assets.Scripts.Heroes
{
    /// <summary>
    /// How wide the fielded party is allowed to be. One constant, because party width is no longer
    /// something a save buys.
    ///
    /// <para><b>The slot purchase was removed on 2026-09-17, and this is why.</b> Section 5b decided
    /// that <i>gold never buys a hero again — a hero is access, gated by the campaign</i>, and the
    /// tavern was deleted for it. Buying the right to field one more hero was the same trade wearing
    /// a different hat: the last surviving piece of the tavern. What paces party width now is the
    /// roster — heroes are rescued and unlocked, never purchased — so the cap is simply the widest
    /// lineup combat is authored around.</para>
    ///
    /// <para><b>Width is still a decision, and it always was.</b> The toll was never what made it
    /// one: <see cref="XpSplit"/> divides a kill evenly, so a solo hero levels four times as fast as
    /// one of four. Going wide buys safety and faster clears, going narrow buys depth, and that is
    /// paid every single run rather than once at a shop. The gold price was a second charge on a
    /// choice already priced, which is exactly why it read as a toll rather than as a decision.</para>
    ///
    /// <para>Kept as a class rather than folded into a literal so the balance model, the roster and
    /// the party screen all name the same ceiling.</para>
    /// </summary>
    public static class PartySlots
    {
        /// <summary>
        /// The widest party the game can field. A hard ceiling: combat fan-out, the turn order and
        /// <c>MaxBodiesPerRoom</c> are all authored around four.
        /// </summary>
        public const int MaxCap = 4;

        /// <summary>
        /// What a fresh save can field — the same number. Kept as its own name because the balance
        /// model and the frontier read "the width you start with" and "the width you can reach" in
        /// different sentences, and they should stay distinguishable if that ever splits again.
        /// </summary>
        public const int BaseCap = MaxCap;

        /// <summary>
        /// The width a fresh save actually <i>fields</i>: one, the solo start (section 5b).
        ///
        /// <para>This is what the investment frontier charges width from, and it is a different
        /// question from <see cref="MaxCap"/>. The cap says what the game permits; this says what
        /// the player has before they have gone and earned anything. A second body is a rescue, a
        /// campaign gate and a run's worth of play - real investment - and pricing from the cap
        /// would hand the model three heroes nobody paid for.</para>
        ///
        /// <para>It was 2 while the cap was bought, which was already stale: 5b restored the solo
        /// start, so a fresh save had been fielding one hero and being modelled as fielding two.</para>
        /// </summary>
        public const int FreeWidth = 1;
    }
}
