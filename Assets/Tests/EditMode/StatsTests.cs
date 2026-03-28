using Assets.Scripts.Rooms;
using NUnit.Framework;

namespace Tests.EditMode
{
    public class StatsTests
    {
        [Test]
        public void Constructor_SetsAllFields()
        {
            var stats = new Stats(10, 5, 100, 8);

            Assert.AreEqual(10, stats.Attack);
            Assert.AreEqual(5, stats.Defense);
            Assert.AreEqual(100, stats.Health);
            Assert.AreEqual(100, stats.MaxHealth);
            Assert.AreEqual(8, stats.Agility);
        }

        [Test]
        public void Constructor_MaxHealthEqualsHealth()
        {
            var stats = new Stats(1, 1, 50);

            Assert.AreEqual(stats.Health, stats.MaxHealth);
        }

        [Test]
        public void Constructor_DefaultAgility_IsFive()
        {
            var stats = new Stats(1, 1, 50);

            Assert.AreEqual(5, stats.Agility);
        }

        [Test]
        public void Constructor_ZeroHealth()
        {
            var stats = new Stats(1, 1, 0);

            Assert.AreEqual(0, stats.Health);
            Assert.AreEqual(0, stats.MaxHealth);
        }

        [Test]
        public void Health_CanBeMutated()
        {
            var stats = new Stats(1, 1, 50);

            stats.Health -= 10;

            Assert.AreEqual(40, stats.Health);
            Assert.AreEqual(50, stats.MaxHealth);
        }
    }
}
