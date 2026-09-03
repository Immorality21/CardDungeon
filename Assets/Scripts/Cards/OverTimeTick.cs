namespace Assets.Scripts.Cards
{
    /// <summary>
    /// One resolved over-time tick: which effect fired, how much health it actually moved, and how
    /// to label it.
    ///
    /// <para>Returned by <see cref="CombatBuffTracker.ResolveOverTime"/> <b>already applied</b>, so
    /// the caller's only remaining jobs are presentation and — for damage — noticing that the unit
    /// may now be dead.</para>
    /// </summary>
    public class OverTimeTick
    {
        public BuffType BuffType;

        /// <summary>
        /// Health actually moved, always positive; <see cref="Heals"/> says which direction. Zero
        /// entries are not returned at all, so a tick fully absorbed by resistance produces nothing
        /// to show rather than a "0".
        /// </summary>
        public int Amount;

        /// <summary>True when the tick restored health — a regeneration, or an absorbed element.</summary>
        public bool Heals;

        /// <summary>Short label for the floating number, e.g. "Burn" or "Regen".</summary>
        public string Label;
    }
}
