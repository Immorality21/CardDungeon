using System.Collections.Generic;
using Assets.Scripts.Cards.Buffs;
using Assets.Scripts.Combat;
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
        /// One line per active effect, e.g. "9 damage (Attack 7)" or "Bleeding 1/turn, 3 turns".
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

            foreach (var effect in magic.Effects)
            {
                if (effect == null || effect.UnlockLevel > upgradeLevel)
                {
                    continue;
                }

                string line = Describe(effect, caster, buffTracker, powerBonus);
                if (!string.IsNullOrEmpty(line))
                {
                    lines.Add(line);
                }
            }
            return lines;
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
            int upgradeLevel = 0)
        {
            if (magic == null)
            {
                return string.Empty;
            }

            var parts = new List<string>();
            if (!string.IsNullOrEmpty(magic.Description))
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
                default:
                    return null;
            }
        }

        private static string DamageLine(SpellEffect effect, ICombatUnit caster, CombatBuffTracker buffTracker, int powerBonus)
        {
            string element = effect.DamageType != DamageType.Normal ? $" {effect.DamageType}" : string.Empty;
            if (effect.PowerMode == PowerMode.PercentOfMaxHealth)
            {
                return $"{effect.Power}% of max HP{element} damage";
            }

            string line = $"{Magnitude(effect, caster, buffTracker, powerBonus)}{element} damage";
            if (caster != null)
            {
                int attack = caster.GetEffectiveAttackPower()
                    + (buffTracker != null ? buffTracker.GetBuffAmount(caster, caster.AttackStat) : 0);
                line += $" (Attack {attack})";
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
                int magnitude = effect.Power + SpellScaling.BuffContribution(caster, effect.ScalingStat, buffTracker);
                // The handlers' texts are combat popups ("Frozen!"); a description does not shout.
                text = handler.GetDisplayText(sign * magnitude).TrimEnd('!');
            }

            string gloss;
            if (Glosses.TryGetValue(effect.BuffType, out gloss))
            {
                text += $" ({gloss})";
            }

            int turns = Mathf.Max(0, effect.Duration);
            return turns > 0 ? $"{text}, {turns} turn{(turns == 1 ? "" : "s")}" : text;
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
        };
    }
}
