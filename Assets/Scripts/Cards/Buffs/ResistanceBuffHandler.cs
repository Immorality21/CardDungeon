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

        public ResistanceBuffHandler(DamageType damageType, string displayName)
        {
            _damageType = damageType;
            _displayName = displayName;
        }

        public void Apply(ICombatUnit target, int power, int duration, CombatBuffTracker buffTracker)
        {
            if (target == null || buffTracker == null || power == 0)
            {
                return;
            }

            buffTracker.ApplyResistance(target, _damageType, power, duration);
        }

        public string GetDisplayText(int power)
        {
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
