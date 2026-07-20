using Assets.Scripts.Combat;

namespace Assets.Scripts.Enemies.Behaviors
{
    /// <summary>Plain attacker — hits a random living hero every turn.</summary>
    public class AggressorBehavior : IEnemyBehavior
    {
        public EnemyDecision Decide(ICombatUnit self, EnemyCombatContext context)
        {
            return new EnemyDecision
            {
                Type = EnemyActionType.Attack,
                Target = EnemyTargeting.PickRandom(context.Heroes)
            };
        }
    }
}
