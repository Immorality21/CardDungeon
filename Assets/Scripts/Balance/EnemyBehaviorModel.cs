using System.Collections.Generic;
using Assets.Scripts.Enemies;
using Assets.Scripts.Enemies.Behaviors;
using UnityEngine;
using UnityEngine.Serialization;

namespace Assets.Scripts.Balance
{
    /// <summary>What one authored behaviour is worth per turn, in multiples of a basic hit.</summary>
    public class BehaviorProfile
    {
        /// <summary>
        /// Expected damage per turn as a multiple of one ordinary swing. This is what
        /// <c>AverageOffenseMultiplier</c> used to return as a hand-tuned constant per archetype.
        /// </summary>
        public float OffenseMultiplier = 1f;

        /// <summary>Expected healing the enemy side gets back per turn of this enemy's.</summary>
        public float HealingPerTurn;

        /// <summary>Share of turns spent casting, for reporting and for the cast-frequency check.</summary>
        public float CastShare;

        /// <summary>Share of turns that land no damage at all — wind-ups, heals, debuffs.</summary>
        public float IdleShare;

        /// <summary>Number of authored actions that could ever be chosen.</summary>
        public int LiveActionCount;
    }

    /// <summary>
    /// Prices an <see cref="EnemyBehaviorSO"/> in closed form, so the danger index and the attrition
    /// curve read the repertoire an enemy actually has.
    ///
    /// <para>This replaced <c>BalanceMath.AverageOffenseMultiplier</c>'s switch over
    /// <see cref="EnemyArchetype"/>, which returned one hand-tuned float per archetype (0.5 for a
    /// Healer, 0.85 for a Debuffer, a computed cycle for Bruiser and Boss). Those numbers were fine
    /// while an archetype *was* a behaviour; now that behaviours are authored lists, a constant
    /// cannot see them.</para>
    ///
    /// <para><b>The occupancy assumptions are the honest part.</b> Some conditions depend on fight
    /// state a closed form does not track — whether an ally is wounded, whether a hero still lacks a
    /// debuff. Each gets one documented long-run share below. They are deliberately the same shape as
    /// the constants they replace, so a behaviour authored to match an old archetype prices close to
    /// the old number; the difference is that a behaviour authored to do something *else* now prices
    /// differently instead of silently reading as its archetype.</para>
    /// </summary>
    public static class EnemyBehaviorModel
    {
        /// <summary>
        /// Share of turns a wounded ally is available to heal.
        ///
        /// <para>Set to reproduce the <b>0.5</b> offense constant the old Healer archetype returned,
        /// so migrating a healer onto authored actions does not move its danger. It is an assumption,
        /// not a fact - a closed form cannot know how hurt the monsters are - and it is here to be
        /// tuned rather than trusted.</para>
        /// </summary>
        public const float AllyWoundedOccupancy = 0.5f;

        /// <summary>
        /// Share of turns some hero still lacks the debuff. Set to reproduce the old Debuffer
        /// constant of <b>0.85</b> offense: it debuffs 15% of turns and swings the rest.
        /// </summary>
        public const float DebuffOpenOccupancy = 0.15f;

        /// <summary>
        /// Share of the fight spent below an enrage threshold. Bosses die from full health, so most
        /// turns are above it.
        ///
        /// <para>Unlike the two above this is <i>not</i> tuned to match the old model, which ignored
        /// enrage entirely - the equivalent of 0. Pricing it costs the boss preset about +4% offense,
        /// and it is worth having because an authored behaviour can key anything off low health now,
        /// not just a boss's cadence.</para>
        /// </summary>
        public const float LowHealthOccupancy = 0.25f;

