using Assets.Scripts.Combat;
using UnityEngine;
using ImmoralityGaming.Localization;

namespace Assets.Scripts.Cards.Buffs
{
    /// <summary>
    /// The Ranger's Hunter's Mark. A marked unit takes the mark's percent more from every hit: it is read
    /// as a negative resistance to everything in <see cref="CombatBuffTracker.GetResistanceBonus"/>, so the
    /// cast path, the basic attack and the simulator all honour it without a line of their own. Abilities
    /// with <see cref="SpellEffect.BonusVsStatus"/> = Marked (Flushing Shot, Pack Hunt) hit it harder still.
    ///
    /// <para>Stored through <see cref="CombatBuffTracker.ApplyOverTime"/> only because that record carries
    /// an amount; this is not an <see cref="IOverTimeBuffHandler"/>, so it never ticks.</para>
    /// </summary>
    public class MarkedBuffHandler : IBuffHandler
    {
        public void Apply(ICombatUnit target, int power, int duration, CombatBuffTracker buffTracker, ICombatUnit source = null)
        {
            if (target == null || buffTracker == null || duration <= 0)
            {
                return;
            }
            // A debuff arrives negated (DebuffEffectExecutor); the percent is its magnitude.
            buffTracker.ApplyOverTime(target, BuffType.Marked, Mathf.Abs(power), duration, source);
        }

        public string GetDisplayText(int power)
        {
            return Loc.Format("combat.status.marked", ("power", Mathf.Abs(power)));
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
