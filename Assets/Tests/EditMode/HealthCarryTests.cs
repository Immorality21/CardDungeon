using Assets.Scripts.Dungeon;
using NUnit.Framework;

namespace Tests.EditMode
{
    /// <summary>
    /// Arriving on the next floor of a run heals nobody (owner, 2026-11-01), and a hero who fell stays
    /// down (playtest 3 finding 9): <see cref="HealthCarry"/>'s pure half. The record itself is
    /// <see cref="PartyHealthSnapshot"/>, which <c>PartyHealthSnapshotTests</c> covers.
    /// </summary>
    public class HealthCarryTests
    {
        [Test]
        public void Carries_ALaterFloorOfTheSameRun()
        {
            Assert.IsTrue(HealthCarry.Carries(1, "DrownedMarch", "DrownedMarch"));
            Assert.IsTrue(HealthCarry.Carries(4, "DrownedMarch", "DrownedMarch"));
        }

        [Test]
        public void Carries_TheRunsFirstFloor_CarriesNothing()
        {
            // A record left over from an earlier run must never hurt anyone at the start of a new one.
            Assert.IsFalse(HealthCarry.Carries(0, "DrownedMarch", "DrownedMarch"));
        }

        [Test]
        public void Carries_ARunSaveFromAnotherRun_CarriesNothing()
        {
            Assert.IsFalse(HealthCarry.Carries(2, "TheWarrens", "DrownedMarch"));
            Assert.IsFalse(HealthCarry.Carries(2, null, "DrownedMarch"));
            Assert.IsFalse(HealthCarry.Carries(2, null, null));
        }

        [Test]
        public void HeroHealth_ARunSaveFromBeforeThisRule_LeavesEveryoneFull()
        {
            var old = new RunSaveData();

            Assert.AreEqual(30, PartyHealthSnapshot.HealthFor(old.HeroHealth, "Warrior", 30));
        }
    }
}
