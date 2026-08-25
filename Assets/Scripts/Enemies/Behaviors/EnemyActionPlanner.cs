using System.Collections.Generic;
using Assets.Scripts.Cards;
using Assets.Scripts.Combat;
using Assets.Scripts.UnitStats;
using UnityEngine;

namespace Assets.Scripts.Enemies.Behaviors
{
    /// <summary>Rolls an <see cref="EnemyActionPlanner"/> needs, injected so the planner stays pure.</summary>
    public struct EnemyPlanRolls
    {
        /// <summary>Gate roll per entry, in list order. Falls back to <see cref="Fallback"/> when short.</summary>
        public IList<float> Gates;

        /// <summary>Weighted pick within the winning priority tier.</summary>
        public float Tier;

        /// <summary>Target selection (random hero, which magic).</summary>
        public float Target;

        /// <summary>Which magic to cast, when the entry draws from the Draw list.</summary>
        public float Magic;

        public float Fallback;

        public float GateAt(int index)
        {
            if (Gates != null && index >= 0 && index < Gates.Count)
            {
                return Gates[index];
            }
            return Fallback;
        }

        /// <summary>Rolls drawn from Unity's RNG — what the live combat loop and the simulator use.</summary>
        public static EnemyPlanRolls Random(int entryCount)
        {
            var gates = new float[Mathf.Max(0, entryCount)];
            for (int i = 0; i < gates.Length; i++)
            {
                gates[i] = UnityEngine.Random.Range(0f, 1f);
            }
            return new EnemyPlanRolls
            {
                Gates = gates,
                Tier = UnityEngine.Random.Range(0f, 1f),
                Target = UnityEngine.Random.Range(0f, 1f),
                Magic = UnityEngine.Random.Range(0f, 1f),
                Fallback = UnityEngine.Random.Range(0f, 1f)
            };
        }
    }

    /// <summary>
    /// Turns an <see cref="EnemyBehaviorSO"/> into the action an enemy takes this turn. Pure and
    /// roll-injected, so the combat loop, the headless simulator and the tests all decide identically.
    ///
    /// <para><b>The order is: deliver, gate, priority, weight.</b></para>
    /// <list type="number">
    /// <item><description>If the enemy is mid-telegraph it delivers that action, full stop. It has
    /// already shown the player a wind-up and swallowing it would make the telegraph a lie.</description></item>
    /// <item><description>Each entry is eligible only if every condition holds, its
    /// <see cref="EnemyActionEntry.ChanceGate"/> roll passes, and the action has somewhere to
    /// land.</description></item>
    /// <item><description>The highest eligible <see cref="EnemyActionEntry.Priority"/> wins the
    /// turn outright.</description></item>
    /// <item><description><see cref="EnemyActionEntry.Weight"/> picks between entries tied at that
    /// priority.</description></item>
    /// </list>
    ///
    /// <para>With nothing eligible it swings, so a half-authored behaviour never produces a wasted
    /// turn.</para>
    /// </summary>
    public static class EnemyActionPlanner
    {
        /// <summary>Sentinel for <see cref="EnemyCombatContext.ChargingEntryIndex"/>: not mid-telegraph.</summary>
        public const int NoCharge = -1;

