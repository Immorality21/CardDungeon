using Assets.Scripts.Combat;

namespace Assets.Scripts.Cards.Buffs
{
    public class FrozenBuffHandler : IBuffHandler
    {
        public void Apply(ICombatUnit target, int power, int duration, CombatBuffTracker buffTracker)
        {
            buffTracker.ApplyStatusEffect(target, BuffType.Frozen, duration);
        }

        public string GetDisplayText(int power)
        {
            return "Frozen!";
        }

        public bool SkipsTurn => true;

        public string GetSkipTurnMessage(ICombatUnit unit)
        {
            return $"{unit.DisplayName} is frozen!";
        }

        public bool IsRemovedByDamageType(DamageType damageType)
        {
            return damageType == DamageType.Fire;
        }
    }
}
