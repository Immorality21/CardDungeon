using Assets.Scripts.Combat;

namespace Assets.Scripts.Cards.Buffs
{
    public class ResistanceBuffHandler : IBuffHandler
    {
        private readonly string _displayName;

        public ResistanceBuffHandler(string displayName)
        {
            _displayName = displayName;
        }

        public void Apply(ICombatUnit target, int power, int duration, CombatBuffTracker buffTracker)
        {
            // Resistance buffs are not yet implemented
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
