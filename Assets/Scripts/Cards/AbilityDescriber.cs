using System.Collections.Generic;
using Assets.Scripts.Cards.Buffs;
using Assets.Scripts.Combat;
using Assets.Scripts.UnitStats;
using UnityEngine;

namespace Assets.Scripts.Cards
{
    /// <summary>
    /// What an ability does, in words, for the hero about to use it - the text under the ability
    /// picker. Pure, so it is testable without a scene.
    ///
    /// <para>Until 2026-09-28 the picker showed "Slash 2/2" and nothing else: not what it hit for,
    /// not that it bled, not how it differed from Attack (playtest finding 8). The authored
    /// <see cref="MagicSO.Description"/> is flavour and rarely says the mechanics, so the mechanics
    /// are generated from the effects themselves and cannot drift from what the spell does.</para>
    ///
    /// <para>Numbers come from the same arithmetic the executors run - <see cref="SpellPower"/>,
    /// <see cref="SpellScaling"/>, the Forge's power bonus - and are <b>before</b> the target's
    /// defense and resistances, which the picker cannot know yet. Attack's number is given beside
    /// the damage for the same reason: both are raw, so the comparison is fair.</para>
    /// </summary>
    public static class AbilityDescriber
    {
        /// <summary>
        /// One line per active effect, e.g. "9 damage (STR 7)" or "Bleeding 1/turn, 3 turns".
        /// Effects locked behind a higher upgrade level are left out, as the resolver skips them.
        /// Health costs are left out too - the picker row already shows the price.
        /// </summary>
        public static List<string> EffectLines(
            MagicSO magic,
            ICombatUnit caster,
            CombatBuffTracker buffTracker,
            int powerBonus = 0,
            int upgradeLevel = 0)
        {
            var lines = new List<string>();
            if (magic == null || magic.Effects == null)
            {
                return lines;
            }

            // The caster's grid upgrades for this ability (MagicUpgrade nodes) are folded in exactly as
            // EffectResolver folds them in at the cast, so the text shows the numbers it will hit for.
            var upgrades = EffectResolver.UpgradesOf(magic, caster);
            int bonus = powerBonus + (upgrades != null ? upgrades.PowerBonus : 0);

            // Whether it can crit depends on the caster too (a grid node can unlock it), so it is said
            // on the damage line rather than read off the asset alone - with the real chance when there
            // is a caster to read it from.
            bool canCrit = EffectResolver.CanCrit(magic, caster);
            string critText = caster != null
                ? $", {Mathf.RoundToInt(EffectResolver.CritChanceWith(magic, caster) * 100f)}% crit"
                : ", can crit";
            foreach (var effect in magic.Effects)
            {
                if (effect == null || effect.UnlockLevel > upgradeLevel)
                {
                    continue;
                }

                string line = Describe(EffectResolver.ApplyUpgrades(effect, upgrades), caster, buffTracker, bonus);
                if (!string.IsNullOrEmpty(line))
                {
                    if (canCrit && effect.EffectType == SpellEffectType.Damage)
                    {
                        line += critText;
                    }
                    lines.Add(line);
                }
            }
            return lines;
        }

        /// <summary>
        /// The Forge's line for one effect: the same wording as the picker and the Storehouse, with no
        /// caster (the Forge belongs to the party, not a hero), the stat it scales with named instead
        /// of added, and - when the next upgrade changes the number - the preview
        /// ("3 damage → 5 damage + STR"). Locked effects are described too; the caller marks them.
        /// </summary>
        public static string ForgeLine(SpellEffect effect, int powerBonus, int nextPowerBonus)
        {
            if (effect == null)
            {
                return string.Empty;
            }
            string now = Describe(effect, null, null, powerBonus);
            if (string.IsNullOrEmpty(now))
            {
                return effect.EffectType.ToString();
            }
            string next = nextPowerBonus != powerBonus ? Describe(effect, null, null, nextPowerBonus) : now;
            string line = next != now ? $"{now} → {next}" : now;

            bool scales = (effect.EffectType == SpellEffectType.Damage || effect.EffectType == SpellEffectType.Heal)
                          && effect.PowerMode != PowerMode.Flat && effect.PowerMode != PowerMode.PercentOfMaxHealth
                          && StatCatalog.CanScalePower(effect.ScalingStat);
            return scales ? $"{line} + {StatCatalog.ShortName(effect.ScalingStat)}" : line;
        }

