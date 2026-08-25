using System.Collections.Generic;
using Assets.Scripts.Cards;
using Assets.Scripts.Cards.Buffs;
using Assets.Scripts.Combat;
using Assets.Scripts.Enemies;
using Assets.Scripts.Enemies.Behaviors;
using Assets.Scripts.Items;
using Assets.Scripts.Progression;
using Assets.Scripts.Rooms;
using Assets.Scripts.UnitStats;
using UnityEngine;

namespace Assets.Scripts.Balance
{
    /// <summary>
    /// How the simulated party plays. Comparing outcomes across policies is what exposes a shallow
    /// encounter: if <see cref="AttackOnly"/> does as well as <see cref="Adaptive"/>, the fight has no
    /// decisions in it, however well-tuned its numbers are.
    /// </summary>
    public enum SimPolicy
    {
        /// <summary>Basic attacks only, focus-firing the weakest enemy. The floor.</summary>
        AttackOnly,

        /// <summary>Always cast when a charge is available, ignoring the situation. Tests raw magic value.</summary>
        MagicFirst,

        /// <summary>Heal when hurt, cast when it helps, attack otherwise. Stands in for competent play.</summary>
        Adaptive
    }

    /// <summary>Knobs for a simulation batch.</summary>
    public class SimSettings
    {
        public int Trials = 200;
        public int Seed = 20260819;
        public int MaxTurns = 300;
        public SimPolicy Policy = SimPolicy.Adaptive;

        public int PotionCount;
        public int PotionHealAmount;

        /// <summary>Combos the resolver may fire; pass the project's combo assets to model them.</summary>
        public List<MagicComboSO> Combos = new List<MagicComboSO>();

        /// <summary>Fraction of max HP below which the Adaptive policy reaches for a heal.</summary>
        public float HealThreshold = 0.45f;
    }

    /// <summary>Aggregated results over a simulation batch.</summary>
    public class SimOutcome
    {
        public SimPolicy Policy;
        public int Trials;
        public int Wins;
        public int Stalemates;

        public float AverageTurns;
        public float AverageEndHealthFraction;   // over winning trials
        public float AverageHeroDeaths;
        public float AveragePotionsUsed;
        public float AverageCastsUsed;

        public float WinRate => Trials > 0 ? (float)Wins / Trials : 0f;

        /// <summary>
        /// A single comparable score: win rate weighted by how much health the party keeps. Two
        /// policies that both win 100% of the time are separated by what the win cost them, which is
        /// what makes the dominant-strategy comparison meaningful.
        /// </summary>
        public float Score => WinRate * (0.5f + 0.5f * AverageEndHealthFraction);
    }

    /// <summary>
    /// A headless run of the real combat loop. It reuses <see cref="TurnManager"/>,
    /// <see cref="DamageCalculator"/>, <see cref="CombatBuffTracker"/>, <see cref="EffectResolver"/>,
    /// <see cref="MagicTagTracker"/>, <see cref="ComboDetector"/> and the actual
    /// <see cref="EnemyActionPlanner"/>, so enemy decision-making, buffs, combos and
    /// damage are the game's, not a copy.
    ///
    /// What it re-implements is the turn *loop* in <see cref="CombatManager"/> (a coroutine on a
    /// MonoBehaviour, so it cannot be called from here) and the player's choices, which are a UI in
    /// the real game. Those two are the drift risk: <c>EncounterSimulatorTests</c> pins the simulated
    /// hit against <c>CombatManager</c>'s formula to catch it.
    ///
    /// Known simplifications, all deliberate:
    /// <list type="bullet">
    /// <item>Drawing magic mid-fight is not modelled — a hero's slots are whatever they start with,
    /// so the simulator never spends a turn on Draw.</item>
    /// <item>Fleeing is never attempted.</item>
    /// <item>Magic charges refill at the start of every fight, matching <c>RefillCharges()</c>.</item>
    /// </list>
    /// </summary>
    public static class EncounterSimulator
    {
        /// <summary>
        /// Shared resolver for enemy casts. <see cref="EffectResolver"/> holds no per-fight state
        /// (just its executor factory), so one instance is safe and saves threading it through
        /// the enemy turn the way the hero turn threads its own.
        /// </summary>
        private static readonly EffectResolver CastResolver = new EffectResolver();

