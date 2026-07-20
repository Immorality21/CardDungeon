namespace Assets.Scripts.Enemies.Behaviors
{
    /// <summary>Maps an <see cref="EnemyArchetype"/> to its stateless behavior instance.</summary>
    public static class EnemyBehaviorFactory
    {
        private static readonly AggressorBehavior _aggressor = new AggressorBehavior();
        private static readonly BruiserBehavior _bruiser = new BruiserBehavior();
        private static readonly HealerBehavior _healer = new HealerBehavior();
        private static readonly DebufferBehavior _debuffer = new DebufferBehavior();

        public static IEnemyBehavior Get(EnemyArchetype archetype)
        {
            switch (archetype)
            {
                case EnemyArchetype.Bruiser:
                    return _bruiser;
                case EnemyArchetype.Healer:
                    return _healer;
                case EnemyArchetype.Debuffer:
                    return _debuffer;
                default:
                    return _aggressor;
            }
        }
    }
}
