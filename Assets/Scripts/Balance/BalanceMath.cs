using System.Collections.Generic;
using Assets.Scripts.Combat;
using Assets.Scripts.Enemies;
using Assets.Scripts.Enemies.Behaviors;
using Assets.Scripts.Rooms;
using Assets.Scripts.UnitStats;
using UnityEngine;

namespace Assets.Scripts.Balance
{
    /// <summary>
    /// Closed-form balance metrics. Everything here is derived from the same primitives the live
    /// combat loop uses — <see cref="DamageCalculator"/> for damage, <see cref="TurnManager"/>'s tick
    /// constant for turn frequency, <see cref="CombatManager"/>'s crit constants — so the numbers on
    /// screen are the game's numbers rather than a parallel model of them.
    ///
    /// The model deliberately assumes <b>basic attacks only</b>, on both sides. That makes it a
    /// conservative lower bound on party output (magic hits harder) and keeps the metric stable while
    /// you retune stats. Magic, items and per-archetype decisions are covered by the simulator.
    /// </summary>
    public static class BalanceMath
    {
        /// <summary>
        /// Average damage multiplier once crit is folded in, at the base crit rate (zero Luck).
        /// Kept for callers that have no attacker to hand; prefer the overload.
        /// </summary>
        public static float ExpectedCritMultiplier()
        {
            return 1f + CombatManager.CritChance * (CombatManager.CritMultiplier - 1f);
        }

        /// <summary>
        /// Average damage multiplier for a specific attacker, so Luck shows up in every derived
        /// metric instead of only in play. Falls back to the base rate for a null attacker.
        /// </summary>
        public static float ExpectedCritMultiplier(ICombatUnit attacker)
        {
            return 1f + CombatManager.CritChanceFor(attacker) * (CombatManager.CritMultiplier - 1f);
        }

        /// <summary>
        /// Average damage one basic attack lands on a target, crit included. Uses the real
        /// <see cref="DamageCalculator"/> so resistance and the defense curve behave identically.
        /// </summary>
        public static float AverageDamage(int rawAttack, ICombatUnit target, DamageType type = DamageType.Normal,
            float multiplier = 1f, ICombatUnit attacker = null)
        {
            if (target == null)
            {
                return 0f;
            }

            int raw = Mathf.RoundToInt(rawAttack * multiplier);
            int flat = DamageCalculator.Calculate(raw, target.GetEffectiveStat(StatType.Endurance), type, target.Resistances);
            return flat * ExpectedCritMultiplier(attacker);
        }

        /// <summary>Average damage against a group, over a uniformly random living target.</summary>
        public static float AverageDamageAgainstGroup(int rawAttack, IList<SimUnit> targets, DamageType type = DamageType.Normal,
            float multiplier = 1f, ICombatUnit attacker = null)
        {
            if (targets == null || targets.Count == 0)
            {
                return 0f;
            }

            float total = 0f;
            int counted = 0;
            foreach (var target in targets)
            {
                if (target == null)
                {
                    continue;
                }
                total += AverageDamage(rawAttack, target, type, multiplier, attacker);
                counted++;
            }

            return counted > 0 ? total / counted : 0f;
        }

        /// <summary>Turns a unit gets per CTB tick: it acts once every BASE_TICKS/agility ticks.</summary>
        public static float TurnsPerTick(ICombatUnit unit)
        {
            if (unit == null)
            {
                return 0f;
            }
            float agility = Mathf.Max(1, unit.GetEffectiveStat(StatType.Agility));
            return agility / TurnManager.BASE_TICKS;
        }

        /// <summary>Whole hits needed to drop a health total at the given average damage.</summary>
        public static int HitsToKill(float averageDamage, int health)
        {
            if (averageDamage <= 0f)
            {
                return int.MaxValue;
            }
            return Mathf.Max(1, Mathf.CeilToInt(health / averageDamage));
        }

        /// <summary>Combined current health of a group.</summary>
        public static int HealthPool(IList<SimUnit> units)
        {
            int total = 0;
            if (units == null)
            {
                return total;
            }
            foreach (var unit in units)
            {
                if (unit != null && unit.Stats != null)
                {
                    total += unit.Stats.Health;
                }
            }
            return total;
        }

        /// <summary>Combined max health of a group.</summary>
        public static int MaxHealthPool(IList<SimUnit> units)
        {
            int total = 0;
            if (units == null)
            {
                return total;
            }
            foreach (var unit in units)
            {
                if (unit != null && unit.Stats != null)
                {
                    total += unit.Stats.MaxHealth;
                }
            }
            return total;
        }

        /// <summary>
        /// Average damage an archetype lands per turn, as a multiple of one plain hit. Charge turns
        /// deal nothing, heavies hit for their multiplier, and the boss signature strikes every hero
        /// — so a wider party makes the boss's average turn worth far more. Derived from the same
        /// constants the behaviors use, so retuning a behavior updates the metric.
        /// </summary>
        public static float AverageOffenseMultiplier(EnemyArchetype archetype, int partySize)
        {
            // Compatibility shim: an archetype no longer *is* a behaviour, so this prices the
            // built-in preset for it. Callers that have the real enemy should use the overload below,
            // which reads that enemy's authored actions.
            return AverageOffenseMultiplier(
                EnemyBehaviorSO.BuiltInPreset(archetype), partySize, 0f, 1f);
        }

