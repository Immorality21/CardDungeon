using System.Collections.Generic;
using Assets.Scripts.Dungeon;
using NUnit.Framework;

namespace Tests.EditMode
{
    /// <summary>
    /// A hero who fell stays down on the run's next floors until the party goes back to town
    /// (playtest 3 finding 9): <see cref="DownedCarry"/>'s pure half.
    /// </summary>
    public class DownedCarryTests
    {
        private static KeyValuePair<string, int> Hero(string key, int health)
        {
            return new KeyValuePair<string, int>(key, health);
        }

        [Test]
        public void Collect_KeepsOnlyTheHeroesAtZeroHealth()
        {
            var downed = DownedCarry.Collect(new[] { Hero("Warrior", 12), Hero("Paladin", 0), Hero("Mage", -3) });

            CollectionAssert.AreEqual(new[] { "Paladin", "Mage" }, downed);
        }

        [Test]
        public void Collect_NamesEachHeroOnce_AndSkipsBlankKeys()
        {
            var downed = DownedCarry.Collect(new[] { Hero("Paladin", 0), Hero("Paladin", 0), Hero("", 0) });

            CollectionAssert.AreEqual(new[] { "Paladin" }, downed);
        }

        [Test]
        public void Collect_NobodyDown_IsEmpty()
        {
            Assert.IsEmpty(DownedCarry.Collect(new[] { Hero("Warrior", 1) }));
            Assert.IsEmpty(DownedCarry.Collect(null));
        }

        [Test]
        public void StaysDown_ALaterFloor_KeepsTheFallenDown()
        {
            var downed = new List<string> { "Paladin" };

            Assert.IsTrue(DownedCarry.StaysDown(1, downed, "Paladin"));
            Assert.IsTrue(DownedCarry.StaysDown(4, downed, "Paladin"));
            Assert.IsFalse(DownedCarry.StaysDown(1, downed, "Warrior"));
        }

        [Test]
        public void StaysDown_TheRunsFirstFloor_CarriesNothing()
        {
            // A list left over from an earlier run must never keep anyone down at the start of a new one.
            Assert.IsFalse(DownedCarry.StaysDown(0, new List<string> { "Paladin" }, "Paladin"));
        }

        [Test]
        public void StaysDown_AfterTheHubEmptiedTheList_NobodyIsDown()
        {
            Assert.IsFalse(DownedCarry.StaysDown(2, new List<string>(), "Paladin"));
            Assert.IsFalse(DownedCarry.StaysDown(2, null, "Paladin"));
        }
    }
}
