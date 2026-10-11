using Assets.Scripts.Combat;
using UnityEngine;

namespace Assets.Scripts.Cards.Buffs
{
    /// <summary>
    /// The Warlock's Leeching Curse. A leeched unit gives a share of every hit the curser's side lands on
    /// it back to the curser as health - worked by <see cref="LeechTracker"/>, off the fight's
    /// <c>HealthChange</c>s. The record is an over-time one (<see cref="CombatBuffTracker.ApplyOverTime"/>)
    /// only because that is the record that carries an amount (the percent) and a source (the curser);
    /// this handler is not an <see cref="IOverTimeBuffHandler"/>, so it never ticks.
    /// </summary>
    public class LeechedBuffHandler : IBuffHandler
    {
        public void Apply(ICombatUnit target, int power, int duration, CombatBuffTracker buffTracker, ICombatUnit source = null)
        {
            if (target == null || buffTracker == null || duration <= 0)
            {
                return;
            }
            // A debuff arrives negated (DebuffEffectExecutor); the percent is its magnitude.
            buffTracker.ApplyOverTime(target, BuffType.Leeched, Mathf.Abs(power), duration, source);
        }

        public string GetDisplayText(int power)
        {
            return $"Leeched {Mathf.Abs(power)}%!";
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
