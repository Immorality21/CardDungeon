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
        public int SelfTurnCount;               // turns this enemy has already taken (cadence conditions)

        /// <summary>
        /// Index into the behaviour's <c>Actions</c> of the telegraphed action currently in flight, or
        /// <see cref="EnemyActionPlanner.NoCharge"/>. This replaced a bare "is charging" bool: with
        /// telegraphs authored per action rather than fixed per archetype, knowing *that* an enemy is
        /// winding up is no longer enough to know what it is about to deliver.
        /// </summary>
        public int ChargingEntryIndex = EnemyActionPlanner.NoCharge;

        /// <summary>What this enemy can cast (its own spell list) — see EnemyMagicPlan.</summary>
        public List<EnemySpellEntry> Spells;

        /// <summary>True while a telegraphed action is in flight.</summary>
        public bool SelfIsCharging => ChargingEntryIndex >= 0;
    }
}