        public static SimOutcome Run(
            PartyBaseline party,
            IList<SimUnit> enemyTemplates,
            SimSettings settings)
        {
            var outcome = new SimOutcome { Policy = settings.Policy };
            if (party == null || party.Size == 0 || enemyTemplates == null || enemyTemplates.Count == 0)
            {
                return outcome;
            }

            // Keep the caller's random stream intact: the whole batch runs on a fixed seed so the
            // same assets always produce the same numbers.
            var savedState = Random.state;
            Random.InitState(settings.Seed);

            float turnTotal = 0f;
            float endHealthTotal = 0f;
            float deathTotal = 0f;
            float potionTotal = 0f;
            float castTotal = 0f;

            try
            {
                for (int trial = 0; trial < settings.Trials; trial++)
                {
                    var result = RunOne(party, enemyTemplates, settings);

                    outcome.Trials++;
                    turnTotal += result.Turns;
                    deathTotal += result.HeroDeaths;
                    potionTotal += result.PotionsUsed;
                    castTotal += result.Casts;

                    if (result.Stalemate)
                    {
                        outcome.Stalemates++;
                    }
                    else if (result.PartyWon)
                    {
                        outcome.Wins++;
                        endHealthTotal += result.EndHealthFraction;
                    }
                }
            }
            finally
            {
                Random.state = savedState;
            }

            if (outcome.Trials > 0)
            {
                outcome.AverageTurns = turnTotal / outcome.Trials;
                outcome.AverageHeroDeaths = deathTotal / outcome.Trials;
                outcome.AveragePotionsUsed = potionTotal / outcome.Trials;
                outcome.AverageCastsUsed = castTotal / outcome.Trials;
            }
            outcome.AverageEndHealthFraction = outcome.Wins > 0 ? endHealthTotal / outcome.Wins : 0f;

            return outcome;
        }

        /// <summary>Runs all three policies over the same encounter, for the dominant-strategy check.</summary>
        public static Dictionary<SimPolicy, SimOutcome> RunAllPolicies(
            PartyBaseline party,
            IList<SimUnit> enemyTemplates,
            SimSettings settings)
        {
            var results = new Dictionary<SimPolicy, SimOutcome>();
            foreach (SimPolicy policy in System.Enum.GetValues(typeof(SimPolicy)))
            {
                var perPolicy = new SimSettings
                {
                    Trials = settings.Trials,
                    Seed = settings.Seed,
                    MaxTurns = settings.MaxTurns,
                    Policy = policy,
                    PotionCount = settings.PotionCount,
                    PotionHealAmount = settings.PotionHealAmount,
                    Combos = settings.Combos,
                    HealThreshold = settings.HealThreshold
                };
                results[policy] = Run(party, enemyTemplates, perPolicy);
            }
            return results;
        }

        private class TrialResult
        {
            public bool PartyWon;
            public bool Stalemate;
            public int Turns;
            public int HeroDeaths;
            public int PotionsUsed;
            public int Casts;
            public float EndHealthFraction;
        }

