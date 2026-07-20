namespace Assets.Scripts.Enemies
{
    /// <summary>
    /// Behavior archetype for an enemy. Selected per <see cref="EnemySpawnEntry"/>;
    /// drives which <c>IEnemyBehavior</c> the combat loop uses on the enemy's turn.
    /// </summary>
    public enum EnemyArchetype
    {
        Aggressor,
        Bruiser,
        Healer,
        Debuffer
    }
}
