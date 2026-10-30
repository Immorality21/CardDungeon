using Assets.Scripts.Combat;
using UnityEngine;
using ImmoralityGaming.Localization;

namespace Assets.Scripts.Cards.Buffs
{
    /// <summary>
    /// The Rogue's Vanish. A hidden unit cannot be picked as an enemy's target: the fight leaves it out
    /// of the heroes it offers the enemy planner (<c>CombatManager.TargetableHeroes</c>), unless every
    /// hero is hidden. A status on the bearer's own side, so it is not curable.
    /// </summary>
    public class HiddenBuffHandler : IBuffHandler
    {
        public static bool IsHidden(CombatBuffTracker buffTracker, ICombatUnit unit)
        {
            return buffTracker != null && unit != null && buffTracker.HasStatusEffect(unit, BuffType.Hidden);
        }

        public void Apply(ICombatUnit target, int power, int duration, CombatBuffTracker buffTracker, ICombatUnit source = null)
        {
            if (target == null || buffTracker == null || duration <= 0)
            {
                return;
            }
            buffTracker.ApplyStatusEffect(target, BuffType.Hidden, duration);
        }

        public string GetDisplayText(int power)
        {
            return Loc.Get("combat.status.vanished");
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

    /// <summary>
    /// The Rogue's Shadowstep and Smoke and Mirrors. The next <c>Amount</c> physical blows aimed at the
    /// unit miss outright, before its Luck dodge is rolled (<see cref="TryEvade"/>, read by
    /// <c>CombatManager.ExecuteAttack</c> and <c>EffectResolver</c>'s hit filter). Stored through
    /// <see cref="CombatBuffTracker.ApplyOverTime"/> because that record carries the count; it never ticks.
    /// </summary>
    public class EvasiveBuffHandler : IBuffHandler
    {
        /// <summary>Spends one evasion on <paramref name="target"/> if it has any: true when the blow misses.</summary>
        public static bool TryEvade(CombatBuffTracker buffTracker, ICombatUnit target)
        {
            var evasive = buffTracker?.GetStatus(target, BuffType.Evasive);
            if (evasive == null || evasive.Amount <= 0)
            {
                return false;
            }
            evasive.Amount--;
            if (evasive.Amount <= 0)
            {
                buffTracker.RemoveStatusEffect(target, BuffType.Evasive);
            }
            return true;
        }

        public void Apply(ICombatUnit target, int power, int duration, CombatBuffTracker buffTracker, ICombatUnit source = null)
        {
            if (target == null || buffTracker == null || duration <= 0)
            {
                return;
            }
            buffTracker.ApplyOverTime(target, BuffType.Evasive, Mathf.Max(1, Mathf.Abs(power)), duration, source);
        }

        public string GetDisplayText(int power)
        {
            return Loc.Get("combat.status.evasive");
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

    /// <summary>
    /// The Rogue's Death Mark. Every damaging hit on a marked unit is a critical one - abilities that
    /// cannot crit included - at <see cref="CritRules.MarkedMultiplier"/>. Read where crits are rolled:
    /// <c>EffectResolver.RollCrits</c>, <c>DamageEffectExecutor</c> and <c>CombatManager.ExecuteAttack</c>.
    /// </summary>
    public class DeathMarkedBuffHandler : IBuffHandler
    {
        public static bool IsMarked(CombatBuffTracker buffTracker, ICombatUnit unit)
        {
            return buffTracker != null && unit != null && buffTracker.HasStatusEffect(unit, BuffType.DeathMarked);
        }

        public void Apply(ICombatUnit target, int power, int duration, CombatBuffTracker buffTracker, ICombatUnit source = null)
        {
            if (target == null || buffTracker == null || duration <= 0)
            {
                return;
            }
            buffTracker.ApplyStatusEffect(target, BuffType.DeathMarked, duration);
        }

        public string GetDisplayText(int power)
        {
            return Loc.Get("combat.status.death-mark");
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
