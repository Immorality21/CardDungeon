using System;
using System.Collections.Generic;
using Assets.Scripts.Cards.Effects;
using Assets.Scripts.Combat;
using Assets.Scripts.Heroes;
using Assets.Scripts.Progression;
using UnityEngine;

namespace Assets.Scripts.Cards
{
    public class EffectResolver
    {
        private static readonly Color ComboNameColor = new Color(1f, 0.6f, 0f);
        private static readonly Color DodgeColor = new Color(0.8f, 0.8f, 0.85f);
        private const string DodgeText = "Dodge";
        private const string MissText = "Miss";
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
            // What the caster's sphere grid has added to this ability (MagicUpgrade nodes), folded into
            // copies of its effects below - the asset is shared and never changed. Null for an enemy, a
            // summon, or an ability with no upgrades.
            var upgrades = UpgradesOf(action);
            var targets = HitFilter(action, buffTracker, result);
            RollCrits(action, targets, upgrades);

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
                var effectToUse = ApplyUpgrades(
                    ApplyPowerBonus(effect, powerBonus + (upgrades != null ? upgrades.PowerBonus : 0), powerScale),
                    upgrades);
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
        /// The targets a physical attack actually reaches. For each living target other than the
        /// caster, a <b>blinded</b> caster first rolls its miss (<see cref="DefenseRules.RollMiss"/>),
        /// then the target rolls its Luck-based dodge; a target missed or dodged is dropped for every
        /// effect, tag and combo of the cast and gets a "Miss" or "Dodge" popup. Magic and non-attacks
        /// pass through untouched. The health-cost pass still reads <c>action.Targets</c> — a miss does
        /// not refund the price.
        /// </summary>
        private static List<ICombatUnit> HitFilter(SpellcastAction action, CombatBuffTracker buffTracker, EffectResult result)
        {
            if (action.Magic == null || action.Targets == null || !action.Magic.IsPhysicalAttack())
            {
                return action.Targets;
            }

            bool blinded = Buffs.BlindedBuffHandler.IsBlinded(buffTracker, action.Caster);
            var reached = new List<ICombatUnit>(action.Targets.Count);
            foreach (var target in action.Targets)
            {
                if (target != action.Caster && target.IsAlive)
                {
                    string avoided = DefenseRules.RollMiss(blinded) ? MissText
                        : DefenseRules.RollDodge(target) ? DodgeText
                        : null;
                    if (avoided != null)
                    {
                        result.Entries.Add(new EffectEntry
                        {
                            Target = target,
                            Text = avoided,
                            Color = DodgeColor
                        });
                        continue;
                    }
                }
                reached.Add(target);
            }
            return reached;
        }

        /// <summary>
        /// What <paramref name="caster"/>'s sphere grid has added to <paramref name="magic"/> (MagicUpgrade
        /// nodes), or null. Only a unit with a grid answers (<see cref="IAbilityUpgrades"/>: a hero, or the
        /// balance model's stand-in for one).
        /// </summary>
        public static AbilityGrant UpgradesOf(MagicSO magic, ICombatUnit caster)
        {
            return magic != null && caster is IAbilityUpgrades upgraded ? upgraded.UpgradesFor(magic.Key) : null;
        }

        private static AbilityGrant UpgradesOf(SpellcastAction action)
        {
            return UpgradesOf(action.Magic, action.Caster);
        }

        /// <summary>
        /// Whether this cast's damage can crit: the ability is authored to (<see cref="MagicSO.CanCrit"/>),
        /// or its caster's grid bought a crit upgrade for it (<see cref="AbilityGrant.CanCrit"/>).
        /// </summary>
        public static bool CanCrit(MagicSO magic, ICombatUnit caster)
        {
            if (magic == null || !magic.HasEffectType(SpellEffectType.Damage))
            {
                return false;
            }
            var upgrades = UpgradesOf(magic, caster);
            return magic.CanCrit || (upgrades != null && upgrades.CanCrit);
        }

        /// <summary>
        /// The crit chance <paramref name="caster"/> has with <paramref name="magic"/>: its Luck-based
        /// chance plus the ability's crit upgrades. 0 when the ability cannot crit at all. What the
        /// describer and the balance model read, so they agree with <see cref="RollCrits"/>.
        /// </summary>
        public static float CritChanceWith(MagicSO magic, ICombatUnit caster)
        {
            if (!CanCrit(magic, caster))
            {
                return 0f;
            }
            var upgrades = UpgradesOf(magic, caster);
            return Mathf.Clamp01(CritRules.ChanceFor(caster) + (upgrades != null ? upgrades.CritChanceBonus / 100f : 0f));
        }

        /// <summary>
        /// Rolls a crit once per reached enemy-side target for an ability that can crit, at the
        /// caster's own crit chance (<see cref="CritRules.ChanceFor"/>) plus the ability's crit upgrades,
        /// so a multi-hit cast and the riders on it agree on which targets were struck critically.
        /// </summary>
        private static void RollCrits(SpellcastAction action, List<ICombatUnit> targets, AbilityGrant upgrades)
        {
            action.CritTargets.Clear();
            if (targets == null || !CanCrit(action.Magic, action.Caster))
            {
                return;
            }
            float bonus = upgrades != null ? upgrades.CritChanceBonus / 100f : 0f;
            foreach (var target in targets)
            {
                if (target != action.Caster && target.IsAlive && CritRules.RollCrit(action.Caster, bonus))
                {
                    action.CritTargets.Add(target);
                }
            }
        }

        /// <summary>
        /// A copy of <paramref name="effect"/> with an ability's grid upgrades folded in: more turns on
        /// a timed Buff or Debuff, and a better chance on a Debuff (never past certain). Power is folded
        /// in by <see cref="ApplyPowerBonus"/>, the same path the old Forge bonus took. Returns the
        /// effect untouched when nothing applies, so an un-upgraded cast allocates nothing.
        /// </summary>
        public static SpellEffect ApplyUpgrades(SpellEffect effect, AbilityGrant upgrades)
        {
            if (effect == null || upgrades == null)
            {
                return effect;
            }
            bool timed = (effect.EffectType == SpellEffectType.Buff || effect.EffectType == SpellEffectType.Debuff)
                         && effect.Duration > 0;
            int turns = timed ? upgrades.DurationBonus : 0;
            int chance = effect.EffectType == SpellEffectType.Debuff ? upgrades.ApplyChanceBonus : 0;
            if (turns == 0 && chance == 0)
            {
                return effect;
            }
            var copy = effect.Clone();
            copy.Duration = Mathf.Max(1, effect.Duration + turns);
            copy.ApplyChance = Mathf.Clamp(effect.ApplyChance + chance, 1, AfflictionRules.Certain);
            return copy;
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

            // Clone, not a hand copy: a copy that names its fields drops the next one added. It
            // dropped ScalingStat once, so an upgraded magic lost its caster's stat entirely.
            var copy = effect.Clone();
            copy.Power = power;
            return copy;
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
