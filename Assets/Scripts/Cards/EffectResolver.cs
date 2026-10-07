using System;
using System.Collections.Generic;
using Assets.Scripts.Cards.Effects;
using Assets.Scripts.Combat;
using Assets.Scripts.Progression;
using UnityEngine;

namespace Assets.Scripts.Cards
{
    public class EffectResolver
    {
        private static readonly Color ComboNameColor = new Color(1f, 0.6f, 0f);
        private static readonly Color DodgeColor = new Color(0.8f, 0.8f, 0.85f);
        private const string DodgeText = "Dodge";
        private const float ComboDelay = 0.3f;

        private readonly EffectExecutorFactory _factory;

        /// <summary>
        /// The fight's turn clock, which a <see cref="SpellEffectType.TurnDelay"/> pushes units back
        /// on. Set by whoever owns the fight - <c>CombatManager</c> and the simulator's encounter
        /// loop - and left null everywhere else, where a delay is inert.
        /// </summary>
        public TurnManager Clock { get; set; }

        /// <summary>
        /// Where the fight keeps ability charges, which a <see cref="SpellEffectType.RestoreCharge"/>
        /// refills. Set by <c>CombatManager</c> at combat start; null everywhere else - the balance
        /// model included - where a restore is inert.
        /// </summary>
        public IChargeBank Charges { get; set; }

        /// <summary>
        /// The fight's event stream, raised by every effect that moves health - which is how a kill
        /// made by an ability is credited to its caster. Set by whoever owns the fight, like
        /// <see cref="Clock"/>; null everywhere else (room events), where health still moves but
        /// nobody is told.
        /// </summary>
        public CombatEvents Events { get; set; }

        /// <summary>
        /// How many of a unit's kind the player has defeated, which a
        /// <see cref="SpellEffectType.Disassemble"/> reads for its odds. <c>CombatManager</c> answers
        /// from the Bestiary; null everywhere else (the balance model included), where every machine
        /// reads as never defeated - the cautious odds.
        /// </summary>
        public Func<ICombatUnit, int> KillsOf { get; set; }

        /// <summary>The roll behind chance-based effects (Disassemble): uniform in [0, 1). Null uses
        /// <c>Random.Range(0f, 1f)</c>; tests set a fixed one.</summary>
        public Func<float> Roll { get; set; }

        public EffectResolver()
        {
            _factory = new EffectExecutorFactory(() => Clock, () => Charges, () => Events, () => KillsOf, () => Roll);
        }

