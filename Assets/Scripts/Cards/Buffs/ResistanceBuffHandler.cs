using Assets.Scripts.Combat;

namespace Assets.Scripts.Cards.Buffs
{
    /// <summary>
    /// Grants a timed elemental resistance — the defensive half of the elemental layer. Was a no-op
    /// for as long as the five resistance <see cref="BuffType"/>s existed, which made every
    /// resistance buff silently inert: the popup said "+40 FireResistance" and nothing changed.
    ///
    /// <para><c>Power</c> is a percentage, so it stacks with innate and gear resistance and can push
    /// a deliberately assembled build past 100% into absorption.</para>
    /// </summary>
    public class ResistanceBuffHandler : IBuffHandler
    {
        private readonly DamageType _damageType;
        private readonly string _displayName;
        private readonly bool _allTypes;

        public ResistanceBuffHandler(DamageType damageType, string displayName)
        {
            _damageType = damageType;
            _displayName = displayName;
        }

        private ResistanceBuffHandler(string displayName)
        {
            _displayName = displayName;
            _allTypes = true;
        }

        /// <summary>A resistance to every element, Normal included - a flat cut to every hit
        /// (<see cref="BuffType.Guarded"/>, the Warrior's Take Cover).</summary>
        public static ResistanceBuffHandler ToAll(string displayName)
        {
            return new ResistanceBuffHandler(displayName);
        }

        public void Apply(ICombatUnit target, int power, int duration, CombatBuffTracker buffTracker, ICombatUnit source = null)
        {
            if (target == null || buffTracker == null || power == 0)
            {
                return;
            }

            if (_allTypes)
            {
                buffTracker.ApplyResistanceToAll(target, power, duration);
                return;
            }
            buffTracker.ApplyResistance(target, _damageType, power, duration);
        }

        public string GetDisplayText(int power)
        {
            if (_allTypes)
            {
                return power >= 0 ? $"{power}% less damage" : $"{-power}% more damage";
            }
            if (power >= 0)
            {
                return $"+{power}% {_displayName}";
            }

            return $"{power}% {_displayName}";
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
