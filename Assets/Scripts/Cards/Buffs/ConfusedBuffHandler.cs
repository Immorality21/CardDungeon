using Assets.Scripts.Combat;

namespace Assets.Scripts.Cards.Buffs
{
    /// <summary>
    /// The Cultist's Mass Hysteria. On a confused enemy's next turn it rolls <see cref="Chance"/>: on a
    /// hit it swings at one of its own allies instead of acting, and either way the confusion is spent
    /// with that turn. An enemy standing alone has nobody to turn on and acts normally.
    ///
    /// <para>Read in <c>CombatManager.ExecuteEnemyTurn</c> only. The balance model does not simulate
    /// it - Ultras are not in the simulator - so it can only make a fight easier than measured.</para>
    /// </summary>
    public class ConfusedBuffHandler : IBuffHandler
    {
        /// <summary>The share of a confused unit's turns spent hitting its own side.</summary>
        public const float Chance = 0.5f;

        /// <summary>Whether <paramref name="unit"/> is confused - null-safe for callers with no fight.</summary>
        public static bool IsConfused(CombatBuffTracker buffTracker, ICombatUnit unit)
        {
            return buffTracker != null && unit != null && buffTracker.HasStatusEffect(unit, BuffType.Confused);
        }

        public void Apply(ICombatUnit target, int power, int duration, CombatBuffTracker buffTracker, ICombatUnit source = null)
        {
            if (target == null || buffTracker == null || duration <= 0)
            {
                return;
            }

            buffTracker.ApplyStatusEffect(target, BuffType.Confused, duration);
        }

        public string GetDisplayText(int power)
        {
            return "Confused!";
        }

        public bool SkipsTurn => false;

        public string GetSkipTurnMessage(ICombatUnit unit)
        {
            return null;
        }

        public bool IsRemovedByDamageType(DamageType damageType)
        {
            return false;
        }
    }
}
