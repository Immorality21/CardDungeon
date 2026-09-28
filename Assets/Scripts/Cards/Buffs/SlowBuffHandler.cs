using Assets.Scripts.Combat;
using Assets.Scripts.Items;
using Assets.Scripts.UnitStats;

namespace Assets.Scripts.Cards.Buffs
{
    public class SlowBuffHandler : IBuffHandler
    {
        public void Apply(ICombatUnit target, int power, int duration, CombatBuffTracker buffTracker)
        {
            // The power arrives signed (DebuffEffectExecutor negates it) and is used as a magnitude:
            // Slow always lowers Agility. Negating the signed value, as this did, turned a Slow cast as
            // a Debuff into an Agility *buff* - nothing authored Slow until Quake, which is why it hid.
            buffTracker.ApplyBuff(target, StatType.Agility, -System.Math.Abs(power), duration);
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
