using System.Collections.Generic;
using Assets.Scripts.Cards;
using Assets.Scripts.Combat;

namespace Assets.Scripts.Enemies.Behaviors
{
    /// <summary>Combat state a behavior needs to decide its action.</summary>
    public class EnemyCombatContext
    {
        public List<ICombatUnit> Heroes;        // living heroes (attack/debuff targets)
        public List<ICombatUnit> Allies;        // living enemy allies, excluding self
        public CombatBuffTracker BuffTracker;
        public bool SelfIsCharging;             // whether this enemy is mid-charge
    }
}
