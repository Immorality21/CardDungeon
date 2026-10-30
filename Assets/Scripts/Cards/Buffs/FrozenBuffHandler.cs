using Assets.Scripts.Combat;
using ImmoralityGaming.Localization;

namespace Assets.Scripts.Cards.Buffs
{
    public class FrozenBuffHandler : IBuffHandler
    {
        public void Apply(ICombatUnit target, int power, int duration, CombatBuffTracker buffTracker, ICombatUnit source = null)
        {
            buffTracker.ApplyStatusEffect(target, BuffType.Frozen, duration);
        }

        public string GetDisplayText(int power)
        {
            return Loc.Get("combat.status.frozen");
        }

        public bool SkipsTurn => true;

        public string GetSkipTurnMessage(ICombatUnit unit)
        {
            return Loc.Format("combat.skip.frozen", ("unit", unit.DisplayName));
        }

        public bool IsRemovedByDamageType(DamageType damageType)
        {
            return damageType == DamageType.Fire;
        }
    }
}