        private static TrialResult RunOne(PartyBaseline party, IList<SimUnit> enemyTemplates, SimSettings settings)
        {
            var result = new TrialResult();

            var heroes = party.CloneUnits();
            var enemies = new List<SimUnit>();
            foreach (var template in enemyTemplates)
            {
                if (template != null)
                {
                    enemies.Add(template.Clone());
                }
            }

            var units = new List<ICombatUnit>();
            foreach (var hero in heroes)
            {
                units.Add(hero);
            }
            foreach (var enemy in enemies)
            {
                units.Add(enemy);
            }

            var buffTracker = new CombatBuffTracker();
            var tagTracker = new MagicTagTracker();
            var comboDetector = new ComboDetector(settings.Combos ?? new List<MagicComboSO>());
            var resolver = new EffectResolver();

            var turnManager = new TurnManager();
            turnManager.SetBuffTracker(buffTracker);
            turnManager.Initialize(units);

            int potionsLeft = settings.PotionCount;
            int maxHealthPool = 0;
            foreach (var hero in heroes)
            {
                maxHealthPool += hero.Effective[StatType.MaxHealth];
            }

            while (AnyAlive(heroes) && AnyAlive(enemies) && result.Turns < settings.MaxTurns)
            {
                var unit = turnManager.GetNextUnit();
                if (unit == null)
                {
                    break;
                }

                result.Turns++;

                if (!unit.IsAlive)
                {
                    continue;
                }

                // Frozen and friends skip the turn but still tick, exactly as in the live loop.
                if (SkipsTurn(unit, buffTracker))
                {
                    buffTracker.TickBuffs(unit);
                    tagTracker.TickTags(unit);
                    continue;
                }

                var actor = unit as SimUnit;
                if (actor == null)
                {
                    continue;
                }

                if (actor.IsHero)
                {
                    TakeHeroTurn(actor, heroes, enemies, buffTracker, tagTracker, comboDetector, resolver,
                        settings, ref potionsLeft, result);
                }
                else
                {
                    TakeEnemyTurn(actor, heroes, enemies, buffTracker);
                }

                buffTracker.TickBuffs(unit);
                tagTracker.TickTags(unit);

                // Dead units leave the tick queue, as HandleEnemyDeath / ResolveHeroDamaged do.
                foreach (var enemy in enemies)
                {
                    if (!enemy.IsAlive)
                    {
                        turnManager.RemoveUnit(enemy);
                    }
                }
                foreach (var hero in heroes)
                {
                    if (!hero.IsAlive)
                    {
                        turnManager.RemoveUnit(hero);
                    }
                }
            }

            foreach (var hero in heroes)
            {
                if (!hero.IsAlive)
                {
                    result.HeroDeaths++;
                }
            }

            bool heroesAlive = AnyAlive(heroes);
            bool enemiesAlive = AnyAlive(enemies);

            if (heroesAlive && enemiesAlive)
            {
                result.Stalemate = true;
            }
            else
            {
                result.PartyWon = heroesAlive;
            }

            if (result.PartyWon && maxHealthPool > 0)
            {
                int remaining = 0;
                foreach (var hero in heroes)
                {
                    remaining += Mathf.Max(0, hero.Stats.Health);
                }
                result.EndHealthFraction = (float)remaining / maxHealthPool;
            }

            return result;
        }

        // ---------------------------------------------------------------- hero turns

        private static void TakeHeroTurn(
            SimUnit hero,
            List<SimUnit> heroes,
            List<SimUnit> enemies,
            CombatBuffTracker buffTracker,
            MagicTagTracker tagTracker,
            ComboDetector comboDetector,
            EffectResolver resolver,
            SimSettings settings,
            ref int potionsLeft,
            TrialResult result)
        {
            if (settings.Policy == SimPolicy.Adaptive)
            {
                var wounded = MostWounded(heroes);
                if (wounded != null && HealthFraction(wounded) <= settings.HealThreshold)
                {
                    if (potionsLeft > 0 && settings.PotionHealAmount > 0)
                    {
                        potionsLeft--;
                        result.PotionsUsed++;
                        wounded.Stats.Health = Mathf.Min(
                            wounded.Stats.Health + settings.PotionHealAmount,
                            wounded.Stats.MaxHealth);
                        return;
                    }

                    var healSlot = FindSlot(hero, SpellEffectType.Heal);
                    if (healSlot != null)
                    {
                        Cast(hero, healSlot, heroes, enemies, buffTracker, tagTracker, comboDetector, resolver, result);
                        return;
                    }
                }
            }

            if (settings.Policy != SimPolicy.AttackOnly)
            {
                var damageSlot = BestDamageSlot(hero, enemies, buffTracker);
                if (damageSlot != null)
                {
                    Cast(hero, damageSlot, heroes, enemies, buffTracker, tagTracker, comboDetector, resolver, result);
                    return;
                }
            }

            var target = WeakestAlive(enemies);
            if (target != null)
            {
                ResolveAttack(hero, target, buffTracker);
            }
        }

        private static void Cast(
            SimUnit caster,
            SimMagicSlot slot,
            List<SimUnit> heroes,
            List<SimUnit> enemies,
            CombatBuffTracker buffTracker,
            MagicTagTracker tagTracker,
            ComboDetector comboDetector,
            EffectResolver resolver,
            TrialResult result)
        {
            var targets = ResolveTargets(slot.Magic, caster, heroes, enemies);
            if (targets.Count == 0)
            {
                var fallback = WeakestAlive(enemies);
                if (fallback != null)
                {
                    ResolveAttack(caster, fallback, buffTracker);
                }
                return;
            }

            slot.Charges--;
            result.Casts++;

            var action = new SpellcastAction
            {
                Magic = slot.Magic,
                Caster = caster,
                Targets = targets
            };

            resolver.Execute(
                action,
                buffTracker,
                tagTracker,
                comboDetector,
                MetaProgressManager.MagicPowerBonusForLevel(slot.UpgradeLevel),
                slot.UpgradeLevel,
                null);
        }

