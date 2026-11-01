using Assets.Scripts.Combat;
using Assets.Scripts.Items;
using Assets.Scripts.UnitStats;
using ImmoralityGaming.Localization;

namespace Assets.Scripts.Cards.Buffs
{
    public class StatBuffHandler : IBuffHandler
    {
        private readonly StatType _stat;

        /// <summary>The stat this handler changes — read by percentage buffs to size themselves.</summary>
        public StatType Stat => _stat;

        public StatBuffHandler(StatType stat)
        {
            _stat = stat;
        }

        public void Apply(ICombatUnit target, int power, int duration, CombatBuffTracker buffTracker, ICombatUnit source = null)
        {
            buffTracker.ApplyBuff(target, _stat, power, duration);
        }

        /// <summary>A percentage of each target's own stat: "+20% Strength".</summary>
        public string GetPercentText(int percent)
        {
            return percent >= 0
                ? Loc.Format("combat.status.stat-up-percent", ("power", percent), ("stat", StatCatalog.DisplayName(_stat)))
                : Loc.Format("combat.status.stat-down-percent", ("power", percent), ("stat", StatCatalog.DisplayName(_stat)));
        }

        public string GetDisplayText(int power)
        {
            if (power >= 0)
            {
                return Loc.Format("combat.status.stat-up", ("power", power), ("stat", StatCatalog.DisplayName(_stat)));
            }

            return Loc.Format("combat.status.stat-down", ("power", power), ("stat", StatCatalog.DisplayName(_stat)));
        }

        public bool SkipsTurn => false;

        public bool IsRemovedByDamageType(DamageType damageType)
        {
            return false;
        }
    }
}
