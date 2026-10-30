using Assets.Scripts.Combat;
using ImmoralityGaming.Localization;

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
        private readonly string _nameKey;
        private readonly bool _allTypes;

        public ResistanceBuffHandler(DamageType damageType, string nameKey)
        {
            _damageType = damageType;
            _nameKey = nameKey;
        }

        private ResistanceBuffHandler(string nameKey)
        {
            _nameKey = nameKey;
            _allTypes = true;
        }

        /// <summary>A resistance to every element, Normal included - a flat cut to every hit
        /// (<see cref="BuffType.Guarded"/>, the Warrior's Take Cover).</summary>
        public static ResistanceBuffHandler ToAll(string nameKey)
        {
            return new ResistanceBuffHandler(nameKey);
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
                return power >= 0
                    ? Loc.Format("combat.status.guard-less-damage", ("power", power))
                    : Loc.Format("combat.status.guard-more-damage", ("power", -power));
            }
            if (power >= 0)
            {
                return Loc.Format("combat.status.resistance-up", ("power", power), ("name", Loc.Get(_nameKey)));
            }

            return Loc.Format("combat.status.resistance-down", ("power", power), ("name", Loc.Get(_nameKey)));
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