        public static EnemyDecision Plan(
            ICombatUnit self,
            EnemyCombatContext context,
            EnemyBehaviorSO behavior,
            EnemyPlanRolls rolls)
        {
            var actions = behavior != null ? behavior.Actions : null;

            // 1. Deliver a telegraphed action already in flight.
            if (context.ChargingEntryIndex >= 0 && actions != null
                && context.ChargingEntryIndex < actions.Count)
            {
                var pending = actions[context.ChargingEntryIndex];
                if (pending != null && pending.CanTelegraph)
                {
                    return Deliver(pending, self, context, rolls);
                }
            }

            if (actions == null || actions.Count == 0)
            {
                return Swing(self, context, rolls, 1f);
            }

            // 2 + 3. Eligible entries, highest priority tier only.
            int bestPriority = int.MinValue;
            for (int i = 0; i < actions.Count; i++)
            {
                if (!IsEligible(actions[i], i, self, context, rolls))
                {
                    continue;
                }
                if (actions[i].Priority > bestPriority)
                {
                    bestPriority = actions[i].Priority;
                }
            }

            if (bestPriority == int.MinValue)
            {
                return Swing(self, context, rolls, 1f);
            }

            // 4. Weighted pick inside the tier.
            float total = 0f;
            for (int i = 0; i < actions.Count; i++)
            {
                if (actions[i].Priority == bestPriority && IsEligible(actions[i], i, self, context, rolls))
                {
                    total += Mathf.Max(0f, actions[i].Weight);
                }
            }

            int chosenIndex = -1;
            if (total <= 0f)
            {
                // Every entry in the tier is weighted 0 — treat as uniform rather than as "do nothing".
                var tied = new List<int>();
                for (int i = 0; i < actions.Count; i++)
                {
                    if (actions[i].Priority == bestPriority && IsEligible(actions[i], i, self, context, rolls))
                    {
                        tied.Add(i);
                    }
                }
                if (tied.Count > 0)
                {
                    int pick = Mathf.Clamp(Mathf.FloorToInt(Mathf.Clamp01(rolls.Tier) * tied.Count), 0, tied.Count - 1);
                    chosenIndex = tied[pick];
                }
            }
            else
            {
                float target = Mathf.Clamp01(rolls.Tier) * total;
                float running = 0f;
                for (int i = 0; i < actions.Count; i++)
                {
                    if (actions[i].Priority != bestPriority || !IsEligible(actions[i], i, self, context, rolls))
                    {
                        continue;
                    }
                    chosenIndex = i;
                    running += Mathf.Max(0f, actions[i].Weight);
                    if (target < running)
                    {
                        break;
                    }
                }
            }

            if (chosenIndex < 0)
            {
                return Swing(self, context, rolls, 1f);
            }

            return Begin(actions[chosenIndex], chosenIndex, self, context, rolls);
        }

        /// <summary>
        /// The action this enemy will take, but <b>only when it is already determined</b> — null
        /// means "not knowable yet". Drives the intent icon on the enemy's HP bar.
        ///
        /// <para>Behaviours used to be deterministic, so a preview was simply the decision. With
        /// authored gates and weights it usually is not, and an intent icon that guesses wrong is
        /// worse than no icon: it teaches the player to distrust the telegraph. So this reports three
        /// cases only — a telegraph already in flight (certain by construction, which is the whole
        /// point of a telegraph), a single ungated action that is the only thing it can do, and
        /// otherwise nothing.</para>
        /// </summary>
        public static EnemyActionType? PredictCertain(
            ICombatUnit self, EnemyCombatContext context, EnemyBehaviorSO behavior)
        {
            var actions = behavior != null ? behavior.Actions : null;

            if (context.ChargingEntryIndex >= 0 && actions != null
                && context.ChargingEntryIndex < actions.Count)
            {
                var pending = actions[context.ChargingEntryIndex];
                if (pending != null && pending.CanTelegraph)
                {
                    return pending.Kind == EnemyActionKind.AoeAttack
                        ? EnemyActionType.AoeAttack
                        : EnemyActionType.HeavyAttack;
                }
            }

            if (actions == null || actions.Count == 0)
            {
                return EnemyActionType.Attack;
            }

            // Any live gated entry means the turn could go either way.
            int bestPriority = int.MinValue;
            int candidate = -1;
            int candidateCount = 0;

            for (int i = 0; i < actions.Count; i++)
            {
                var entry = actions[i];
                if (entry == null || !ConditionsHold(entry, self, context)
                    || !HasSomewhereToLand(entry, self, context))
                {
                    continue;
                }

                if (entry.ChanceGate > 0f)
                {
                    return null;
                }

                if (entry.Priority > bestPriority)
                {
                    bestPriority = entry.Priority;
                    candidate = i;
                    candidateCount = 1;
                }
                else if (entry.Priority == bestPriority)
                {
                    candidateCount++;
                }
            }

            if (candidate < 0)
            {
                return EnemyActionType.Attack;
            }
            if (candidateCount > 1)
            {
                return null;
            }

            var chosen = actions[candidate];
            if (chosen.IsTelegraphed)
            {
                return chosen.Kind == EnemyActionKind.AoeAttack
                    ? EnemyActionType.ChargeAoe
                    : EnemyActionType.ChargeHeavy;
            }

            switch (chosen.Kind)
            {
                case EnemyActionKind.HeavyAttack:
                    return EnemyActionType.HeavyAttack;
                case EnemyActionKind.AoeAttack:
                    return EnemyActionType.AoeAttack;
                case EnemyActionKind.Heal:
                    return EnemyActionType.Heal;
                case EnemyActionKind.Debuff:
                    return EnemyActionType.Debuff;
                case EnemyActionKind.CastMagic:
                    return EnemyActionType.CastMagic;
                default:
                    return EnemyActionType.Attack;
            }
        }

