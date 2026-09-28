using System;
using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Heroes;
using Assets.Scripts.UnitStats;
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

        // ---------------------------------------------------------------- party replacement

        /// <summary>
        /// The stat percentage a party-replacing summon brings for <paramref name="stat"/>, upgrades
        /// included: a <c>SummonPower</c> node adds <see cref="SummonGrant.PowerBonus"/> percentage
        /// points to <b>MaxHealth</b> — the kind has no effect Power to raise, so its "power" is its
        /// size (§4b: SummonPower → HP-ratio points).
        /// </summary>
        public static int PercentFor(SummonSO summon, SummonGrant grant, StatType stat)
        {
            if (summon == null || summon.StatPercents == null || stat == StatType.None)
            {
                return 0;
            }
            int percent = summon.StatPercents[stat];
            if (stat == StatType.MaxHealth && grant != null)
            {
                percent += grant.PowerBonus;
            }
            return Mathf.Max(0, percent);
        }

        /// <summary>
        /// A party-replacing summon's stat block: each stat of the summoner's, scaled by its
        /// percentage and rounded down, with a health floor of 1. <paramref name="summonerStat"/>
        /// is the summoner's base + gear stat and must <b>not</b> include combat buffs or level
        /// afflictions — the snapshot is what makes the summon one progression to price, with no
        /// buff-then-summon loop (§4b). Shared by the live game and the balance model.
        /// </summary>
        public static StatBlock StatsFor(SummonSO summon, SummonGrant grant, Func<StatType, int> summonerStat)
        {
            var block = new StatBlock();
            if (summon == null || summonerStat == null)
            {
                return block;
            }
            foreach (var stat in StatCatalog.Types)
            {
                int percent = PercentFor(summon, grant, stat);
                if (percent <= 0)
                {
                    continue;
                }
                int value = Mathf.FloorToInt(Mathf.Max(0, summonerStat(stat)) * percent / 100f);
                if (value > 0)
                {
                    block[stat] = value;
                }
            }
            if (block[StatType.MaxHealth] < 1)
            {
                block[StatType.MaxHealth] = 1;
            }
            return block;
        }

        /// <summary>How many of its own turns a party-replacing summon stays: its own count plus
        /// <c>SummonDuration</c> upgrades, at least 1.</summary>
        public static int TurnsFor(SummonSO summon, SummonGrant grant)
        {
            if (summon == null)
            {
                return 0;
            }
            return Mathf.Max(1, summon.TurnsActive + (grant != null ? grant.DurationBonus : 0));
        }

        /// <summary>
        /// Every ability a summon casts (its actions and its Signature), by key. These are
        /// <see cref="MagicSO"/> assets like any other, but they are reached through the summon and
        /// never through a grid node, the Forge or a loadout, so every check that asks "can a hero
        /// learn this?" must leave them out.
        /// </summary>
        public static HashSet<string> AbilityKeys(IEnumerable<SummonSO> summons)
        {
            var keys = new HashSet<string>();
            if (summons == null)
            {
                return keys;
            }
            foreach (var summon in summons.Where(s => s != null))
            {
                if (summon.Actions != null)
                {
                    foreach (var action in summon.Actions.Where(a => a != null && !string.IsNullOrEmpty(a.Key)))
                    {
                        keys.Add(action.Key);
                    }
                }
                if (summon.Signature != null && !string.IsNullOrEmpty(summon.Signature.Key))
                {
                    keys.Add(summon.Signature.Key);
                }
            }
            return keys;
        }

        /// <summary>One line for the grid, the report and the tooltip: "+50% STR for 3 turns · 1 charge".</summary>
        public static string Describe(SummonSO summon, SummonGrant grant)
        {
            if (summon == null)
            {
                return "";
            }
            if (summon.Kind == SummonKind.ReplaceParty)
            {
                return DescribeReplacement(summon, grant);
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

        /// <summary>"Takes the party's place · 250% of the summoner's health · 3 turns · 1 charge per run".</summary>
        private static string DescribeReplacement(SummonSO summon, SummonGrant grant)
        {
            var parts = new List<string> { "Takes the party's place" };
            int health = PercentFor(summon, grant, StatType.MaxHealth);
            if (health > 0)
            {
                parts.Add($"{health}% of the summoner's health");
            }
            int turns = TurnsFor(summon, grant);
            parts.Add(turns == 1 ? "1 turn" : $"{turns} turns");
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
