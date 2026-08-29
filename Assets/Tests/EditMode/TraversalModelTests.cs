using Assets.Scripts.Balance;
using NUnit.Framework;

namespace Tests.EditMode
{
    /// <summary>
    /// Pins the topology model behind <c>RunCurveModel</c>'s traversal factor. These are properties of
    /// a tree rather than tuning numbers, so they should survive a balance pass untouched — if one of
    /// these moves, the dungeon generator changed shape and the run curve is now lying.
    /// </summary>
    public class TraversalModelTests
    {
        [SetUp]
        public void ClearCache()
        {
            // The model memoises, and a stale entry would make one test's seed leak into another's.
            TraversalModel.ClearCache();
        }

        [Test]
        public void Measure_SingleRoomFloor_IsEntirelyWalked()
        {
            var band = TraversalModel.Measure(1, 0.667f);

            Assert.AreEqual(1, band.FullClear);
            Assert.AreEqual(1f, band.Beeline);
            Assert.AreEqual(1f, band.Explorer);
        }

        [Test]
        public void Measure_TwoRoomFloor_IsEntirelyWalked()
        {
            // Two rooms is a single door: the second room is both the only branch and the exit.
            var band = TraversalModel.Measure(2, 0.667f);

            Assert.AreEqual(2f, band.Beeline, 0.0001f);
            Assert.AreEqual(2f, band.Explorer, 0.0001f);
        }

        [Test]
        public void Measure_BeelineNeverExceedsTheExplorer()
        {
            // The road to the exit is a subset of what a blind walk opens, by construction.
            foreach (int rooms in new[] { 3, 5, 8, 11, 16, 31 })
            {
                var band = TraversalModel.Measure(rooms, 0.667f);

                Assert.LessOrEqual(band.Beeline, band.Explorer + 0.0001f,
                    $"Beeline exceeded the explorer on a {rooms}-room floor.");
                Assert.LessOrEqual(band.Explorer, band.FullClear + 0.0001f,
                    $"Explorer exceeded the whole floor on a {rooms}-room floor.");
                Assert.GreaterOrEqual(band.Beeline, 2f,
                    $"A {rooms}-room floor must cost at least a start room and one more.");
            }
        }

        [Test]
        public void Measure_LongerFloorsAreWalkedLessCompletely()
        {
            // The finding that makes room count the weakest lever: the shortfall grows with length.
            var small = TraversalModel.Measure(8, 0.667f);
            var large = TraversalModel.Measure(31, 0.667f);

            Assert.Less(large.BeelineFraction, small.BeelineFraction,
                "A longer floor should put a smaller share of itself on the road to the exit.");
            Assert.Less(large.ExplorerFraction, small.ExplorerFraction,
                "A longer floor should leave a blind explorer a smaller share of it.");
        }

        [Test]
        public void Measure_HigherChainBiasPutsMoreOfTheFloorOnTheRoadOut()
        {
            // ChainBias is the free traversal lever: a stringier tree has a longer unique route.
            var loose = TraversalModel.Measure(16, 0.0f);
            var stringy = TraversalModel.Measure(16, 1.0f);

            Assert.Greater(stringy.Beeline, loose.Beeline,
                "Biasing growth toward leaves should lengthen the only road to the exit.");
        }

        [Test]
        public void Measure_IsDeterministic()
        {
            var first = TraversalModel.Measure(16, 0.667f);
            TraversalModel.ClearCache();
            var second = TraversalModel.Measure(16, 0.667f);

            Assert.AreEqual(first.Beeline, second.Beeline, 0.0001f);
            Assert.AreEqual(first.Explorer, second.Explorer, 0.0001f);
        }

        [Test]
        public void PopulatedFraction_FullClear_PricesEveryRoom()
        {
            Assert.AreEqual(1f, TraversalModel.PopulatedFraction(16, 0.667f, TraversalMode.FullClear));
        }

        [Test]
        public void PopulatedFraction_DiscountsTheStartRoomFromBothSides()
        {
            // The run curve has already taken the party's start room off the total, so the factor is
            // (visited - 1) / (rooms - 1) and not visited / rooms. Getting this wrong double-counts
            // the one room the player is guaranteed to be standing in.
            const int rooms = 16;
            var band = TraversalModel.Measure(rooms, 0.667f);

            float expected = (band.Explorer - 1f) / (rooms - 1f);
            float actual = TraversalModel.PopulatedFraction(rooms, 0.667f, TraversalMode.Explorer);

            Assert.AreEqual(expected, actual, 0.0001f);
            Assert.Less(actual, band.ExplorerFraction,
                "Discounting the start room from both sides must bite harder than the raw share.");
        }

        [Test]
        public void PopulatedFraction_BeelineIsHarsherThanTheExplorer()
        {
            float beeline = TraversalModel.PopulatedFraction(16, 0.667f, TraversalMode.Beeline);
            float explorer = TraversalModel.PopulatedFraction(16, 0.667f, TraversalMode.Explorer);

            Assert.Less(beeline, explorer);
            Assert.Greater(beeline, 0f);
            Assert.LessOrEqual(explorer, 1f);
        }

        [Test]
        public void PopulatedFraction_DegenerateFloorsPriceEverything()
        {
            // Nothing to skip, so no discount — and no divide by zero on rooms - 1.
            Assert.AreEqual(1f, TraversalModel.PopulatedFraction(1, 0.667f, TraversalMode.Explorer));
            Assert.AreEqual(1f, TraversalModel.PopulatedFraction(0, 0.667f, TraversalMode.Beeline));
        }
    }
}
