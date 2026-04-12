using Assets.Scripts.Combat;
using Assets.Scripts.Items;

namespace Assets.Scripts.Cards.Buffs
{
    public class HasteBuffHandler : IBuffHandler
    {
        public void Apply(ICombatUnit target, int power, int duration, CombatBuffTracker buffTracker)
        {
            buffTracker.ApplyBuff(target, StatType.Agility, power, duration);
            buffTracker.ApplyStatusEffect(target, BuffType.Haste, duration);
        }

        public string GetDisplayText(int power)
        {
            return "Haste!";
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
