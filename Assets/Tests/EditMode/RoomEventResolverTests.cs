using System.Collections.Generic;
using Assets.Scripts.Combat;
using Assets.Scripts.Rooms.Events;
using Assets.Scripts.UnitStats;
using NUnit.Framework;

namespace Tests.EditMode
{
    /// <summary>
    /// The decision layer of room events: how a stat becomes odds, how odds become words, and how a
    /// weighted outcome pool resolves. All of it deterministic, so the part of the feature the player
    /// actually reasons about is testable without a running dungeon.
    /// </summary>
    public class RoomEventResolverTests
    {
        [Test]
        public void SuccessChance_StatMatchesDifficulty_IsEvenOdds()
        {
            Assert.AreEqual(0.5f, RoomEventResolver.SuccessChance(10, 10), 0.001f,
                "Difficulty is defined as the stat value at which the check is an even bet.");
        }

        [Test]
        public void SuccessChance_RisesWithStatButNeverReachesCertainty()
        {
            float low = RoomEventResolver.SuccessChance(5, 10);
            float mid = RoomEventResolver.SuccessChance(10, 10);
            float high = RoomEventResolver.SuccessChance(30, 10);

            Assert.Less(low, mid);
            Assert.Less(mid, high);
            Assert.LessOrEqual(high, RoomEventResolver.MaxChance,
                "Diminishing returns are the point: no amount of investment makes a gamble a formality.");
        }

        [Test]
        public void SuccessChance_ZeroStat_IsFloorNotZero()
        {
            Assert.AreEqual(RoomEventResolver.MinChance, RoomEventResolver.SuccessChance(0, 10), 0.001f,
                "A hopeless party can still get lucky, or the option may as well be hidden.");
        }

        [Test]
        public void SuccessChance_ZeroDifficulty_IsCeiling()
        {
            Assert.AreEqual(RoomEventResolver.MaxChance, RoomEventResolver.SuccessChance(0, 0), 0.001f,
                "An unauthored difficulty must not read as impossible for everyone.");
        }

        [Test]
        public void BandFor_CoversTheWholeRange()
        {
            Assert.AreEqual(OddsBand.NearHopeless, RoomEventResolver.BandFor(0.05f));
            Assert.AreEqual(OddsBand.SlightChance, RoomEventResolver.BandFor(0.30f));
            Assert.AreEqual(OddsBand.EvenOdds, RoomEventResolver.BandFor(0.50f));
            Assert.AreEqual(OddsBand.VeryLikely, RoomEventResolver.BandFor(0.70f));
            Assert.AreEqual(OddsBand.AlmostCertain, RoomEventResolver.BandFor(0.95f));
        }

        [Test]
        public void ClarityFor_StatBuysInformationAsWellAsOdds()
        {
            Assert.AreEqual(OddsClarity.Clear, RoomEventResolver.ClarityFor(10, 10),
                "A party that matches the difficulty reads it exactly.");
            Assert.AreEqual(OddsClarity.Vague, RoomEventResolver.ClarityFor(5, 10),
                "Half the difficulty gets an impression.");
            Assert.AreEqual(OddsClarity.Unknown, RoomEventResolver.ClarityFor(2, 10),
                "Well under it, the party is guessing - which is the information the stat buys.");
        }

        [Test]
        public void DescribeOdds_NeverStatesANumber()
        {
            foreach (OddsBand band in System.Enum.GetValues(typeof(OddsBand)))
            {
                foreach (OddsClarity clarity in System.Enum.GetValues(typeof(OddsClarity)))
                {
                    string text = RoomEventResolver.DescribeOdds(band, clarity);

                    Assert.IsNotEmpty(text);
                    Assert.IsFalse(text.Contains("%"),
                        "Raw percentages turn the decision into arithmetic; the band is the whole point.");
                }
            }
        }

        [Test]
        public void DescribeOdds_UnknownClarity_HidesTheBand()
        {
            string hopeless = RoomEventResolver.DescribeOdds(OddsBand.NearHopeless, OddsClarity.Unknown);
            string certain = RoomEventResolver.DescribeOdds(OddsBand.AlmostCertain, OddsClarity.Unknown);

            Assert.AreEqual(hopeless, certain,
                "A party that cannot read the risk must not learn it from the wording.");
        }