        /// <summary>Who the ability lands on, as a player would say it.</summary>
        public static string TargetLabel(MagicTargetType targetType)
        {
            switch (targetType)
            {
                case MagicTargetType.AllEnemies:
                    return "All enemies";
                case MagicTargetType.Self:
                    return "Self";
                case MagicTargetType.SingleAlly:
                    return "One ally";
                case MagicTargetType.AllAllies:
                    return "Whole party";
                case MagicTargetType.FallenAlly:
                    return "One fallen ally";
                default:
                    return "One enemy";
            }
        }

        /// <summary>
        /// The whole footer: flavour, then who it hits and what it does - one effect per line.
        /// </summary>
        public static string Full(
            MagicSO magic,
            ICombatUnit caster,
            CombatBuffTracker buffTracker,
            int powerBonus = 0,
            int upgradeLevel = 0,
            bool withDescription = true)
        {
            if (magic == null)
            {
                return string.Empty;
            }

            var parts = new List<string>();
            if (withDescription && !string.IsNullOrEmpty(magic.Description))
            {
                parts.Add(magic.Description.Trim());
            }

            // One effect per line: the picker window is narrow, and a joined line wrapped mid-number
            // ("Bleed 2/" | "turn").
            var effects = EffectLines(magic, caster, buffTracker, powerBonus, upgradeLevel);
            string target = TargetLabel(magic.TargetType);
            if (effects.Count == 0)
            {
                parts.Add(target);
            }
            else
            {
                parts.Add($"{target}: {effects[0]}");
                for (int i = 1; i < effects.Count; i++)
                {
                    parts.Add("+ " + effects[i]);
                }
            }
            return string.Join("\n", parts);
        }

        private static string Describe(SpellEffect effect, ICombatUnit caster, CombatBuffTracker buffTracker, int powerBonus)
        {
            switch (effect.EffectType)
            {
                case SpellEffectType.Damage:
                    return DamageLine(effect, caster, buffTracker, powerBonus);
                case SpellEffectType.Heal:
                    return effect.PowerMode == PowerMode.PercentOfMaxHealth
                        ? $"heals {effect.Power}% of max HP"
                        : $"heals {Magnitude(effect, caster, buffTracker, powerBonus)} HP";
                case SpellEffectType.Buff:
                    return StatusLine(effect, caster, buffTracker, +1);
                case SpellEffectType.Debuff:
                    return StatusLine(effect, caster, buffTracker, -1);
                case SpellEffectType.TurnDelay:
                    return $"pushes its next turn back {effect.Power}% of a turn";
                case SpellEffectType.Drain:
                    return effect.DrainToParty
                        ? $"heals the whole party {effect.Power}% of the damage dealt"
                        : $"heals the caster {effect.Power}% of the damage dealt";
                case SpellEffectType.RestoreCharge:
                    return effect.Power == 1
                        ? "restores 1 charge to an ability"
                        : $"restores {effect.Power} charges to an ability";
                case SpellEffectType.Disassemble:
                    return "takes a machine apart outright - likelier the more of its kind you have defeated";
                case SpellEffectType.Revive:
                    return $"revives a fallen hero with {effect.Power}% of max HP";
                case SpellEffectType.Cure:
                    return effect.Power <= 0
                        ? "clears every debuff"
                        : effect.Power == 1 ? "clears one debuff" : $"clears {effect.Power} debuffs";
                case SpellEffectType.Intercept:
                    return $"takes the single-target blows aimed at them until your next turn, at {effect.Power}% less damage";
                case SpellEffectType.Retaliate:
                    return $"strikes back once at the next attacker for {Magnitude(effect, caster, buffTracker, powerBonus)}"
                           + (effect.DamageType != DamageType.Normal ? $" {effect.DamageType}" : "") + " damage";
                case SpellEffectType.Thorns:
                    return $"returns {effect.Power}% of each blow taken as {effect.DamageType} damage"
                           + (effect.Duration > 0 ? $" for {effect.Duration} turns" : "");
                case SpellEffectType.Execute:
                    return $"destroys {TraitName(effect.BonusVsTrait, "a foe")} under {effect.Power}% health outright (not bosses)";
                default:
                    return null;
            }
        }

        /// <summary>"Undead" for a trait, or <paramref name="none"/> when there is none.</summary>
        private static string TraitName(UnitTraits trait, string none)
        {
            return trait == UnitTraits.None ? none : trait.ToString();
        }

