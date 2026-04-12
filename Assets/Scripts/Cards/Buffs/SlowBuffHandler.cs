using Assets.Scripts.Combat;
using Assets.Scripts.Items;

namespace Assets.Scripts.Cards.Buffs
{
    public class SlowBuffHandler : IBuffHandler
    {
        public void Apply(ICombatUnit target, int power, int duration, CombatBuffTracker buffTracker)
        {
            buffTracker.ApplyBuff(target, StatType.Agility, -power, duration);
            buffTracker.ApplyStatusEffect(target, BuffType.Slow, duration);
        }

        public string GetDisplayText(int power)
        {
            return "Slow!";
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
