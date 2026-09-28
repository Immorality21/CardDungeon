using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Heroes;
using UnityEngine;

namespace Assets.Scripts.Cards
{
    /// <summary>
    /// The pure rules of a summon: what it does once its grid upgrades are folded in, how many
    /// charges it has, and the transient <see cref="MagicSO"/> the effect engine casts. Covered by
    /// <c>SummonTests</c>.
    /// </summary>
    public static class SummonOps
    {
        /// <summary>Prefix of the transient magic's key, so a summon's cast can never collide with,
        /// or be upgraded as, a real ability.</summary>
        public const string CastKeyPrefix = "summon:";

        /// <summary>Charges per run: the summon's base plus its charge upgrades, at least 1.</summary>
        public static int MaxCharges(SummonSO summon, SummonGrant grant)
        {
            if (summon == null)
            {
                return 0;
            }
            return Mathf.Max(1, summon.BaseCharges + (grant != null ? grant.ChargeBonus : 0));
        }

        /// <summary>
        /// The summon's effects with its upgrades applied: <see cref="SummonGrant.PowerBonus"/>
        /// added to every effect's Power, <see cref="SummonGrant.DurationBonus"/> to every timed
        /// effect's Duration. Returns copies — the asset is never modified.
        /// </summary>
        public static List<SpellEffect> EffectsFor(SummonSO summon, SummonGrant grant)
        {
            var effects = new List<SpellEffect>();
            if (summon == null || summon.Effects == null)
            {
                return effects;
            }

            int power = grant != null ? grant.PowerBonus : 0;
            int turns = grant != null ? grant.DurationBonus : 0;
            foreach (var effect in summon.Effects.Where(e => e != null))
            {
                effects.Add(new SpellEffect
                {
                    EffectType = effect.EffectType,
                    Power = effect.Power + power,
                    PowerMode = effect.PowerMode,
                    ScalingStat = effect.ScalingStat,
                    DamageType = effect.DamageType,
                    BuffType = effect.BuffType,
                    Duration = effect.Duration > 0 ? effect.Duration + turns : effect.Duration,
                    UnlockLevel = 0
                });
            }
            return effects;
        }

        /// <summary>
        /// A throwaway <see cref="MagicSO"/> carrying the upgraded effects, so a summon resolves
        /// through the very same <c>EffectResolver</c> an ability does. No tags (a summon triggers no
        /// combo) and a prefixed key (it takes no Forge upgrade). The caller owns it and should
        /// destroy it once the cast has resolved.
        /// </summary>
        public static MagicSO BuildCastable(SummonSO summon, SummonGrant grant)
        {
            var magic = ScriptableObject.CreateInstance<MagicSO>();
            magic.hideFlags = HideFlags.DontSave;
            magic.name = summon != null ? CastKeyPrefix + summon.Key : CastKeyPrefix;
            magic.Key = magic.name;
            magic.DisplayName = summon != null ? summon.Label : "Summon";
            magic.Description = summon != null ? summon.Description : "";
            magic.TargetType = summon != null ? summon.TargetType : MagicTargetType.AllAllies;
            magic.Effects = EffectsFor(summon, grant);
            magic.Tags = new List<MagicTag>();
            return magic;
        }

        /// <summary>One line for the grid, the report and the tooltip: "+50% STR for 3 turns · 1 charge".</summary>
        public static string Describe(SummonSO summon, SummonGrant grant)
        {
            if (summon == null)
            {
                return "";
            }
            var parts = new List<string>();
            foreach (var effect in EffectsFor(summon, grant))
            {
                parts.Add(DescribeEffect(effect));
            }
            int charges = MaxCharges(summon, grant);
            parts.Add(charges == 1 ? "1 charge per run" : $"{charges} charges per run");
            return string.Join(" · ", parts);
        }

        private static string DescribeEffect(SpellEffect effect)
        {
            string turns = effect.Duration > 0 ? $" for {effect.Duration} turns" : "";
            switch (effect.EffectType)
            {
                case SpellEffectType.Buff:
                    return effect.PowerMode == PowerMode.PercentOfTargetStat
                        ? $"+{effect.Power}% {effect.BuffType}{turns}"
                        : $"+{effect.Power} {effect.BuffType}{turns}";
                case SpellEffectType.Debuff:
                    return $"-{effect.Power} {effect.BuffType}{turns}";
                case SpellEffectType.Damage:
                    return $"{effect.Power} {effect.DamageType} damage";
                case SpellEffectType.Heal:
                    return $"heals {effect.Power}";
                default:
                    return effect.EffectType.ToString();
            }
        }
    }
}
