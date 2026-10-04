using System;
using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Combat;
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
        /// added to the <b>first</b> effect's Power - the headline, which is what a power node is
        /// priced against - and <see cref="SummonGrant.DurationBonus"/> to every timed effect's
        /// Duration. Returns copies — the asset is never modified.
        ///
        /// <para>First only, because effects read Power in different units: on the Paladin's Dawn
        /// Stag a +10 meant for its 40% heal would also have turned a 3-a-turn regeneration into
        /// 13 (2026-09-30).</para>
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
                    Power = effect.Power + (effects.Count == 0 ? power : 0),
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
        /// A unit-building summon's stat block (a party replacement or an ally): each stat of the summoner's, scaled by its
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

        /// <summary>
        /// The units a party-replacing summon brings, front rank first. A single-unit summon (the
        /// Golem) brings itself. A squad (<see cref="SummonSO.IsSquad"/>, the Demon Army) brings
        /// <see cref="SummonSO.SquadSize"/> troops plus <see cref="SummonGrant.SizeBonus"/>, capped at
        /// <see cref="SummonSO.MaxSquadSize"/>, all of the weakest tier; then each of
        /// <see cref="SummonGrant.Promotions"/> raises the weakest troop one tier - the front rank
        /// first - until every troop is at the top tier. Three Imps and two promotions is two
        /// Succubi and an Imp.
        /// </summary>
        public static List<SummonSO> SquadFor(SummonSO summon, SummonGrant grant)
        {
            var units = new List<SummonSO>();
            if (summon == null)
            {
                return units;
            }
            if (!summon.IsSquad)
            {
                units.Add(summon);
                return units;
            }

            var tiers = summon.SquadTiers.Where(t => t != null).ToList();
            int max = Mathf.Max(1, summon.MaxSquadSize);
            int size = Mathf.Clamp(summon.SquadSize + (grant != null ? grant.SizeBonus : 0), 1, max);
            var levels = new int[size];
            int promotions = grant != null ? Mathf.Max(0, grant.Promotions) : 0;
            for (int p = 0; p < promotions; p++)
            {
                int weakest = -1;
                for (int i = 0; i < size; i++)
                {
                    if (levels[i] < tiers.Count - 1 && (weakest < 0 || levels[i] < levels[weakest]))
                    {
                        weakest = i;
                    }
                }
                if (weakest < 0)
                {
                    break;   // every troop is at the top tier
                }
                levels[weakest]++;
            }
            foreach (int level in levels)
            {
                units.Add(tiers[level]);
            }
            return units;
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
                if (summon.AttackAbility != null && !string.IsNullOrEmpty(summon.AttackAbility.Key))
                {
                    keys.Add(summon.AttackAbility.Key);
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
            if (summon.Kind == SummonKind.ReplaceParty || summon.Kind == SummonKind.JoinParty)
            {
                return DescribeReplacement(summon, grant);
            }
            var parts = new List<string>();
            foreach (var effect in EffectsFor(summon, grant))
            {
                parts.Add(DescribeEffect(effect));
            }
            parts = CollapseRepeats(parts);
            if (summon.BonusThreat > 0)
            {
                parts.Add("draws the enemies' attention");
            }
            int charges = MaxCharges(summon, grant);
            parts.Add(charges == 1 ? "1 charge per run" : $"{charges} charges per run");
            return string.Join(" · ", parts);
        }

        /// <summary>"Takes the party's place · 250% of the summoner's health · 3 turns · 1 charge per run",
        /// "Fights beside the party · ..." for an ally, "Takes the party's place · 2 Imps and a Succubus · ..."
        /// for a squad.</summary>
        private static string DescribeReplacement(SummonSO summon, SummonGrant grant)
        {
            var parts = new List<string>
            {
                summon.Kind == SummonKind.JoinParty ? "Fights beside the party" : "Takes the party's place"
            };
            if (summon.IsSquad)
            {
                parts.Add(DescribeSquad(SquadFor(summon, grant)));
            }
            else
            {
                int health = PercentFor(summon, grant, StatType.MaxHealth);
                if (health > 0)
                {
                    parts.Add($"{health}% of the summoner's health");
                }
            }
            int turns = TurnsFor(summon, grant);
            parts.Add(turns == 1 ? "1 turn" : $"{turns} turns");
            int charges = MaxCharges(summon, grant);
            parts.Add(charges == 1 ? "1 charge per run" : $"{charges} charges per run");
            return string.Join(" · ", parts);
        }

        /// <summary>"3 Imps", "2 Succubi and an Imp": the troops by kind, strongest first.</summary>
        public static string DescribeSquad(List<SummonSO> troops)
        {
            var groups = troops.Where(t => t != null)
                .GroupBy(t => t.Label)
                .Select(g => new { Name = g.Key, Count = g.Count(), Index = troops.IndexOf(g.First()) })
                .OrderBy(g => g.Index)
                .Select(g => g.Count == 1 ? Article(g.Name) + " " + g.Name : g.Count + " " + Plural(g.Name))
                .ToList();
            if (groups.Count <= 1)
            {
                return groups.Count == 1 ? groups[0] : "";
            }
            return string.Join(", ", groups.Take(groups.Count - 1)) + " and " + groups[groups.Count - 1];
        }

        private static string Article(string name)
        {
            return name.Length > 0 && "AEIOUaeiou".IndexOf(name[0]) >= 0 ? "an" : "a";
        }

        private static string Plural(string name)
        {
            if (name.EndsWith("us"))
            {
                return name.Substring(0, name.Length - 2) + "i";   // Succubus -> Succubi
            }
            return name.EndsWith("s") ? name + "es" : name + "s";
        }

        /// <summary>Repeated identical lines read as one with a count: two equal hits are "AGI damage ×2",
        /// not the same phrase twice.</summary>
        private static List<string> CollapseRepeats(List<string> parts)
        {
            var collapsed = new List<string>();
            int i = 0;
            while (i < parts.Count)
            {
                int run = 1;
                while (i + run < parts.Count && parts[i + run] == parts[i])
                {
                    run++;
                }
                collapsed.Add(run > 1 ? $"{parts[i]} ×{run}" : parts[i]);
                i += run;
            }
            return collapsed;
        }

        private static string DescribeEffect(SpellEffect effect)
        {
            string turns = effect.Duration > 0 ? $" for {effect.Duration} turns" : "";
            switch (effect.EffectType)
            {
                case SpellEffectType.Buff:
                    if (effect.BuffType == BuffType.Regenerating)
                    {
                        return $"regenerates {effect.Power} a turn{turns}";
                    }
                    return effect.PowerMode == PowerMode.PercentOfTargetStat
                        ? $"+{effect.Power}% {effect.BuffType}{turns}"
                        : $"+{effect.Power} {effect.BuffType}{turns}";
                case SpellEffectType.Debuff:
                    return $"-{effect.Power} {effect.BuffType}{turns}";
                case SpellEffectType.Damage:
                {
                    string element = effect.DamageType != DamageType.Normal ? $" {effect.DamageType}" : "";
                    if (!StatCatalog.CanScalePower(effect.ScalingStat) || effect.PowerMode != PowerMode.BasePower)
                    {
                        return $"{effect.Power}{element} damage";
                    }
                    string stat = StatCatalog.ShortName(effect.ScalingStat);
                    return effect.Power > 0 ? $"{effect.Power} + {stat}{element} damage" : $"{stat}{element} damage";
                }
                case SpellEffectType.TurnDelay:
                    return $"delays by {effect.Power}% of a turn";
                case SpellEffectType.Drain:
                    return $"drains {effect.Power}% of the damage";
                case SpellEffectType.Heal:
                    return effect.PowerMode == PowerMode.PercentOfMaxHealth
                        ? $"heals {effect.Power}% of health"
                        : $"heals {effect.Power}";
                default:
                    return effect.EffectType.ToString();
            }
        }
    }
}
