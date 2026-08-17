using Assets.Scripts.Combat;
using UnityEngine;

namespace Assets.Scripts.Enemies.Behaviors
{
    /// <summary>
    /// The run's climax fight. A boss cycles basic attacks with a telegraphed <b>signature</b>
    /// move (a party-wide AoE, charged one turn ahead like the Bruiser's heavy so the player can
    /// react), and grows more dangerous when <b>enraged</b> below a health threshold: harder basic
    /// hits and a tighter signature cadence. Pure decider — the combat loop executes and animates.
    /// </summary>
    public class BossBehavior : IEnemyBehavior
    {
        // Health fraction at or below which the boss enrages.
        public const float EnrageThreshold = 0.30f;

        // How often (in turns taken) the boss winds up its signature AoE. Enrage tightens it.
        public const int SignatureInterval = 3;
        public const int EnragedSignatureInterval = 2;

        // Damage multipliers. Signature is per-target across the whole party, so it is kept
        // lower than the Bruiser's single-target heavy; enrage boosts ordinary blows.
        public const float SignatureMultiplier = 1.6f;
        public const float EnrageAttackMultiplier = 1.5f;

        public EnemyDecision Decide(ICombatUnit self, EnemyCombatContext context)
        {
            // Deliver the signature that was telegraphed on the previous turn.
            if (context.SelfIsCharging)
            {
                return new EnemyDecision
                {
                    Type = EnemyActionType.AoeAttack,
                    Multiplier = SignatureMultiplier
                };
            }

            bool enraged = IsEnraged(self);

            // On its cadence, wind up the party-wide signature (telegraphed). The first turn
            // (SelfTurnCount == 0) is always a plain attack so the fight opens readably.
            int interval = enraged ? EnragedSignatureInterval : SignatureInterval;
            if (context.SelfTurnCount > 0 && context.SelfTurnCount % interval == 0)
            {
                return new EnemyDecision { Type = EnemyActionType.ChargeAoe };
            }

            // Otherwise a basic attack — harder while enraged.
            return new EnemyDecision
            {
                Type = EnemyActionType.Attack,
                Target = EnemyTargeting.PickRandom(context.Heroes),
                Multiplier = enraged ? EnrageAttackMultiplier : 1f
            };
        }

        private static bool IsEnraged(ICombatUnit self)
        {
            if (self == null || self.Stats == null || self.Stats.MaxHealth <= 0)
            {
                return false;
            }
            float ratio = (float)self.Stats.Health / self.Stats.MaxHealth;
            return ratio <= EnrageThreshold;
        }
    }
}
