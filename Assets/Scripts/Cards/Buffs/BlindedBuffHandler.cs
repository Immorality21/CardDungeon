using Assets.Scripts.Combat;
using ImmoralityGaming.Localization;

namespace Assets.Scripts.Cards.Buffs
{
    /// <summary>
    /// The miss status. A blinded unit's <b>physical</b> hits - basic attacks, heavy blows and
    /// Strength/Agility abilities - miss <see cref="DefenseRules.BlindMissChance"/> of the time, rolled
    /// before the target's own dodge (<see cref="DefenseRules.RollMiss"/>). Magic is untouched, so Blind
    /// is the answer to a bruiser the way Silence is the answer to a caster, and a blinded caster
    /// shrugs it off.
    ///
    /// <para>Read where a physical hit is resolved: <c>CombatManager.ExecuteAttack</c>,
    /// <c>EffectResolver</c>'s hit filter and the simulator's <c>ResolveAttack</c>. Like Silence it
    /// never skips the turn.</para>
    /// </summary>
    public class BlindedBuffHandler : IBuffHandler
    {
        /// <summary>Whether <paramref name="unit"/> is blinded - null-safe for callers with no fight.</summary>
        public static bool IsBlinded(CombatBuffTracker buffTracker, ICombatUnit unit)
        {
            return buffTracker != null && unit != null && buffTracker.HasStatusEffect(unit, BuffType.Blinded);
        }

        public void Apply(ICombatUnit target, int power, int duration, CombatBuffTracker buffTracker, ICombatUnit source = null)
        {
            if (target == null || buffTracker == null || duration <= 0)
            {
                return;
            }

            buffTracker.ApplyStatusEffect(target, BuffType.Blinded, duration);
        }

        public string GetDisplayText(int power)
        {
            return Loc.Get("combat.status.blinded");
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
