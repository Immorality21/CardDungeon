using Assets.Scripts.Combat;
using Assets.Scripts.Items;

namespace Assets.Scripts.Cards
{
    /// <summary>
    /// The caster stat a <see cref="SpellEffect"/> scales off. <c>Attack</c> is first so it is the
    /// default for magic authored before caster stats existed, which keeps their numbers unchanged.
    /// </summary>
    public enum SpellScalingStat
    {
        Strength,
        Intelligence,
        Spirit,
        Luck,
        None
    }

    /// <summary>
    /// Turns a <see cref="SpellScalingStat"/> into the caster's contribution to an effect, buffs
    /// included. Kept separate from the executors so damage, healing and anything added later all
    /// scale the same way rather than each re-deriving it.
    /// </summary>
    public static class SpellScaling
    {
        public static int CasterContribution(ICombatUnit caster, SpellScalingStat stat, CombatBuffTracker buffTracker)
        {
            if (caster == null)
            {
                return 0;
            }

            switch (stat)
            {
                case SpellScalingStat.Strength:
                    // The raw attribute, not the hero's attack power: a Strength-scaled spell should
                    // not get stronger just because that hero happens to swing off Agility.
                    return caster.GetEffectiveStrength() + Buff(buffTracker, caster, StatType.Strength);
                case SpellScalingStat.Intelligence:
                    return caster.GetEffectiveIntelligence() + Buff(buffTracker, caster, StatType.Intelligence);
                case SpellScalingStat.Spirit:
                    return caster.GetEffectiveSpirit() + Buff(buffTracker, caster, StatType.Spirit);
                case SpellScalingStat.Luck:
                    return caster.GetEffectiveLuck() + Buff(buffTracker, caster, StatType.Luck);
                default:
                    return 0;
            }
        }

        private static int Buff(CombatBuffTracker tracker, ICombatUnit unit, StatType stat)
        {
            return tracker != null ? tracker.GetBuffAmount(unit, stat) : 0;
        }
    }
}
