using System.Collections.Generic;
using Assets.Scripts.Combat;

namespace Assets.Scripts.Enemies.Behaviors
{
    /// <summary>
    /// Heals the most-wounded ally (itself included) when one is hurt; otherwise attacks.
    /// A high-value target the player should prioritise.
    /// </summary>
    public class HealerBehavior : IEnemyBehavior
    {
        public const int HealPower = 8;

        public EnemyDecision Decide(ICombatUnit self, EnemyCombatContext context)
        {
            var candidates = new List<ICombatUnit>();
            if (context.Allies != null)
            {
                candidates.AddRange(context.Allies);
            }
            candidates.Add(self);

            var wounded = EnemyTargeting.MostWounded(candidates);
            if (wounded != null)
            {
                return new EnemyDecision
                {
                    Type = EnemyActionType.Heal,
                    Target = wounded,
                    Amount = HealPower
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