        /// <param name="powerBonus">Flat power added to the magic's Damage/Heal effects (from its upgrade level).</param>
        /// <param name="magicUpgradeLevel">The magic's upgrade level — effects with a higher UnlockLevel are skipped.</param>
        /// <param name="comboLevelLookup">Returns a combo's upgrade level by key (gates combo effects + scales combo power); null = level 0.</param>
        /// <param name="powerScale">
        /// Multiplier on each Damage/Heal effect's base Power, applied after <paramref name="powerBonus"/>.
        /// 1 leaves the authored numbers alone, which is every hero cast. It exists for <b>enemy</b>
        /// casts: an enemy's spells scale with its level's <c>EnemyTuning.Difficulty</c>, the same
        /// dial that scales the Strength its basic attack swings off, so its magic escalates across
        /// the campaign instead of staying at floor-one power. Buff/Debuff power is left alone for
        /// the same reason the upgrade bonus leaves it alone - it is a stat delta, not a damage number.
        /// </param>
        public EffectResult Execute(
            SpellcastAction action,
            CombatBuffTracker buffTracker,
            MagicTagTracker tagTracker = null,
            ComboDetector comboDetector = null,
            int powerBonus = 0,
            int magicUpgradeLevel = 0,
            Func<string, int> comboLevelLookup = null,
            float powerScale = 1f)
        {
            var result = new EffectResult();
            var targets = DodgeFilter(action, result);

            // Three passes: benefits, then drains, then costs. A HealthCost authored first would take
            // the caster down before the buff it paid for was applied, and BuffEffectExecutor skips
            // dead targets — so the card would silently charge for nothing. A Drain reads the damage
            // the cast has dealt, so it has to wait for all of it. Ordering it here rather than
            // documenting an authoring rule means the card works however it is written.
            foreach (var effect in action.Magic.Effects)
            {
                if (effect.UnlockLevel > magicUpgradeLevel
                    || effect.EffectType == SpellEffectType.HealthCost
                    || effect.EffectType == SpellEffectType.Drain)
                {
                    continue;
                }
                var effectToUse = ApplyPowerBonus(effect, powerBonus, powerScale);
                var executor = _factory.GetExecutor(effectToUse.EffectType);
                if (executor is ICastAwareEffectExecutor castAware)
                {
                    castAware.Execute(effectToUse, action, targets, buffTracker, result);
                }
                else
                {
                    executor.Execute(effectToUse, action.Caster, targets, buffTracker, result);
                }
            }

            foreach (var effect in action.Magic.Effects)
            {
                if (effect.UnlockLevel > magicUpgradeLevel || effect.EffectType != SpellEffectType.Drain)
                {
                    continue;
                }
                // A percentage of what landed: no upgrade bonus (it would read as percentage points)
                // and no level scale (the damage it reads was already scaled).
                _factory.GetExecutor(SpellEffectType.Drain)
                    .Execute(effect, action.Caster, targets, buffTracker, result);
            }

            foreach (var effect in action.Magic.Effects)
            {
                if (effect.UnlockLevel > magicUpgradeLevel || effect.EffectType != SpellEffectType.HealthCost)
                {
                    continue;
                }
                // No upgrade bonus and no level scale: a health cost is a price, and upgrading a
                // spell must never raise what it charges.
                _factory.GetExecutor(SpellEffectType.HealthCost)
                    .Execute(effect, action.Caster, action.Targets, buffTracker, result);
            }

            if (tagTracker != null && comboDetector != null && action.Magic.Tags.Count > 0)
            {
                foreach (var target in targets)
                {
                    if (!target.IsAlive)
                    {
                        continue;
                    }

                    var combo = comboDetector.DetectCombo(action.Magic.Tags, target, tagTracker);
                    if (combo != null)
                    {
                        ApplyCombo(combo, target, action.Caster, buffTracker, result, comboLevelLookup);
                    }
                }

                foreach (var target in targets)
                {
                    tagTracker.ApplyTags(target, action.Magic.Tags, action.Magic.TagDuration);
                }
            }

            // Raised here, not by each caller, so the live fight and the simulator - which both resolve
            // every cast, summon, Ultra strike and enemy spell through this method - cannot disagree
            // about when an ability was used. A throwaway castable (a summon's, an Ultra's) is valid
            // only while handlers run.
            Events?.Publish(new AbilityUsed
            {
                Caster = action.Caster,
                Ability = action.Magic,
                Targets = action.Targets != null ? new List<ICombatUnit>(action.Targets) : new List<ICombatUnit>()
            });
            return result;
        }

        /// <summary>
        /// Resolves a loose list of effects with <paramref name="source"/> as their caster - a triggered
        /// reaction's (<c>TriggerRegistry</c>), which has no <see cref="MagicSO"/> of its own. The same
        /// three passes a cast runs (benefits, then drains, then costs) and the same executors, so a
        /// reaction can be anything an ability can be; no dodge roll, no tags, no combo and no upgrade
        /// bonus. Power scales off the source's stats as authored (<c>ScalingStat</c>), so a flat
        /// reaction is authored with <c>StatType.None</c>.
        /// </summary>
        public EffectResult ExecuteEffects(
            IList<SpellEffect> effects,
            ICombatUnit source,
            List<ICombatUnit> targets,
            CombatBuffTracker buffTracker)
        {
            var result = new EffectResult();
            if (effects == null || targets == null)
            {
                return result;
            }

            foreach (var pass in EffectPasses)
            {
                foreach (var effect in effects)
                {
                    if (effect == null || PassOf(effect.EffectType) != pass)
                    {
                        continue;
                    }
                    _factory.GetExecutor(effect.EffectType).Execute(effect, source, targets, buffTracker, result);
                }
            }
            return result;
        }

        private static readonly int[] EffectPasses = { 0, 1, 2 };

        /// <summary>Which of the three passes an effect resolves in: benefits 0, drains 1, costs 2.</summary>
        private static int PassOf(SpellEffectType type)
        {
            switch (type)
            {
                case SpellEffectType.Drain:
                    return 1;
                case SpellEffectType.HealthCost:
                    return 2;
                default:
                    return 0;
            }
        }

