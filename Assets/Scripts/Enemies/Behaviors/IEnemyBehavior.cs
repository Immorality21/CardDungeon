using Assets.Scripts.Combat;

namespace Assets.Scripts.Enemies.Behaviors
{
    public interface IEnemyBehavior
    {
        EnemyDecision Decide(ICombatUnit self, EnemyCombatContext context);
    }
}