        /// <summary>
        /// Prices a behaviour. <paramref name="castMultiplier"/> is the expected damage of one cast
        /// expressed in the same multiples-of-a-swing currency, so casting folds into the one number
        /// instead of being blended on afterwards.
        /// </summary>
        public static BehaviorProfile Profile(
            EnemyBehaviorSO behavior, int partySize, float castMultiplier, float basicHit)
        {
            var profile = new BehaviorProfile();
            int party = Mathf.Max(1, partySize);

            var actions = behavior != null ? behavior.Actions : null;
            if (actions == null || actions.Count == 0)
            {
                return profile;
            }

            // Availability per entry: its gate times the share of turns its conditions hold.
            var available = new float[actions.Count];
            for (int i = 0; i < actions.Count; i++)
            {
                var entry = actions[i];
                if (entry == null)
                {
                    continue;
                }
                float gate = entry.ChanceGate > 0f ? entry.ChanceGate : 1f;
                available[i] = gate * Occupancy(entry);
            }

            // Selection is a priority cascade: the top tier takes the turn whenever *any* of its
            // entries is available, and what it leaves passes down. Independence across entries is an
            // approximation - two entries gated on opposite sides of a health threshold are really
            // exclusive - and it is why this is a model rather than a simulation.
            var claims = new float[actions.Count];
            float remaining = 1f;

            foreach (int priority in DescendingPriorities(actions))
            {
                if (remaining <= 0f)
                {
                    break;
                }

                float noneAvailable = 1f;
                float weighted = 0f;
                for (int i = 0; i < actions.Count; i++)
                {
                    if (actions[i] == null || actions[i].Priority != priority || available[i] <= 0f)
                    {
                        continue;
                    }
                    noneAvailable *= 1f - Mathf.Clamp01(available[i]);
                    weighted += WeightOf(actions[i]) * available[i];
                }

                float tierShare = remaining * (1f - noneAvailable);
                if (tierShare <= 0f || weighted <= 0f)
                {
                    continue;
                }

                for (int i = 0; i < actions.Count; i++)
                {
                    if (actions[i] == null || actions[i].Priority != priority || available[i] <= 0f)
                    {
                        continue;
                    }
                    claims[i] += tierShare * (WeightOf(actions[i]) * available[i] / weighted);
                }

                remaining -= tierShare;
            }

            // Anything unclaimed is the planner's fallback swing.
            float fallback = Mathf.Max(0f, remaining);

            // A telegraphed action costs *two* turns for one payload: the wind-up and the delivery. So
            // a decision turn is worth more than one turn of the clock, and everything below is
            // divided by that. Getting this wrong is what once priced a boss at a quarter of its real
            // output - the delivery turn was also being counted as free for an ordinary swing.
            float turnsPerDecision = 1f;
            for (int i = 0; i < actions.Count; i++)
            {
                if (actions[i] != null && actions[i].IsTelegraphed)
                {
                    turnsPerDecision += claims[i];
                }
            }

            float damage = fallback;   // the fallback is one plain hit
            for (int i = 0; i < actions.Count; i++)
            {
                var entry = actions[i];
                if (entry == null || claims[i] <= 0f)
                {
                    continue;
                }

                profile.LiveActionCount++;
                float claim = claims[i];

                switch (entry.Kind)
                {
                    case EnemyActionKind.Attack:
                        damage += claim * entry.Multiplier;
                        break;

                    case EnemyActionKind.HeavyAttack:
                    case EnemyActionKind.AoeAttack:
                    {
                        // An AoE lands on every hero.
                        float payload = entry.Multiplier
                                      * (entry.Kind == EnemyActionKind.AoeAttack ? party : 1f);
                        damage += claim * payload;
                        if (entry.IsTelegraphed)
                        {
                            profile.IdleShare += claim;   // the wind-up turn lands nothing
                        }
                        break;
                    }

                    case EnemyActionKind.Heal:
                        profile.HealingPerTurn += claim * entry.Power;
                        profile.IdleShare += claim;
                        break;

                    case EnemyActionKind.Debuff:
                        profile.IdleShare += claim;
                        break;

                    case EnemyActionKind.CastMagic:
                        damage += claim * castMultiplier;
                        profile.CastShare += claim;
                        if (castMultiplier <= 0f)
                        {
                            profile.IdleShare += claim;
                        }
                        break;
                }
            }

            profile.OffenseMultiplier = damage / turnsPerDecision;
            profile.HealingPerTurn /= turnsPerDecision;
            profile.CastShare /= turnsPerDecision;
            profile.IdleShare /= turnsPerDecision;
            return profile;
        }

        /// <summary>Weight, treating an unweighted entry as 1 - the planner treats a zero tier as uniform.</summary>
        private static float WeightOf(EnemyActionEntry entry)
        {
            float weight = Mathf.Max(0f, entry.Weight);
            return weight > 0f ? weight : 1f;
        }

        /// <summary>
        /// The fraction of turns an entry's conditions hold, from the documented occupancies. A
        /// condition kind with no case here contributes 1 — add its share when you add the kind.
        /// </summary>
        public static float Occupancy(EnemyActionEntry entry)
        {
            if (entry == null)
            {
                return 0f;
            }
            if (entry.Conditions == null || entry.Conditions.Count == 0)
            {
                return 1f;
            }

            float occupancy = 1f;
            foreach (var condition in entry.Conditions)
            {
                if (condition == null)
                {
                    continue;
                }
                switch (condition.Kind)
                {
                    case EnemyConditionKind.AllyWounded:
                        occupancy *= AllyWoundedOccupancy;
                        break;

                    case EnemyConditionKind.HeroMissingDebuff:
                        occupancy *= DebuffOpenOccupancy;
                        break;

                    case EnemyConditionKind.SelfHealthBelow:
                        occupancy *= LowHealthOccupancy;
                        break;

                    case EnemyConditionKind.SelfHealthAbove:
                        occupancy *= 1f - LowHealthOccupancy;
                        break;

                    case EnemyConditionKind.EveryNthTurn:
                    {
                        int n = Mathf.Max(1, Mathf.RoundToInt(condition.Value));
                        occupancy *= 1f / n;
                        break;
                    }

                    case EnemyConditionKind.NotFirstTurn:
                        // True for all but the opening turn — negligible over a fight, and treating it
                        // as 1 keeps the boss cadence arithmetic exactly 1-in-N.
                        break;
                }
            }

            return Mathf.Clamp01(occupancy);
        }

        private static IEnumerable<int> DescendingPriorities(IList<EnemyActionEntry> actions)
        {
            var seen = new List<int>();
            foreach (var entry in actions)
            {
                if (entry != null && !seen.Contains(entry.Priority))
                {
                    seen.Add(entry.Priority);
                }
            }
            seen.Sort();
            seen.Reverse();
            return seen;
        }
    }
}
