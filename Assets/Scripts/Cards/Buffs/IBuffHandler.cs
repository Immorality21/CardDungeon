using Assets.Scripts.Combat;

namespace Assets.Scripts.Cards.Buffs
{
    public interface IBuffHandler
    {
        void Apply(ICombatUnit target, int power, int duration, CombatBuffTracker buffTracker);
        string GetDisplayText(int power);
        bool SkipsTurn { get; }
        string GetSkipTurnMessage(ICombatUnit unit);
        bool IsRemovedByDamageType(DamageType damageType);
    }
}