        [Test]
        public void Passes_UsesTheSuppliedRoll()
        {
            Assert.IsTrue(RoomEventResolver.Passes(0.5f, 0.49f));
            Assert.IsFalse(RoomEventResolver.Passes(0.5f, 0.5f));
        }

        [Test]
        public void PickOutcomeIndex_EmptyPool_ReturnsMinusOne()
        {
            Assert.AreEqual(-1, RoomEventResolver.PickOutcomeIndex(new List<RoomEventOutcome>(), 0.5f));
            Assert.AreEqual(-1, RoomEventResolver.PickOutcomeIndex(null, 0.5f));
        }

        [Test]
        public void PickOutcomeIndex_RespectsWeights()
        {
            var pool = new List<RoomEventOutcome>
            {
                new RoomEventOutcome { Weight = 3 },
                new RoomEventOutcome { Weight = 1 }
            };

            Assert.AreEqual(0, RoomEventResolver.PickOutcomeIndex(pool, 0.00f));
            Assert.AreEqual(0, RoomEventResolver.PickOutcomeIndex(pool, 0.74f));
            Assert.AreEqual(1, RoomEventResolver.PickOutcomeIndex(pool, 0.76f));
        }

        [Test]
        public void PickOutcomeIndex_ZeroWeightIsNeverPicked()
        {
            var pool = new List<RoomEventOutcome>
            {
                new RoomEventOutcome { Weight = 0 },
                new RoomEventOutcome { Weight = 1 }
            };

            Assert.AreEqual(1, RoomEventResolver.PickOutcomeIndex(pool, 0.0f));
            Assert.AreEqual(1, RoomEventResolver.PickOutcomeIndex(pool, 0.99f));
        }

        [Test]
        public void PickOutcomeIndex_AllWeightsZero_FallsBackToFirst()
        {
            var pool = new List<RoomEventOutcome>
            {
                new RoomEventOutcome { Weight = 0 },
                new RoomEventOutcome { Weight = 0 }
            };

            Assert.AreEqual(0, RoomEventResolver.PickOutcomeIndex(pool, 0.5f),
                "An unweighted pool is an authoring slip, not a reason for nothing to happen.");
        }

        [Test]
        public void BestFor_PicksThePartySpecialistNotTheLeader()
        {
            var leader = new MockCombatUnit("Warrior", 10, 5, 13);
            var caster = new MockCombatUnit("Acolyte", 3, 3, 10);
            caster.Stats[StatType.Intelligence] = 10;

            var best = RoomEventResolver.BestFor(new List<ICombatUnit> { leader, caster }, StatType.Intelligence);

            Assert.AreSame(caster, best,
                "Party-best is what makes bringing a specialist worth a slot.");
        }

        [Test]
        public void BestFor_ReadsEffectiveStatsSoGearCounts()
        {
            var plain = new MockCombatUnit("Warrior", 10, 5, 13);
            plain.Stats[StatType.Luck] = 8;
            var geared = new MockCombatUnit("Scout", 6, 4, 11);
            geared.Stats[StatType.Luck] = 4;
            geared.EffectiveOverrides[StatType.Luck] = 12;

            var best = RoomEventResolver.BestFor(new List<ICombatUnit> { plain, geared }, StatType.Luck);

            Assert.AreSame(geared, best, "A lucky charm has to count, or gear cannot build for events.");
        }

        [Test]
        public void BestFor_SkipsTheDowned()
        {
            var downed = new MockCombatUnit("Acolyte", 3, 3, 10);
            downed.Stats[StatType.Intelligence] = 20;
            downed.Stats.Health = 0;
            var standing = new MockCombatUnit("Warrior", 10, 5, 13);
            standing.Stats[StatType.Intelligence] = 2;

            var best = RoomEventResolver.BestFor(new List<ICombatUnit> { downed, standing }, StatType.Intelligence);

            Assert.AreSame(standing, best, "A downed hero is in no state to read anything.");
        }

        [Test]
        public void BestFor_NoneStatOrEmptyParty_IsNull()
        {
            var hero = new MockCombatUnit("Warrior", 10, 5, 13);

            Assert.IsNull(RoomEventResolver.BestFor(new List<ICombatUnit> { hero }, StatType.None));
            Assert.IsNull(RoomEventResolver.BestFor(new List<ICombatUnit>(), StatType.Luck));
            Assert.IsNull(RoomEventResolver.BestFor(null, StatType.Luck));
        }
    }
}
