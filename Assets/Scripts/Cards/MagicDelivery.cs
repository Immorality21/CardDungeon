namespace Assets.Scripts.Cards
{
    /// <summary>
    /// How a damaging ability's hit is shown reaching its target (<see cref="MagicSO.Delivery"/>).
    /// Presentation only: the resolver, the simulator and the balance model never read it.
    /// </summary>
    public enum MagicDelivery
    {
        /// <summary>The icon flies from the caster to the target - a spell, an arrow, a dart. The
        /// default, so every ability authored before this existed keeps its bolt.</summary>
        Projectile = 0,

        /// <summary>The icon lands on the target where it stands - a blade, a cleave, a bolt from
        /// the sky. Nothing crosses the stage.</summary>
        Strike = 1,

        /// <summary>A straight beam from the caster that reaches the target at once - a laser
        /// (owner, 2026-11-02: the Floating Eye's). Coloured by the hit's element.</summary>
        Beam = 2,

        /// <summary>The caster lets it out of itself - it swells and rings race out - and then every
        /// target is hit at once, with nothing crossing the stage (the Floating Eye's Screech).</summary>
        Burst = 3,
    }
}