        /// <summary>
        /// Expected damage per turn as a multiple of one ordinary swing, from the enemy's authored
        /// action list. Replaces the old switch over <see cref="EnemyArchetype"/> that returned one
        /// constant per archetype — see <see cref="EnemyBehaviorModel"/> for the occupancy
        /// assumptions behind the conditional entries.
        /// </summary>
        /// <param name="castMultiplier">
        /// Expected damage of one cast in the same multiples-of-a-swing currency, so casting folds
        /// into this one number instead of being blended on afterwards.
        /// </param>
        public static float AverageOffenseMultiplier(
            EnemyBehaviorSO behavior, int partySize, float castMultiplier, float basicHit)
        {
            var profile = EnemyBehaviorModel.Profile(behavior, partySize, castMultiplier, basicHit);
            return profile.OffenseMultiplier;
        }

        /// <summary>Damage a unit lands per tick against a group (its per-turn output x its turn rate).</summary>
        public static float DamagePerTick(SimUnit attacker, IList<SimUnit> targets, int opposingCount)
        {
            if (attacker == null || !attacker.IsAlive)
            {
                return 0f;
            }

            float basicHit = AverageDamageAgainstGroup(
                attacker.GetEffectiveAttackPower(), targets, attacker.AttackDamageType, 1f, attacker);

            if (attacker.IsHero)
            {
                return basicHit * TurnsPerTick(attacker);
            }

            // Everything the enemy can do - swings, telegraphed heavies, party-wide signatures, and
            // its own casts - is priced as one expectation over its authored actions. Casting used to
            // be blended on after this multiplier, in two places that had to be kept in step; it is
            // inside the multiplier now.
            float castMultiplier = 0f;
            if (basicHit > 0f)
            {
                var cast = EnemyMagicModel.Profile(attacker.Definition, attacker.Tuning, attacker, targets);
                castMultiplier = cast.ExpectedDamage / basicHit;
            }

            float multiplier = AverageOffenseMultiplier(
                attacker.Behavior, opposingCount, castMultiplier, basicHit);

            return basicHit * multiplier * TurnsPerTick(attacker);
        }

        /// <summary>Total damage per tick a whole side lands on the other.</summary>
        public static float GroupDamagePerTick(IList<SimUnit> attackers, IList<SimUnit> targets)
        {
            if (attackers == null || targets == null)
            {
                return 0f;
            }

            int opposing = Mathf.Max(1, targets.Count);
            float total = 0f;
            foreach (var attacker in attackers)
            {
                total += DamagePerTick(attacker, targets, opposing);
            }
            return total;
        }

        /// <summary>CTB ticks one side needs to grind the other's health pool to zero.</summary>
        public static float TicksToClear(IList<SimUnit> attackers, IList<SimUnit> targets)
        {
            float dps = GroupDamagePerTick(attackers, targets);
            if (dps <= 0f)
            {
                return float.PositiveInfinity;
            }
            return HealthPool(targets) / dps;
        }

        /// <summary>
        /// The headline "too strong / too weak" scalar: ticks the party needs to win, divided by ticks
        /// the enemies need to wipe the party. Below 1 the party wins with margin, 1 is mutual
        /// destruction, above 1 the fight is lost on paper. Agility-aware on both sides.
        /// </summary>
        public static float DangerIndex(IList<SimUnit> party, IList<SimUnit> enemies)
        {
            float partyNeeds = TicksToClear(party, enemies);
            float enemiesNeed = TicksToClear(enemies, party);

            if (float.IsInfinity(enemiesNeed))
            {
                return 0f;
            }
            if (float.IsInfinity(partyNeeds) || enemiesNeed <= 0f)
            {
                return float.PositiveInfinity;
            }

            return partyNeeds / enemiesNeed;
        }

        /// <summary>Party-turns the party needs to kill one enemy — the readable pacing number.</summary>
        public static float PartyTurnsToKill(IList<SimUnit> party, SimUnit enemy)
        {
            if (party == null || enemy == null)
            {
                return 0f;
            }

            var single = new List<SimUnit> { enemy };
            float ticks = TicksToClear(party, single);
            if (float.IsInfinity(ticks))
            {
                return float.PositiveInfinity;
            }

            float partyTurnsPerTick = 0f;
            foreach (var hero in party)
            {
                partyTurnsPerTick += TurnsPerTick(hero);
            }

            return partyTurnsPerTick > 0f ? ticks * partyTurnsPerTick : float.PositiveInfinity;
        }

        /// <summary>
        /// A single weighted stat budget, for spotting outliers inside a tier at a glance. The weights
        /// live in <see cref="BalanceRulesSO"/> because the right trade-off between a point of Attack
        /// and a point of HP is a design decision, not a fact.
        /// </summary>
        public static float PowerScore(EnemySO enemy, BalanceRulesSO rules)
        {
            return enemy != null ? PowerScore(enemy.BaseStats, rules) : 0f;
        }

        /// <summary>
        /// The weighted stat budget of an arbitrary block — the form the model wants now that an
        /// enemy's real stats come from the level it appears in rather than from its template.
        /// </summary>
        public static float PowerScore(StatBlock stats, BalanceRulesSO rules)
        {
            if (stats == null || rules == null)
            {
                return 0f;
            }

            float score = 0f;
            foreach (var stat in StatCatalog.Types)
            {
                score += stats[stat] * rules.WeightFor(stat);
            }
            return score;
        }

        /// <summary>Fraction of incoming damage the defense curve removes, for display.</summary>
        public static float EnduranceReduction(int defense)
        {
            float def = Mathf.Max(0, defense);
            return def / (def + DamageCalculator.EnduranceConstant);
        }

        /// <summary>Grades a value against a target band. Used by every table cell that shows a colour.</summary>
        public static BalanceSeverity Grade(float value, float min, float max, float criticalBelow, float criticalAbove)
        {
            if (value <= criticalBelow || value >= criticalAbove)
            {
                return BalanceSeverity.Critical;
            }
            if (value < min || value > max)
            {
                return BalanceSeverity.Warning;
            }
            return BalanceSeverity.Ok;
        }
    }
}
