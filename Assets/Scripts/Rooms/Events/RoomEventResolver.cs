using System.Collections.Generic;
using Assets.Scripts.Combat;
using Assets.Scripts.UnitStats;
using UnityEngine;

namespace Assets.Scripts.Rooms.Events
{
    /// <summary>How likely a check reads, in words rather than numbers.</summary>
    public enum OddsBand
    {
        NearHopeless = 0,
        SlightChance = 1,
        EvenOdds = 2,
        VeryLikely = 3,
        AlmostCertain = 4
    }

    /// <summary>
    /// How well the party can judge its own odds. The governing stat buys information as well as
    /// success: a high-Intelligence party reads the runes' difficulty accurately, a dull one only
    /// gets an impression, and one well under the difficulty is guessing.
    /// </summary>
    public enum OddsClarity
    {
        Unknown = 0,
        Vague = 1,
        Clear = 2
    }

    /// <summary>
    /// The maths behind a room event: how a stat becomes odds, how odds become words, and how a
    /// weighted outcome pool resolves. Deterministic - every roll is supplied by the caller - in the
    /// same spirit as <c>DamageCalculator</c> and <c>LootRoller</c>, so the whole decision layer is
    /// unit-testable without a running dungeon.
    /// </summary>
    public static class RoomEventResolver
    {
        /// <summary>Nothing is ever a certainty in either direction.</summary>
        public const float MinChance = 0.05f;

        public const float MaxChance = 0.95f;

        /// <summary>
        /// Diminishing returns on the governing stat: stat / (stat + difficulty), which is even odds
        /// when the party matches the difficulty and never reaches certainty. Deliberately the same
        /// curve shape as <c>CombatManager.CritChanceFor</c> - one idiom for "more of this stat
        /// helps, and keeps helping less".
        /// </summary>
        public static float SuccessChance(int statValue, int difficulty)
        {
            if (difficulty <= 0)
            {
                return MaxChance;
            }

            if (statValue <= 0)
            {
                return MinChance;
            }

            float raw = statValue / (float)(statValue + difficulty);
            return Mathf.Clamp(raw, MinChance, MaxChance);
        }

        /// <summary>Which qualitative band a chance falls in.</summary>
        public static OddsBand BandFor(float chance)
        {
            if (chance < 0.20f)
            {
                return OddsBand.NearHopeless;
            }

            if (chance < 0.40f)
            {
                return OddsBand.SlightChance;
            }

            if (chance < 0.60f)
            {
                return OddsBand.EvenOdds;
            }

            if (chance < 0.80f)
            {
                return OddsBand.VeryLikely;
            }

            return OddsBand.AlmostCertain;
        }

        /// <summary>
        /// How precisely the party can read those odds, from the same stat that resolves them:
        /// matching the difficulty reads it exactly, half of it reads it roughly, less than that
        /// cannot read it at all.
        /// </summary>
        public static OddsClarity ClarityFor(int statValue, int difficulty)
        {
            if (difficulty <= 0 || statValue >= difficulty)
            {
                return OddsClarity.Clear;
            }

            if (statValue * 2 >= difficulty)
            {
                return OddsClarity.Vague;
            }

            return OddsClarity.Unknown;
        }

        /// <summary>
        /// The odds line the player actually reads. Never a percentage: a band keeps the decision a
        /// judgement call while still paying visibly for stat investment.
        /// </summary>
        public static string DescribeOdds(OddsBand band, OddsClarity clarity)
        {
            switch (clarity)
            {
                case OddsClarity.Clear:
                    return ClearPhrase(band);
                case OddsClarity.Vague:
                    return VaguePhrase(band);
                default:
                    return "You have no idea what you are dealing with.";
            }
        }

        private static string ClearPhrase(OddsBand band)
        {
            switch (band)
            {
                case OddsBand.AlmostCertain:
                    return "You are almost certain to manage it.";
                case OddsBand.VeryLikely:
                    return "You are very likely to manage it.";
                case OddsBand.EvenOdds:
                    return "It is an even bet.";
                case OddsBand.SlightChance:
                    return "You have only a slight chance.";
                default:
                    return "It looks near hopeless.";
            }
        }