        private static bool ConditionsHold(
            EnemyActionEntry entry, ICombatUnit self, EnemyCombatContext context)
        {
            if (entry.Conditions == null)
            {
                return true;
            }
            foreach (var condition in entry.Conditions)
            {
                if (!Holds(condition, self, context))
                {
                    return false;
                }
            }
            return true;
        }

        // ------------------------------------------------------------------ eligibility

        /// <summary>
        /// Whether an entry can be taken this turn: its conditions, its gate roll, and whether the
        /// action has anywhere to land. The last part matters — a Heal with nobody wounded or a
        /// CastMagic with an empty Draw list must not win a turn and then do nothing.
        /// </summary>
        public static bool IsEligible(
            EnemyActionEntry entry, int index, ICombatUnit self, EnemyCombatContext context, EnemyPlanRolls rolls)
        {
            if (entry == null)
            {
                return false;
            }

            if (entry.ChanceGate > 0f && rolls.GateAt(index) >= entry.ChanceGate)
            {
                return false;
            }

            if (entry.Conditions != null)
            {
                foreach (var condition in entry.Conditions)
                {
                    if (!Holds(condition, self, context))
                    {
                        return false;
                    }
                }
            }

            return HasSomewhereToLand(entry, self, context);
        }

        /// <summary>Whether one condition holds. Every kind here must be priceable by <c>EnemyBehaviorModel</c>.</summary>
        public static bool Holds(EnemyActionCondition condition, ICombatUnit self, EnemyCombatContext context)
        {
            if (condition == null)
            {
                return true;
            }

            switch (condition.Kind)
            {
                case EnemyConditionKind.SelfHealthBelow:
                    return HealthFraction(self) <= condition.Value;

                case EnemyConditionKind.SelfHealthAbove:
                    return HealthFraction(self) > condition.Value;

                case EnemyConditionKind.AllyWounded:
                    return MostWoundedAlly(self, context) != null;

                case EnemyConditionKind.HeroMissingDebuff:
                    return EnemyTargeting.FirstWithoutDebuff(
                        context.Heroes, context.BuffTracker, condition.Stat) != null;

                case EnemyConditionKind.EveryNthTurn:
                {
                    int n = Mathf.Max(1, Mathf.RoundToInt(condition.Value));
                    return context.SelfTurnCount % n == 0;
                }

                case EnemyConditionKind.NotFirstTurn:
                    return context.SelfTurnCount > 0;

                default:
                    return true;
            }
        }

        private static bool HasSomewhereToLand(
            EnemyActionEntry entry, ICombatUnit self, EnemyCombatContext context)
        {
            switch (entry.Kind)
            {
                case EnemyActionKind.Heal:
                    return MostWoundedAlly(self, context) != null;

                case EnemyActionKind.Debuff:
                    return EnemyTargeting.FirstWithoutDebuff(
                        context.Heroes, context.BuffTracker, entry.TargetStat) != null;

                case EnemyActionKind.CastMagic:
                    return entry.Magic != null
                        ? entry.Magic.Effects != null && entry.Magic.Effects.Count > 0
                        : EnemyMagicPlan.HasCastable(context.DrawableMagics);

                default:
                    return HasLivingHero(context);
            }
        }

        // ------------------------------------------------------------------ building the decision

        private static EnemyDecision Begin(
            EnemyActionEntry entry, int index, ICombatUnit self, EnemyCombatContext context, EnemyPlanRolls rolls)
        {
            // A telegraphed action spends this turn winding up; the payload lands next turn.
            if (entry.IsTelegraphed)
            {
                return new EnemyDecision
                {
                    Type = entry.Kind == EnemyActionKind.AoeAttack
                        ? EnemyActionType.ChargeAoe
                        : EnemyActionType.ChargeHeavy,
                    Target = entry.Kind == EnemyActionKind.AoeAttack
                        ? null
                        : EnemyTargeting.PickRandom(context.Heroes),
                    Multiplier = entry.Multiplier,
                    EntryIndex = index
                };
            }

            return Deliver(entry, self, context, rolls, index);
        }

