using Assets.Scripts.Combat;
using Assets.Scripts.UnitStats;
using UnityEngine;

namespace Assets.Scripts.Cards
{
    /// <summary>
    /// Turns a <see cref="SpellEffect"/>'s <c>Power</c> plus its <see cref="PowerMode"/> into the
    /// number an executor actually applies. One place, so damage, healing and health costs read the
    /// mode identically instead of each executor re-deriving it — the same reason
    /// <see cref="SpellScaling"/> exists for the caster contribution.
    ///
    /// <para>Also home to the health-cost arithmetic, because it is what makes the cost
    /// <b>predictable</b>: <see cref="ResolveHealthCost"/> is a pure function of authored data and
    /// the caster's max health, so the UI can grey out a spell the caster cannot pay for using the
    /// exact number the executor will charge.</para>
    /// </summary>
    public static class SpellPower
    {
        /// <summary>
        /// Magnitude for a Damage or Heal effect landing on <paramref name="target"/>: the raw
        /// damage before defense and resistance, or the health restored before the max-health clamp.
        /// </summary>
        /// <param name="flatPower">
        /// The caller's override for "this power belongs to the definition, not to whoever triggered
        /// it" (a combo's bonus effect, a room event's outcome). Equivalent to
        /// <see cref="PowerMode.Flat"/>, and it does <b>not</b> override
        /// <see cref="PowerMode.PercentOfMaxHealth"/> — a percentage effect is still a percentage
        /// with no caster at all.
        /// </param>
        public static int Resolve(
            SpellEffect effect,
            ICombatUnit caster,
            ICombatUnit target,
            CombatBuffTracker buffTracker,
            bool flatPower = false)
        {
            if (effect == null)
            {
                return 0;
            }

            if (effect.PowerMode == PowerMode.PercentOfMaxHealth)
            {
                return PercentOfMaxHealth(effect.Power, target);
            }

            if (flatPower || effect.PowerMode == PowerMode.Flat)
            {
                return effect.Power;
            }

            return effect.Power + SpellScaling.CasterContribution(caster, effect.ScalingStat, buffTracker);
        }

        /// <summary>
        /// <paramref name="percent"/> of <paramref name="unit"/>'s max health, rounded <b>down</b>
        /// with a floor of 1 — so a percentage effect on a small health bar still does something,
        /// and a 0% effect stays nothing.
        /// </summary>
        public static int PercentOfMaxHealth(int percent, ICombatUnit unit)
        {
            if (unit == null || percent <= 0)
            {
                return 0;
            }

            int maxHealth = unit.GetEffectiveStat(StatType.MaxHealth);
            return Mathf.Max(1, Mathf.FloorToInt(maxHealth * percent / 100f));
        }

        /// <summary>
        /// What one <see cref="SpellEffectType.HealthCost"/> effect charges its caster. The cost is
        /// paid in raw health: no defense, no resistance, no upgrade bonus — upgrading a spell must
        /// never raise its price.
        /// </summary>
        public static int ResolveHealthCost(SpellEffect effect, ICombatUnit caster)
        {
            if (effect == null || effect.EffectType != SpellEffectType.HealthCost)
            {
                return 0;
            }

            if (effect.PowerMode == PowerMode.PercentOfMaxHealth)
            {
                return PercentOfMaxHealth(effect.Power, caster);
            }

            return Mathf.Max(0, effect.Power);
        }

        /// <summary>
        /// Everything <paramref name="magic"/> would charge <paramref name="caster"/> in health,
        /// counting only the effects unlocked at <paramref name="magicUpgradeLevel"/> — the same
        /// gate <see cref="EffectResolver"/> applies, so the quoted price is the price paid.
        /// </summary>
        public static int TotalHealthCost(MagicSO magic, ICombatUnit caster, int magicUpgradeLevel = 0)
        {
            if (magic == null || magic.Effects == null)
            {
                return 0;
            }

            int total = 0;
            foreach (var effect in magic.Effects)
            {
                if (effect == null || effect.UnlockLevel > magicUpgradeLevel)
                {
                    continue;
                }
                total += ResolveHealthCost(effect, caster);
            }

            return total;
        }

        /// <summary>
        /// Whether <paramref name="caster"/> can pay for <paramref name="magic"/> and still be
        /// standing. Strictly greater than the cost: a spell that would drop its caster to exactly 0
        /// is refused rather than allowed as a suicide play.
        ///
        /// <para>The cast is <b>gated</b> rather than allowed to kill, which is what keeps the whole
        /// death-mid-cast problem from existing: <c>ExecuteCastAction</c> has no death handling, so a
        /// caster who killed themselves would stop acting with no death log and no visual. The
        /// executor keeps a 1 HP floor as a safety net for the same reason.</para>
        /// </summary>
        public static bool CanAfford(MagicSO magic, ICombatUnit caster, int magicUpgradeLevel = 0)
        {
            int cost = TotalHealthCost(magic, caster, magicUpgradeLevel);
            if (cost <= 0)
            {
                return true;
            }

            return caster != null && caster.Stats.Health > cost;
        }
    }
}
