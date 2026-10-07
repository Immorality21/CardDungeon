using Assets.Scripts.Combat;

namespace Assets.Scripts.Cards.Buffs
{
    public interface IBuffHandler
    {
        /// <param name="source">Who applied it - the caster, or a reaction's bearer. An over-time
        /// effect credits its ticks (and a kill) to it; null falls back to the unit whose turn is open.</param>
        void Apply(ICombatUnit target, int power, int duration, CombatBuffTracker buffTracker, ICombatUnit source = null);
        string GetDisplayText(int power);
        bool SkipsTurn { get; }
        string GetSkipTurnMessage(ICombatUnit unit);
        bool IsRemovedByDamageType(DamageType damageType);
    }
}