        private static List<ICombatUnit> ResolveTargets(
            MagicSO magic,
            SimUnit caster,
            List<SimUnit> heroes,
            List<SimUnit> enemies)
        {
            var targets = new List<ICombatUnit>();

            switch (magic.TargetType)
            {
                case MagicTargetType.SingleEnemy:
                {
                    var target = WeakestAlive(enemies);
                    if (target != null)
                    {
                        targets.Add(target);
                    }
                    break;
                }
                case MagicTargetType.AllEnemies:
                    foreach (var enemy in enemies)
                    {
                        if (enemy.IsAlive)
                        {
                            targets.Add(enemy);
                        }
                    }
                    break;
                case MagicTargetType.Self:
                    targets.Add(caster);
                    break;
                case MagicTargetType.SingleAlly:
                {
                    var ally = MostWounded(heroes) ?? caster;
                    targets.Add(ally);
                    break;
                }
                case MagicTargetType.AllAllies:
                    foreach (var hero in heroes)
                    {
                        if (hero.IsAlive)
                        {
                            targets.Add(hero);
                        }
                    }
                    break;
            }

            return targets;
        }

        private static SimMagicSlot FindSlot(SimUnit hero, SpellEffectType effectType)
        {
            foreach (var slot in hero.MagicSlots)
            {
                if (slot.CanCast && slot.Magic.HasEffectType(effectType))
                {
                    return slot;
                }
            }
            return null;
        }

        /// <summary>The castable damage slot that would land the most damage on the weakest enemy.</summary>
        private static SimMagicSlot BestDamageSlot(SimUnit hero, List<SimUnit> enemies, CombatBuffTracker buffTracker)
        {
            var target = WeakestAlive(enemies);
            if (target == null)
            {
                return null;
            }

            SimMagicSlot best = null;
            float bestDamage = BasicAttackDamage(hero, target, buffTracker);

            foreach (var slot in hero.MagicSlots)
            {
                if (!slot.CanCast || !slot.Magic.HasEffectType(SpellEffectType.Damage))
                {
                    continue;
                }

                float damage = EstimateMagicDamage(hero, slot, target, buffTracker);
                if (damage > bestDamage)
                {
                    bestDamage = damage;
                    best = slot;
                }
            }

            return best;
        }

        private static float EstimateMagicDamage(SimUnit caster, SimMagicSlot slot, SimUnit target, CombatBuffTracker buffTracker)
        {
            float total = 0f;
            int powerBonus = MetaProgressManager.MagicPowerBonusForLevel(slot.UpgradeLevel);
            int attackBonus = buffTracker.GetBuffAmount(caster, caster.AttackStat);
            int defense = target.GetEffectiveStat(StatType.Endurance) + buffTracker.GetBuffAmount(target, StatType.Endurance);

            foreach (var effect in slot.Magic.Effects)
            {
                if (effect.EffectType != SpellEffectType.Damage || effect.UnlockLevel > slot.UpgradeLevel)
                {
                    continue;
                }

                int raw = caster.GetEffectiveAttackPower() + attackBonus + effect.Power + powerBonus;
                total += DamageCalculator.Calculate(raw, defense, effect.DamageType, target.Resistances);
            }

            return total;
        }

        private static float BasicAttackDamage(SimUnit attacker, SimUnit target, CombatBuffTracker buffTracker)
        {
            int attackBonus = buffTracker.GetBuffAmount(attacker, attacker.AttackStat);
            int defense = target.GetEffectiveStat(StatType.Endurance) + buffTracker.GetBuffAmount(target, StatType.Endurance);
            return DamageCalculator.Calculate(attacker.GetEffectiveAttackPower() + attackBonus, defense, DamageType.Normal, target.Resistances);
        }

        // ---------------------------------------------------------------- enemy turns

