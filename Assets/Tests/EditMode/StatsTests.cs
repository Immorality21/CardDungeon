using Assets.Scripts.Rooms;
using Assets.Scripts.UnitStats;
using NUnit.Framework;

namespace Tests.EditMode
{
    public class StatsTests
    {
        [Test]
        public void Constructor_ClonesTheBlockRatherThanAliasingIt()
        {
            var block = TestStats.Block(10, 5, 100, 8);
            var stats = new Stats(block);

            block[StatType.Strength] = 99;

            Assert.AreEqual(10, stats[StatType.Strength],
                "Stats must own its own block: HeroSO.BaseStats is shared by every Hero built from "
                + "that asset, so aliasing would let one hero's level-up edit the asset.");
        }

        [Test]
        public void Constructor_StartsAtFullHealth()
        {
            var stats = new Stats(TestStats.Block(10, 5, 100, 8));

            Assert.AreEqual(100, stats.MaxHealth);
            Assert.AreEqual(100, stats.Health);
        }

        [Test]
        public void Constructor_BlockWithoutMaxHealth_YieldsZeroHealth()
        {
            var stats = new Stats(new StatBlock(new UnitStat(StatType.Strength, 3)));

            Assert.AreEqual(0, stats.MaxHealth);
            Assert.AreEqual(0, stats.Health,
                "A block missing MaxHealth produces a dead unit - which is why the SOs carry "
                + "StatBlock.Defaults() rather than an empty block.");
        }

        [Test]
        public void Constructor_MaxHealthEqualsHealth()
        {
            var stats = TestStats.Make(1, 1, 50);

            Assert.AreEqual(stats.Health, stats.MaxHealth);
        }


        [Test]
        public void Constructor_ZeroHealth()
        {
            var stats = TestStats.Make(1, 1, 0);

            Assert.AreEqual(0, stats.Health);
            Assert.AreEqual(0, stats.MaxHealth);
        }

        [Test]
        public void Health_CanBeMutated()
        {
            var stats = TestStats.Make(1, 1, 50);

            stats.Health -= 10;

            Assert.AreEqual(40, stats.Health);
            Assert.AreEqual(50, stats.MaxHealth);
        }
    }
}
