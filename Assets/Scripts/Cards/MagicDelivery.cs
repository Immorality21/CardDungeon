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
    }
}