        private static void TakeEnemyTurn(
            SimUnit enemy,
            List<SimUnit> heroes,
            List<SimUnit> enemies,
            CombatBuffTracker buffTracker)
        {
            var behavior = enemy.Behavior != null
                ? enemy.Behavior
                : EnemyBehaviorSO.BuiltInPreset(enemy.Archetype);

            var context = new EnemyCombatContext
            {
                Heroes = AliveAs(heroes),
                Allies = AliveAsExcept(enemies, enemy),
                BuffTracker = buffTracker,
                ChargingEntryIndex = enemy.ChargingEntryIndex,
                SelfTurnCount = enemy.TurnsTaken,
                DrawableMagics = enemy.Definition != null ? enemy.Definition.DrawableMagics : null
            };

            // The same planner call CombatManager.ExecuteEnemyTurn makes, so there is no second
            // decision implementation to drift.
            var decision = EnemyActionPlanner.Plan(
                enemy, context, behavior, EnemyPlanRolls.Random(behavior.Actions.Count));

            switch (decision.Type)
            {
                case EnemyActionType.CastMagic:
                    ResolveCast(enemy, decision, buffTracker);
                    break;

                case EnemyActionType.ChargeHeavy:
                    enemy.ChargingEntryIndex = decision.EntryIndex;
                    enemy.ChargeTarget = decision.Target;
                    break;

                case EnemyActionType.HeavyAttack:
                {
                    var target = enemy.ChargeTarget as SimUnit;
                    if (target == null || !target.IsAlive)
                    {
                        target = RandomAlive(heroes);
                    }
                    enemy.ChargingEntryIndex = -1;
                    enemy.ChargeTarget = null;
                    if (target != null)
                    {
                        ResolveAttack(enemy, target, buffTracker, decision.Multiplier);
                    }
                    break;
                }

                case EnemyActionType.ChargeAoe:
                    enemy.ChargingEntryIndex = decision.EntryIndex;
                    enemy.ChargeTarget = null;
                    break;

                case EnemyActionType.AoeAttack:
                {
                    enemy.ChargingEntryIndex = -1;
                    enemy.ChargeTarget = null;
                    foreach (var hero in heroes)
                    {
                        if (hero.IsAlive)
                        {
                            ResolveAttack(enemy, hero, buffTracker, decision.Multiplier);
                        }
                    }
                    break;
                }

                case EnemyActionType.Heal:
                {
                    var target = decision.Target;
                    if (target != null && target.IsAlive)
                    {
                        target.Stats.Health = Mathf.Min(target.Stats.Health + decision.Amount, target.Stats.MaxHealth);
                    }
                    break;
                }

                case EnemyActionType.Debuff:
                {
                    var target = decision.Target;
                    if (target != null && target.IsAlive)
                    {
                        buffTracker.ApplyBuff(target, decision.DebuffStat, -decision.Amount, decision.Duration);
                    }
                    break;
                }

                default:
                {
                    var target = decision.Target as SimUnit;
                    if (target == null || !target.IsAlive)
                    {
                        target = RandomAlive(heroes);
                    }
                    if (target != null)
                    {
                        ResolveAttack(enemy, target, buffTracker, decision.Multiplier);
                    }
                    break;
                }
            }

            enemy.TurnsTaken++;
        }

        /// <summary>
        /// Resolves an enemy cast through the real <see cref="EffectResolver"/> — the same call
        /// <c>CombatManager.ExecuteEnemyCast</c> makes, with the same arguments: no upgrade bonus, no
        /// upgrade level, no tag tracker or combo detector, and the level's spell-power scale. That
        /// reuse is the point; a second implementation here would be free to drift.
        /// </summary>
        private static void ResolveCast(SimUnit enemy, EnemyDecision decision, CombatBuffTracker buffTracker)
        {
            var targets = new List<ICombatUnit>();
            foreach (var target in decision.MagicTargets)
            {
                if (target != null && target.IsAlive)
                {
                    targets.Add(target);
                }
            }

            if (decision.Magic == null || targets.Count == 0)
            {
                return;
            }

            var action = new SpellcastAction
            {
                Magic = decision.Magic,
                Caster = enemy,
                Targets = targets
            };

            float powerScale = LevelEnemyTuning.MagicPowerScaleFor(enemy.Definition, enemy.Tuning);
            CastResolver.Execute(action, buffTracker, null, null, 0, 0, null, powerScale);
        }

