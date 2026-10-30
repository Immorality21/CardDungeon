using Assets.Scripts.Combat;
using ImmoralityGaming.Localization;

namespace Assets.Scripts.Cards.Buffs
{
    /// <summary>
    /// <see cref="BuffType.Barrier"/>: puts up a barrier of <c>power</c> points that soaks damage before
    /// the bar is touched, for <c>duration</c> of the target's turns (<see cref="BarrierTable"/>, on the
    /// fight's event stream). Authored with <c>PowerMode.PercentOfMaxHealth</c> it is that share of each
    /// target's own bar (<c>BuffEffectExecutor</c> resolves it). With no fight stream - a test, a room
    /// event - there is nothing to soak with and it does nothing.
    /// </summary>
    public class BarrierBuffHandler : IBuffHandler
    {
        public void Apply(ICombatUnit target, int power, int duration, CombatBuffTracker buffTracker, ICombatUnit source = null)
        {
            if (target == null || buffTracker == null || buffTracker.Events == null || power <= 0)
            {
                return;
            }
            buffTracker.Events.Barriers.Add(target, power, duration);
        }

        public string GetDisplayText(int power)
        {
            return Loc.Format("combat.status.barrier", ("power", power));
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
