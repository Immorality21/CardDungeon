using Assets.Scripts.Combat;
using Assets.Scripts.Items;

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
        AoeAttack    // boss: deliver the telegraphed signature across all living heroes
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
    }
}
