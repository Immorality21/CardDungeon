using System.Collections.Generic;
using Assets.Scripts.Combat;
using Assets.Scripts.Items;
using Assets.Scripts.UnitStats;

namespace Assets.Scripts.Enemies.Behaviors
{
    public enum EnemyActionType
    {
        Attack,
        ChargeHeavy,
        HeavyAttack,
        Heal,
        Debuff,
        ChargeAoe,   // boss: telegraph a signature move that hits the whole party next turn
        AoeAttack,   // boss: deliver the telegraphed signature across all living heroes
        CastMagic    // cast one of the enemy's Spells (see EnemyMagicPlan)
    }

    /// <summary>
    /// The action an enemy behavior chooses for a turn. Behaviors decide; the combat
    /// loop (CombatManager) executes, so all animation/logging stays in one place.
    /// </summary>
    public class EnemyDecision
    {
        public EnemyActionType Type;
        public ICombatUnit Target;
        public float Multiplier = 1f;         // damage multiplier for HeavyAttack
        public int Amount;                    // heal amount / debuff magnitude
        public int Duration;                  // debuff duration in turns
        public StatType DebuffStat = StatType.Strength;

        /// <summary>The magic to cast, for <see cref="EnemyActionType.CastMagic"/>.</summary>
        public Assets.Scripts.Cards.MagicSO Magic;

        /// <summary>
        /// Every unit a <see cref="EnemyActionType.CastMagic"/> lands on, resolved from the magic's
        /// <c>TargetType</c> by <see cref="EnemyMagicPlan.ResolveTargets"/>. <see cref="Target"/>
        /// stays the single-target field the other actions use.
        /// </summary>
        public List<ICombatUnit> MagicTargets;

        /// <summary>
        /// Index of the authored action this decision came from, or -1. The combat loop stores it on
        /// the enemy when a telegraph starts, so the follow-up turn knows which payload to deliver.
        /// </summary>
        public int EntryIndex = -1;
    }
}