        private static EnemyDecision Deliver(
            EnemyActionEntry entry, ICombatUnit self, EnemyCombatContext context, EnemyPlanRolls rolls, int index = -1)
        {
            switch (entry.Kind)
            {
                case EnemyActionKind.HeavyAttack:
                    return new EnemyDecision
                    {
                        Type = EnemyActionType.HeavyAttack,
                        Multiplier = entry.Multiplier,
                        Target = EnemyTargeting.PickRandom(context.Heroes),
                        EntryIndex = index
                    };

                case EnemyActionKind.AoeAttack:
                    return new EnemyDecision
                    {
                        Type = EnemyActionType.AoeAttack,
                        Multiplier = entry.Multiplier,
                        EntryIndex = index
                    };

                case EnemyActionKind.Heal:
                    return new EnemyDecision
                    {
                        Type = EnemyActionType.Heal,
                        Target = MostWoundedAlly(self, context),
                        Amount = entry.Power,
                        EntryIndex = index
                    };

                case EnemyActionKind.Debuff:
                    return new EnemyDecision
                    {
                        Type = EnemyActionType.Debuff,
                        Target = EnemyTargeting.FirstWithoutDebuff(
                            context.Heroes, context.BuffTracker, entry.TargetStat),
                        Amount = entry.Power,
                        Duration = entry.Duration,
                        DebuffStat = entry.TargetStat,
                        EntryIndex = index
                    };

                case EnemyActionKind.CastMagic:
                {
                    var magic = entry.Magic != null
                        ? entry.Magic
                        : EnemyMagicPlan.Select(context.DrawableMagics, rolls.Magic);
                    if (magic == null)
                    {
                        return Swing(self, context, rolls, 1f);
                    }

                    var targets = EnemyMagicPlan.ResolveTargets(
                        magic, self, context.Heroes, context.Allies, rolls.Target);
                    if (targets.Count == 0)
                    {
                        return Swing(self, context, rolls, 1f);
                    }

                    return new EnemyDecision
                    {
                        Type = EnemyActionType.CastMagic,
                        Magic = magic,
                        MagicTargets = targets,
                        Target = targets[0],
                        EntryIndex = index
                    };
                }

                default:
                    return Swing(self, context, rolls, entry.Multiplier, index);
            }
        }

        private static EnemyDecision Swing(
            ICombatUnit self, EnemyCombatContext context, EnemyPlanRolls rolls, float multiplier, int index = -1)
        {
            return new EnemyDecision
            {
                Type = EnemyActionType.Attack,
                Target = EnemyTargeting.PickRandom(context.Heroes),
                Multiplier = multiplier,
                EntryIndex = index
            };
        }

        // ------------------------------------------------------------------ helpers

        private static float HealthFraction(ICombatUnit unit)
        {
            if (unit == null || unit.Stats == null)
            {
                return 1f;
            }
            int max = unit.GetEffectiveStat(StatType.MaxHealth);
            if (max <= 0)
            {
                return 1f;
            }
            return (float)unit.Stats.Health / max;
        }

        /// <summary>
        /// The most wounded of this enemy and its allies, or null when every one of them is at full
        /// health. Includes self, matching the old <c>HealerBehavior</c>, which could mend itself.
        /// </summary>
        public static ICombatUnit MostWoundedAlly(ICombatUnit self, EnemyCombatContext context)
        {
            var candidates = new List<ICombatUnit>();
            if (context.Allies != null)
            {
                candidates.AddRange(context.Allies);
            }
            if (self != null)
            {
                candidates.Add(self);
            }
            return EnemyTargeting.MostWounded(candidates);
        }

        private static bool HasLivingHero(EnemyCombatContext context)
        {
            if (context.Heroes == null)
            {
                return false;
            }
            foreach (var hero in context.Heroes)
            {
                if (hero != null && hero.IsAlive)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
