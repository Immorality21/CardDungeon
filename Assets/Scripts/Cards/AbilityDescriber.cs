using System.Collections.Generic;
using Assets.Scripts.Cards.Buffs;
using Assets.Scripts.Combat;
using Assets.Scripts.UnitStats;
using UnityEngine;
using ImmoralityGaming.Localization;

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
            int critChance = caster != null ? Mathf.RoundToInt(EffectResolver.CritChanceWith(magic, caster) * 100f) : 0;
            foreach (var effect in magic.Effects)
            {
                if (effect == null || effect.UnlockLevel > upgradeLevel)
                {
                    continue;
                }

                var upgraded = EffectResolver.ApplyUpgrades(effect, upgrades);
                // A dormant debuff (0%, waiting on an upgrade) is not part of what the ability does yet.
                if (upgraded.EffectType == SpellEffectType.Debuff && upgraded.ApplyChance <= 0)
                {
                    continue;
                }
                string line = Describe(upgraded, caster, buffTracker, bonus);
                if (!string.IsNullOrEmpty(line))
                {
                    if (canCrit && effect.EffectType == SpellEffectType.Damage)
                    {
                        line = caster != null
                            ? Loc.Format("abilities.describe.crit-chance", ("line", line), ("chance", critChance))
                            : Loc.Format("abilities.describe.can-crit", ("line", line));
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
            string line = next != now ? Loc.Format("abilities.describe.forge-preview", ("now", now), ("next", next)) : now;

            bool scales = (effect.EffectType == SpellEffectType.Damage || effect.EffectType == SpellEffectType.Heal)
                          && effect.PowerMode != PowerMode.Flat && effect.PowerMode != PowerMode.PercentOfMaxHealth
                          && StatCatalog.CanScalePower(effect.ScalingStat);
            return scales ? Loc.Format("abilities.describe.forge-scales", ("line", line), ("stat", StatCatalog.ShortName(effect.ScalingStat))) : line;
        }

        /// <summary>Who the ability lands on, as a player would say it - "3 shots, random enemies" for a
        /// many-shot ability (<see cref="MagicSO.RandomHits"/>).</summary>
        public static string TargetLabel(MagicSO magic)
        {
            if (magic == null)
            {
                return string.Empty;
            }
            return magic.HasRandomHits ? Loc.Format("abilities.target.random-shots", ("count", magic.RandomHits)) : TargetLabel(magic.TargetType);
        }

        /// <summary>Who the ability lands on, as a player would say it.</summary>
        public static string TargetLabel(MagicTargetType targetType)
        {
            switch (targetType)
            {
                case MagicTargetType.AllEnemies:
                    return Loc.Get("abilities.target.all-enemies");
                case MagicTargetType.Self:
                    return Loc.Get("abilities.target.self");
                case MagicTargetType.SingleAlly:
                    return Loc.Get("abilities.target.one-ally");
                case MagicTargetType.AllAllies:
                    return Loc.Get("abilities.target.whole-party");
                case MagicTargetType.FallenAlly:
                    return Loc.Get("abilities.target.fallen-ally");
                default:
                    return Loc.Get("abilities.target.one-enemy");
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
            string target = TargetLabel(magic);
            if (effects.Count == 0)
            {
                parts.Add(target);
            }
            else
            {
                parts.Add(Loc.Format("abilities.describe.first-line", ("target", target), ("effect", effects[0])));
                for (int i = 1; i < effects.Count; i++)
                {
                    parts.Add(Loc.Format("abilities.describe.more-line", ("effect", effects[i])));
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
                        ? Loc.Format("abilities.describe.heal-percent", ("percent", effect.Power))
                        : Loc.Format("abilities.describe.heal", ("amount", Magnitude(effect, caster, buffTracker, powerBonus)));
                case SpellEffectType.Buff:
                    return StatusLine(effect, caster, buffTracker, +1);
                case SpellEffectType.Debuff:
                    return StatusLine(effect, caster, buffTracker, -1);
                case SpellEffectType.TurnDelay:
                    return Loc.Format("abilities.describe.turn-delay", ("percent", effect.Power));
                case SpellEffectType.Drain:
                    return effect.DrainToParty
                        ? Loc.Format("abilities.describe.drain-party", ("percent", effect.Power))
                        : Loc.Format("abilities.describe.drain-caster", ("percent", effect.Power));
                case SpellEffectType.RestoreCharge:
                    return effect.Power == 1
                        ? Loc.Get("abilities.describe.restore-charge-one")
                        : Loc.Format("abilities.describe.restore-charges", ("count", effect.Power));
                case SpellEffectType.Disassemble:
                    return Loc.Get("abilities.describe.disassemble");
                case SpellEffectType.Revive:
                    return Loc.Format("abilities.describe.revive", ("percent", effect.Power));
                case SpellEffectType.Cure:
                    return effect.Power <= 0
                        ? Loc.Get("abilities.describe.cure-all")
                        : effect.Power == 1 ? Loc.Get("abilities.describe.cure-one") : Loc.Format("abilities.describe.cure-many", ("count", effect.Power));
                case SpellEffectType.Intercept:
                    return Loc.Format("abilities.describe.intercept", ("percent", effect.Power));
                case SpellEffectType.Retaliate:
                    return effect.DamageType != DamageType.Normal
                        ? Loc.Format("abilities.describe.retaliate-element", ("amount", Magnitude(effect, caster, buffTracker, powerBonus)), ("element", effect.DamageType))
                        : Loc.Format("abilities.describe.retaliate", ("amount", Magnitude(effect, caster, buffTracker, powerBonus)));
                case SpellEffectType.Thorns:
                    return effect.Duration > 0
                        ? Loc.Format("abilities.describe.thorns-turns", ("percent", effect.Power), ("element", effect.DamageType), ("turns", effect.Duration))
                        : Loc.Format("abilities.describe.thorns", ("percent", effect.Power), ("element", effect.DamageType));
                case SpellEffectType.Execute:
                    return Loc.Format("abilities.describe.execute", ("target", TraitName(effect.BonusVsTrait, Loc.Get("abilities.describe.execute-any-foe"))), ("percent", effect.Power));
                case SpellEffectType.Steal:
                    return effect.Power >= 100
                        ? Loc.Get("abilities.describe.steal-certain")
                        : Loc.Get("abilities.describe.steal-luck");
                case SpellEffectType.FightGoldBonus:
                    return Loc.Format("abilities.describe.fight-gold", ("percent", effect.Power));
                case SpellEffectType.RefundCharge:
                    return Loc.Get("abilities.describe.refund-charge");
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
            bool elemental = effect.DamageType != DamageType.Normal;
            if (effect.PowerMode == PowerMode.PercentOfMaxHealth)
            {
                return elemental
                    ? Loc.Format("abilities.describe.damage-percent-element", ("percent", effect.Power), ("element", effect.DamageType))
                    : Loc.Format("abilities.describe.damage-percent", ("percent", effect.Power));
            }

            int amount = Magnitude(effect, caster, buffTracker, powerBonus);
            string line = elemental
                ? Loc.Format("abilities.describe.damage-element", ("amount", amount), ("element", effect.DamageType))
                : Loc.Format("abilities.describe.damage", ("amount", amount));
            if (effect.BonusPercent != 0 && effect.BonusVsTrait != UnitTraits.None)
            {
                line = effect.BonusPercent == 100
                    ? Loc.Format("abilities.describe.bonus-double", ("line", line), ("versus", TraitName(effect.BonusVsTrait, "")))
                    : Loc.Format("abilities.describe.bonus-percent", ("line", line), ("percent", effect.BonusPercent), ("versus", TraitName(effect.BonusVsTrait, "")));
            }
            else if (effect.BonusPercent != 0 && effect.BonusPerDebuff)
            {
                line = Loc.Format("abilities.describe.bonus-per-debuff", ("line", line), ("percent", effect.BonusPercent), ("max", SpellEffect.MaxDebuffBonuses));
            }
            else if (effect.BonusPercent != 0 && effect.BonusVsStatus != BuffType.None)
            {
                string status = effect.BonusVsStatus.ToString().ToLowerInvariant();
                line = effect.BonusPercent == 100
                    ? Loc.Format("abilities.describe.bonus-double", ("line", line), ("versus", status))
                    : Loc.Format("abilities.describe.bonus-percent", ("line", line), ("percent", effect.BonusPercent), ("versus", status));
            }
            if (caster != null)
            {
                int attack = caster.GetEffectiveAttackPower()
                    + (buffTracker != null ? buffTracker.GetBuffAmount(caster, caster.AttackStat) : 0);
                // The stat grid's name for it ("STR 7"), not a second word for the same number.
                line = Loc.Format("abilities.describe.with-attack", ("line", line), ("stat", StatCatalog.ShortName(caster.AttackStat)), ("value", attack));
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
                    text = Loc.Format("abilities.describe.status-max-hp", ("status", handler.GetDisplayText(0).Replace(" 0", "")), ("percent", effect.Power));
                }
                else
                {
                    int magnitude = effect.Power + SpellScaling.BuffContribution(caster, effect.ScalingStat, buffTracker);
                    // The handlers' texts are combat popups ("Frozen!"); a description does not shout.
                    text = handler.GetDisplayText(sign * magnitude).TrimEnd('!');
                }
            }

            string glossKey;
            if (Glosses.TryGetValue(effect.BuffType, out glossKey))
            {
                text = Loc.Format("abilities.describe.status-gloss", ("status", text), ("gloss", Loc.Get(glossKey)));
            }

            int turns = Mathf.Max(0, effect.Duration);
            if (turns > 0)
            {
                text = turns == 1
                    ? Loc.Format("abilities.describe.status-turn", ("status", text), ("turns", turns))
                    : Loc.Format("abilities.describe.status-turns", ("status", text), ("turns", turns));
            }

            // Every debuff is a chance (AfflictionRules); a certain one says nothing.
            if (sign < 0 && effect.ApplyChance < AfflictionRules.Certain)
            {
                text = Loc.Format("abilities.describe.status-chance", ("status", text), ("chance", effect.ApplyChance));
            }
            return text;
        }

        /// <summary>
        /// The statuses whose name does not say what they do. Stat changes and damage-over-time
        /// already read as numbers ("+2 Endurance", "Bleeding 1/turn") and get nothing. Values are text keys.
        /// </summary>
        private static readonly Dictionary<BuffType, string> Glosses = new Dictionary<BuffType, string>
        {
            { BuffType.Frozen, "abilities.gloss.frozen" },
            { BuffType.Slow, "abilities.gloss.slow" },
            { BuffType.Haste, "abilities.gloss.haste" },
            { BuffType.Silenced, "abilities.gloss.silenced" },
            { BuffType.Blinded, "abilities.gloss.blinded" },
            { BuffType.Leeched, "abilities.gloss.leeched" },
            { BuffType.Confused, "abilities.gloss.confused" },
            { BuffType.Marked, "abilities.gloss.marked" },
            { BuffType.Hidden, "abilities.gloss.hidden" },
            { BuffType.Evasive, "abilities.gloss.evasive" },
            { BuffType.DeathMarked, "abilities.gloss.death-marked" },
        };
    }
}
