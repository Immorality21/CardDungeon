using Assets.Scripts.Combat;
using Assets.Scripts.Items;

namespace Assets.Scripts.Cards.Buffs
{
    public class StatBuffHandler : IBuffHandler
    {
        private readonly StatType _stat;
        private readonly string _displayName;

        public StatBuffHandler(StatType stat, string displayName)
        {
            _stat = stat;
            _displayName = displayName;
        }

        public void Apply(ICombatUnit target, int power, int duration, CombatBuffTracker buffTracker)
        {
            buffTracker.ApplyBuff(target, _stat, power, duration);
        }

        public string GetDisplayText(int power)
        {
            if (power >= 0)
            {
                return $"+{power} {_displayName}";
            }

            return $"{power} {_displayName}";
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