        /// <summary>
        /// The targets a physical attack actually reaches: each living target other than the caster
        /// rolls its Luck-based dodge once, and one that dodges is dropped for every effect, tag and
        /// combo of the cast (and gets a "Dodge" popup). Magic and non-attacks pass through untouched.
        /// The health-cost pass still reads <c>action.Targets</c> — a dodge does not refund the price.
        /// </summary>
        private static List<ICombatUnit> DodgeFilter(SpellcastAction action, EffectResult result)
        {
            if (action.Magic == null || action.Targets == null || !action.Magic.IsPhysicalAttack())
            {
                return action.Targets;
            }

            var reached = new List<ICombatUnit>(action.Targets.Count);
            foreach (var target in action.Targets)
            {
                if (target != action.Caster && target.IsAlive && DefenseRules.RollDodge(target))
                {
                    result.Entries.Add(new EffectEntry
                    {
                        Target = target,
                        Text = DodgeText,
                        Color = DodgeColor
                    });
                    continue;
                }
                reached.Add(target);
            }
            return reached;
        }

        /// <summary>
        /// Returns a copy of the effect with a permanent card-upgrade bonus folded into its
        /// Power and then multiplied by <paramref name="powerScale"/>, for Damage/Heal effects only. Buff/Debuff power (a stat amount) is left
        /// unchanged. Returns the original effect when there is no bonus to apply.
        /// </summary>
        private SpellEffect ApplyPowerBonus(SpellEffect effect, int powerBonus, float powerScale = 1f)
        {
            bool scales = !Mathf.Approximately(powerScale, 1f);
            if (powerBonus <= 0 && !scales)
            {
                return effect;
            }

            if (effect.EffectType != SpellEffectType.Damage && effect.EffectType != SpellEffectType.Heal)
            {
                return effect;
            }

            // A percentage effect is already relative to the target's health bar, so it scales for
            // the rest of the game's life on its own. Adding the flat upgrade bonus on top would read
            // as percentage points — +2 per level turns a 10% spell into 20% at max upgrade.
            if (effect.PowerMode == PowerMode.PercentOfMaxHealth)
            {
                return effect;
            }

            int power = effect.Power + Mathf.Max(0, powerBonus);
            if (scales && power > 0)
            {
                power = Mathf.Max(1, Mathf.RoundToInt(power * powerScale));
            }

            return new SpellEffect
            {
                EffectType = effect.EffectType,
                Power = power,
                // Every other field has to be carried across, or the copy quietly differs from the
                // authored effect. ScalingStat was the one that mattered: it defaults to None, so an
                // upgraded magic lost its caster contribution entirely - upgrading a caster's spell
                // made it weaker by that caster's whole scaling stat, and still hit for a plausible
                // number, so nothing looked wrong.
                ScalingStat = effect.ScalingStat,
                PowerMode = effect.PowerMode,
                DamageType = effect.DamageType,
                BuffType = effect.BuffType,
                Duration = effect.Duration,
                UnlockLevel = effect.UnlockLevel
            };
        }

        private void ApplyCombo(
            MagicComboSO combo,
            ICombatUnit target,
            ICombatUnit caster,
            CombatBuffTracker buffTracker,
            EffectResult result,
            Func<string, int> comboLevelLookup)
        {
            result.ComboName = combo.ComboName;
            if (!string.IsNullOrEmpty(combo.Key) && !result.TriggeredComboKeys.Contains(combo.Key))
            {
                result.TriggeredComboKeys.Add(combo.Key);
            }

            result.Entries.Add(new EffectEntry
            {
                Target = target,
                Text = combo.ComboName,
                Color = ComboNameColor,
                Delay = ComboDelay,
                PositionOffset = Vector3.up * 0.3f
            });

            int comboLevel = comboLevelLookup != null && !string.IsNullOrEmpty(combo.Key)
                ? comboLevelLookup(combo.Key)
                : 0;
            int comboPowerBonus = MetaProgressManager.ComboPowerBonusForLevel(comboLevel);

            foreach (var effect in combo.BonusEffects)
            {
                if (effect.UnlockLevel > comboLevel)
                {
                    continue;
                }
                var effectToUse = ApplyPowerBonus(effect, comboPowerBonus);
                var comboTargets = GetComboTargets(effectToUse.EffectType, caster, target);
                var executor = _factory.GetExecutor(effectToUse.EffectType);
                executor.Execute(effectToUse, caster, comboTargets, buffTracker, result, flatPower: true);
            }
        }

        private List<ICombatUnit> GetComboTargets(SpellEffectType effectType, ICombatUnit caster, ICombatUnit target)
        {
            switch (effectType)
            {
                case SpellEffectType.Heal:
                case SpellEffectType.Buff:
                    return new List<ICombatUnit> { caster };
                default:
                    return new List<ICombatUnit> { target };
            }
        }
    }
}