        private static string VaguePhrase(OddsBand band)
        {
            switch (band)
            {
                case OddsBand.AlmostCertain:
                case OddsBand.VeryLikely:
                    return "It looks safe enough, though you could not say how safe.";
                case OddsBand.EvenOdds:
                    return "It looks risky, though you could not say how risky.";
                default:
                    return "It looks dangerous, though you could not say how dangerous.";
            }
        }

        /// <summary>Whether the check passes, given a caller-supplied roll in [0,1).</summary>
        public static bool Passes(float chance, float roll)
        {
            return roll < chance;
        }

        /// <summary>
        /// Weighted pick from an outcome pool, using a caller-supplied roll in [0,1). Returns -1 for
        /// an empty pool. Non-positive weights are treated as zero, and a pool whose weights are all
        /// non-positive falls back to its first entry rather than dropping the outcome silently - an
        /// unweighted pool is an authoring slip, not a reason for nothing to happen.
        ///
        /// <para><paramref name="actor"/> - the hero the check was resolved against - bends the
        /// weights through each outcome's <see cref="RoomEventOutcome.WeightModifierStat"/> and rate.
        /// Whether the check passes is the event's <c>GoverningStat</c>'s business; this decides
        /// <i>how it goes</i>: the clean success rather than the one that also costs you, the glancing
        /// failure rather than the one that wakes something. Null actor = authored weights.</para>
        /// </summary>
        public static int PickOutcomeIndex(IReadOnlyList<RoomEventOutcome> pool, float roll,
            ICombatUnit actor = null)
        {
            if (pool == null || pool.Count == 0)
            {
                return -1;
            }

            float total = 0f;
            for (int i = 0; i < pool.Count; i++)
            {
                total += WeightOf(pool[i], actor);
            }

            if (total <= 0f)
            {
                return 0;
            }

            float target = Mathf.Clamp01(roll) * total;
            float running = 0f;
            for (int i = 0; i < pool.Count; i++)
            {
                running += WeightOf(pool[i], actor);
                if (target < running)
                {
                    return i;
                }
            }

            return pool.Count - 1;
        }

        /// <summary>
        /// An outcome's effective weight: <c>Weight * (1 + stat * rate / 100)</c>, floored at 0.
        /// Relative to the authored weight, so a modifier tilts a pool without rewriting it - and a
        /// steep negative rate can take an outcome off the table entirely for a hero with enough of
        /// the stat, which is the point of authoring one.
        /// </summary>
        private static float WeightOf(RoomEventOutcome outcome, ICombatUnit actor)
        {
            if (outcome == null)
            {
                return 0f;
            }

            float weight = Mathf.Max(0, outcome.Weight);
            if (weight <= 0f
                || actor == null
                || outcome.WeightModifierStat == StatType.None
                || Mathf.Approximately(outcome.WeightModifierRate, 0f))
            {
                return weight;
            }

            int stat = actor.GetEffectiveStat(outcome.WeightModifierStat);
            if (stat <= 0)
            {
                return weight;
            }

            return Mathf.Max(0f, weight * (1f + stat * outcome.WeightModifierRate / 100f));
        }

        /// <summary>
        /// The hero a check is resolved against: the party's <b>best</b> at the governing stat.
        /// Party-best rather than the leader or the party sum, because it is the one rule that makes
        /// a specialist worth a slot - a single high-Intelligence hero is why the party can read the
        /// runes at all. Ties go to party order; the downed are skipped, since they are in no state
        /// to try.
        /// </summary>
        public static ICombatUnit BestFor(IReadOnlyList<ICombatUnit> party, StatType stat)
        {
            if (party == null || stat == StatType.None)
            {
                return null;
            }

            ICombatUnit best = null;
            int bestValue = int.MinValue;

            for (int i = 0; i < party.Count; i++)
            {
                var unit = party[i];
                if (unit == null || !unit.IsAlive)
                {
                    continue;
                }

                int value = unit.GetEffectiveStat(stat);
                if (value > bestValue)
                {
                    bestValue = value;
                    best = unit;
                }
            }

            return best;
        }
    }
}
