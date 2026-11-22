using Assets.Scripts.Combat;
using ImmoralityGaming.Localization;

namespace Assets.Scripts.Cards.Buffs
{
    /// <summary>
    /// <see cref="BuffType.Stunned"/>: the unit's turns are skipped while it lasts, as Frozen's are, but
    /// no element breaks it - roots do not thaw. The Ent's Root.
    /// </summary>
    public class StunnedBuffHandler : IBuffHandler
    {
        public void Apply(ICombatUnit target, int power, int duration, CombatBuffTracker buffTracker, ICombatUnit source = null)
        {
            buffTracker.ApplyStatusEffect(target, BuffType.Stunned, duration);
        }

        public string GetDisplayText(int power)
        {
            return Loc.Get("combat.status.stunned");
        }

        public bool SkipsTurn => true;

        public bool IsRemovedByDamageType(DamageType damageType)
        {
            return false;
        }
    }
}