        private static string DamageLine(SpellEffect effect, ICombatUnit caster, CombatBuffTracker buffTracker, int powerBonus)
        {
            string element = effect.DamageType != DamageType.Normal ? $" {effect.DamageType}" : string.Empty;
            if (effect.PowerMode == PowerMode.PercentOfMaxHealth)
            {
                return $"{effect.Power}% of max HP{element} damage";
            }

            string line = $"{Magnitude(effect, caster, buffTracker, powerBonus)}{element} damage";
            if (effect.BonusPercent != 0 && effect.BonusVsTrait != UnitTraits.None)
            {
                line += effect.BonusPercent == 100
                    ? $", double vs {TraitName(effect.BonusVsTrait, "")}"
                    : $", +{effect.BonusPercent}% vs {TraitName(effect.BonusVsTrait, "")}";
            }
            if (caster != null)
            {
                int attack = caster.GetEffectiveAttackPower()
                    + (buffTracker != null ? buffTracker.GetBuffAmount(caster, caster.AttackStat) : 0);
                // The stat grid's name for it ("STR 7"), not a second word for the same number.
                line += $" ({StatCatalog.ShortName(caster.AttackStat)} {attack})";
            }
            return line;
        }

        /// <summary>Damage/heal magnitude before defense: the Forge bonus on the base, then the caster's stat.</summary>
        private static int Magnitude(SpellEffect effect, ICombatUnit caster, CombatBuffTracker buffTracker, int powerBonus)
        {
            int power = effect.Power + (effect.PowerMode == PowerMode.Flat || effect.PowerMode == PowerMode.BasePower
                ? Mathf.Max(0, powerBonus)
                : 0);
            if (effect.PowerMode == PowerMode.Flat)
            {
                return power;
            }
            return power + SpellScaling.CasterContribution(caster, effect.ScalingStat, buffTracker);
        }

        private static string StatusLine(SpellEffect effect, ICombatUnit caster, CombatBuffTracker buffTracker, int sign)
        {
            var handler = BuffHandlerRegistry.Get(effect.BuffType);
            if (handler == null)
            {
                return null;
            }

            string text;
            if (effect.PowerMode == PowerMode.PercentOfTargetStat)
            {
                // A percentage of each target's own stat: the number depends on who it lands on.
                // "+20 Strength" -> "+20% Strength".
                text = handler.GetDisplayText(sign * effect.Power).TrimEnd('!');
                int space = text.IndexOf(' ');
                text = space > 0 ? text.Insert(space, "%") : text + "%";
            }
            else
            {
                if (effect.PowerMode == PowerMode.PercentOfMaxHealth)
                {
                    // A share of each target's own bar (a barrier): "Barrier of 25% max HP".
                    text = handler.GetDisplayText(0).Replace(" 0", "") + $" of {effect.Power}% max HP";
                }
                else
                {
                    int magnitude = effect.Power + SpellScaling.BuffContribution(caster, effect.ScalingStat, buffTracker);
                    // The handlers' texts are combat popups ("Frozen!"); a description does not shout.
                    text = handler.GetDisplayText(sign * magnitude).TrimEnd('!');
                }
            }

            string gloss;
            if (Glosses.TryGetValue(effect.BuffType, out gloss))
            {
                text += $" ({gloss})";
            }

            int turns = Mathf.Max(0, effect.Duration);
            text = turns > 0 ? $"{text}, {turns} turn{(turns == 1 ? "" : "s")}" : text;

            // Every debuff is a chance (AfflictionRules); a certain one says nothing.
            if (sign < 0 && effect.ApplyChance < AfflictionRules.Certain)
            {
                text += $", {effect.ApplyChance}% chance";
            }
            return text;
        }

        /// <summary>
        /// The statuses whose name does not say what they do. Stat changes and damage-over-time
        /// already read as numbers ("+2 Endurance", "Bleeding 1/turn") and get nothing.
        /// </summary>
        private static readonly Dictionary<BuffType, string> Glosses = new Dictionary<BuffType, string>
        {
            { BuffType.Frozen, "loses its turns" },
            { BuffType.Slow, "acts less often" },
            { BuffType.Haste, "acts more often" },
            { BuffType.Silenced, "cannot use abilities" },
            { BuffType.Blinded, "physical hits may miss" },
            { BuffType.Leeched, "of each hit on it heals the caster" },
        };
    }
}
