namespace Assets.Scripts.Enemies
{
    /// <summary>
    /// Coarse label for a kind of enemy. It no longer selects any logic — an
    /// <c>EnemyBehaviorSO</c>'s authored action list is the behaviour — but it names the presets, the
    /// analyzer's variety checks report on its spread across the roster, and it is the fallback for an
    /// enemy with no behaviour assigned.
    /// </summary>
    public enum EnemyArchetype
    {
        Aggressor,
        Bruiser,
        Healer,
        Debuffer,
        Boss
    }
}
