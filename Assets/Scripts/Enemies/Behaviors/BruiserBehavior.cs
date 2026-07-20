using Assets.Scripts.Combat;

namespace Assets.Scripts.Enemies.Behaviors
{
    /// <summary>
    /// Spends one turn charging (telegraphed), then delivers a heavy hit the next turn.
    /// The charge is the player's window to heal, defend, or kill it first.
    /// </summary>
    public class BruiserBehavior : IEnemyBehavior
    {
        public const float HeavyMultiplier = 2.5f;

        public EnemyDecision Decide(ICombatUnit self, EnemyCombatContext context)
        {
            if (context.SelfIsCharging)
            {
                // Target is resolved by the combat loop from the stored charge target.
                return new EnemyDecision
                {
                    Type = EnemyActionType.HeavyAttack,
                    Multiplier = HeavyMultiplier
                };
            }

            return new EnemyDecision
            {
                Type = EnemyActionType.ChargeHeavy,
                Target = EnemyTargeting.PickRandom(context.Heroes)
            };
        }
    }
}
