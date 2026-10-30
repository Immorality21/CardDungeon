using Assets.Scripts.Combat;
using ImmoralityGaming.Localization;

namespace Assets.Scripts.Cards.Buffs
{
    /// <summary>
    /// <see cref="BuffType.DivineProtection"/>: the target's next <c>power</c> damaging hits (at least
    /// one) - a blow or an ability, any element - are negated outright, for <c>duration</c> of the
    /// target's turns (<see cref="ProtectionTable"/>, on the fight's event stream, spent in
    /// <see cref="HealthOps.Damage"/>). Ticks and prices never spend it. A buff on the bearer's own
    /// side, so not curable. With no fight stream - a test, a room event - it does nothing.
    /// </summary>
    public class DivineProtectionBuffHandler : IBuffHandler
    {
        public void Apply(ICombatUnit target, int power, int duration, CombatBuffTracker buffTracker, ICombatUnit source = null)
        {
            if (target == null || buffTracker == null || buffTracker.Events == null)
            {
                return;
            }
            buffTracker.Events.Protections.Add(target, power > 1 ? power : 1, duration);
        }

        public string GetDisplayText(int power)
        {
            return power > 1
                ? Loc.Format("combat.status.divine-protection-hits", ("hits", power))
                : Loc.Get("combat.status.divine-protection");
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
