using Assets.Scripts.Combat;
using Assets.Scripts.UnitStats;

namespace Assets.Scripts.Cards
{
    /// <summary>
    /// Turns a <see cref="SpellEffect"/>'s <c>ScalingStat</c> into the caster's contribution to that
    /// effect, buffs included. One place, so damage, healing, buffs and anything added later all
    /// scale identically instead of each executor re-deriving it.
    ///
    /// <para>There is no separate spell-scaling enum any more: an effect names a
    /// <see cref="StatType"/> directly, and <see cref="StatType.None"/> means "flat power, no
    /// scaling".</para>
    /// </summary>
    public static class SpellScaling
    {
        /// <summary>
        /// Buffs and debuffs get the caster stat divided by this. Their <c>Power</c> is a flat delta
        /// applied to a stat, not a damage number: a +3 Strength buff is already +30% on a
        /// 10-Strength hero, so adding a caster's stat in full would swamp the stat it is buffing.
        /// </summary>
        public const int BuffScalingDivisor = 4;

        /// <summary>
        /// Full caster contribution — what damage and healing add to Power.
        ///
        /// <para>A <see cref="StatType"/> that is a pool rather than an output contributes nothing,
        /// same as <see cref="StatType.None"/>. The inspector offers every stat in the dropdown, so
        /// without this a spell authored against MaxHealth would quietly add the caster's whole
        /// health bar to its power — 45 free damage on a mid-run hero, scaling with their gear.</para>
        /// </summary>
        public static int CasterContribution(ICombatUnit caster, StatType stat, CombatBuffTracker buffTracker)
        {
            if (caster == null || !StatCatalog.CanScalePower(stat))
            {
                return 0;
            }

            // Strength deliberately reads the raw attribute rather than the hero's attack power: a
            // Strength-scaled spell should not get stronger because that hero swings off Agility.
            int baseValue = caster.GetEffectiveStat(stat);
            int buff = buffTracker != null ? buffTracker.GetBuffAmount(caster, stat) : 0;
            return baseValue + buff;
        }

        /// <summary>Reduced contribution for buff/debuff magnitudes — see <see cref="BuffScalingDivisor"/>.</summary>
        public static int BuffContribution(ICombatUnit caster, StatType stat, CombatBuffTracker buffTracker)
        {
            return CasterContribution(caster, stat, buffTracker) / BuffScalingDivisor;
        }
    }
}