        /// <summary>
        /// One basic attack, byte for byte the same arithmetic as <c>CombatManager.ExecuteAttack</c>
        /// (buff bonuses, multiplier rounding, defense, resistance, then the crit roll).
        /// </summary>
        public static int ResolveAttack(SimUnit attacker, SimUnit target, CombatBuffTracker buffTracker, float multiplier = 1f)
        {
            int attackBonus = buffTracker.GetBuffAmount(attacker, attacker.AttackStat);
            int defenseBonus = buffTracker.GetBuffAmount(target, StatType.Endurance);
            int rawAttack = Mathf.RoundToInt((attacker.GetEffectiveAttackPower() + attackBonus) * multiplier);
            int defense = target.GetEffectiveStat(StatType.Endurance) + defenseBonus;
            int damage = DamageCalculator.Calculate(
                rawAttack, defense, attacker.AttackDamageType, target.Resistances);

            if (damage > 0 && Random.Range(0f, 1f) < CombatManager.CritChanceFor(attacker))
            {
                damage = Mathf.Max(damage + 1, Mathf.RoundToInt(damage * CombatManager.CritMultiplier));
            }

            if (damage < 0)
            {
                // Absorbed, clamped to the target's maximum — same rule as CombatManager.ExecuteAttack.
                int absorbed = Mathf.Min(-damage, Mathf.Max(0, target.Stats.MaxHealth - target.Stats.Health));
                target.Stats.Health += absorbed;
                return -absorbed;
            }

            target.Stats.Health -= damage;
            return damage;
        }

        // ---------------------------------------------------------------- helpers

        private static bool SkipsTurn(ICombatUnit unit, CombatBuffTracker buffTracker)
        {
            foreach (var statusEffect in buffTracker.GetActiveStatusEffects(unit))
            {
                var handler = BuffHandlerRegistry.Get(statusEffect);
                if (handler != null && handler.SkipsTurn)
                {
                    return true;
                }
            }
            return false;
        }

        private static bool AnyAlive(List<SimUnit> units)
        {
            foreach (var unit in units)
            {
                if (unit.IsAlive)
                {
                    return true;
                }
            }
            return false;
        }

        private static List<ICombatUnit> AliveAs(List<SimUnit> units)
        {
            var alive = new List<ICombatUnit>();
            foreach (var unit in units)
            {
                if (unit.IsAlive)
                {
                    alive.Add(unit);
                }
            }
            return alive;
        }

        private static List<ICombatUnit> AliveAsExcept(List<SimUnit> units, SimUnit exclude)
        {
            var alive = new List<ICombatUnit>();
            foreach (var unit in units)
            {
                if (unit.IsAlive && unit != exclude)
                {
                    alive.Add(unit);
                }
            }
            return alive;
        }

        private static SimUnit RandomAlive(List<SimUnit> units)
        {
            var alive = new List<SimUnit>();
            foreach (var unit in units)
            {
                if (unit.IsAlive)
                {
                    alive.Add(unit);
                }
            }
            if (alive.Count == 0)
            {
                return null;
            }
            return alive[Random.Range(0, alive.Count)];
        }

        private static SimUnit WeakestAlive(List<SimUnit> units)
        {
            SimUnit best = null;
            foreach (var unit in units)
            {
                if (!unit.IsAlive)
                {
                    continue;
                }
                if (best == null || unit.Stats.Health < best.Stats.Health)
                {
                    best = unit;
                }
            }
            return best;
        }

        private static SimUnit MostWounded(List<SimUnit> units)
        {
            SimUnit best = null;
            float bestFraction = 1f;
            foreach (var unit in units)
            {
                if (!unit.IsAlive || unit.Stats.MaxHealth <= 0)
                {
                    continue;
                }
                float fraction = HealthFraction(unit);
                if (fraction < bestFraction)
                {
                    bestFraction = fraction;
                    best = unit;
                }
            }
            return best;
        }

        private static float HealthFraction(SimUnit unit)
        {
            if (unit == null || unit.Stats == null || unit.Stats.MaxHealth <= 0)
            {
                return 1f;
            }
            return (float)unit.Stats.Health / unit.Stats.MaxHealth;
        }
    }
}
