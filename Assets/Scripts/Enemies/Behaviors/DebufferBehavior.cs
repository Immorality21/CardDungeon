using Assets.Scripts.Combat;
using Assets.Scripts.Items;

namespace Assets.Scripts.Enemies.Behaviors
{
    /// <summary>
    /// Weakens a hero's Attack (if not already weakened); otherwise attacks. Creates
    /// pressure to spend turns/cards cleansing or to end the fight quickly.
    /// </summary>
    public class DebufferBehavior : IEnemyBehavior
    {
        public const int DebuffAmount = 3;
        public const int DebuffDuration = 3;

        public EnemyDecision Decide(ICombatUnit self, EnemyCombatContext context)
        {
            var target = EnemyTargeting.FirstWithoutDebuff(context.Heroes, context.BuffTracker, StatType.Strength);
            if (target != null)
            {
                return new EnemyDecision
                {
                    Type = EnemyActionType.Debuff,
                    Target = target,
                    Amount = DebuffAmount,
                    Duration = DebuffDuration,
                    DebuffStat = StatType.Strength
                };
            }

            return new EnemyDecision
            {
                Type = EnemyActionType.Attack,
                Target = EnemyTargeting.PickRandom(context.Heroes)
            };
        }
    }
}
