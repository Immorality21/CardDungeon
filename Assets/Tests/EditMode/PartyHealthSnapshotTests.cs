using System.Collections.Generic;
using Assets.Scripts.Dungeon;
using NUnit.Framework;

namespace Tests.EditMode
{
    /// <summary>
    /// The rules for carrying hero health across a mid-level quit and resume. Health is the level's
    /// scarce resource - it refills only on entering a fresh dungeon - so restoring it full made
    /// quitting to the menu a free heal and quietly undid every room event's damage.
    /// </summary>
    public class PartyHealthSnapshotTests
    {
        private static List<HeroHealthSaveData> Saved(params (string key, int health)[] entries)
        {
            var records = new List<HeroHealthSaveData>();
            foreach (var entry in entries)
            {
                records.Add(new HeroHealthSaveData { HeroKey = entry.key, Health = entry.health });
            }
            return records;
        }

        [Test]
        public void HealthFor_HeroInTheSave_ResumesWounded()
        {
            var saved = Saved(("Warrior", 4), ("Tank", 11));

            Assert.AreEqual(4, PartyHealthSnapshot.HealthFor(saved, "Warrior", 13));
            Assert.AreEqual(11, PartyHealthSnapshot.HealthFor(saved, "Tank", 17));
        }

        [Test]
        public void HealthFor_HeroNotInTheSave_ResumesFull()
        {
            // The only way to be absent is to have joined after the file was written - a captive
            // freed later in the level - and Party.AddHero already says a rescue arrives full.
            var saved = Saved(("Warrior", 4));

            Assert.AreEqual(17, PartyHealthSnapshot.HealthFor(saved, "Tank", 17));
        }

        [Test]
        public void HealthFor_NoSaveData_ResumesFull()
        {
            Assert.AreEqual(13, PartyHealthSnapshot.HealthFor(null, "Warrior", 13));
            Assert.AreEqual(13, PartyHealthSnapshot.HealthFor(new List<HeroHealthSaveData>(), "Warrior", 13));
        }

        [Test]
        public void HealthFor_DownedHero_StaysDown()
        {
            // Nothing revives a hero inside a level - HealAll only fires on a fresh dungeon - so
            // resuming a 0 as a 0 is what happens without the quit.
            var saved = Saved(("Warrior", 0));

            Assert.AreEqual(0, PartyHealthSnapshot.HealthFor(saved, "Warrior", 13));
        }

        [Test]
        public void HealthFor_BarShrankSinceTheSave_ClampsIntoTheBar()
        {
            var saved = Saved(("Warrior", 20));

            Assert.AreEqual(13, PartyHealthSnapshot.HealthFor(saved, "Warrior", 13));
        }

        [Test]
        public void HealthFor_BarGrewSinceTheSave_KeepsTheWound()
        {
            // Re-deriving a hero whose bar grew must not hand back the new ceiling: the resume
            // would be a heal again, just a bigger one.
            var saved = Saved(("Warrior", 4));

            Assert.AreEqual(4, PartyHealthSnapshot.HealthFor(saved, "Warrior", 30));
        }

        [Test]
        public void HealthFor_NegativeStoredHealth_ReadsAsDowned()
        {
            var saved = Saved(("Warrior", -5));

            Assert.AreEqual(0, PartyHealthSnapshot.HealthFor(saved, "Warrior", 13));
        }

        [Test]
        public void Capture_WritesOneRecordPerKeyedHero()
        {
            var records = PartyHealthSnapshot.Capture(new List<KeyValuePair<string, int>>
            {
                new KeyValuePair<string, int>("Warrior", 4),
                new KeyValuePair<string, int>("Tank", 11)
            });

            Assert.AreEqual(2, records.Count);
            Assert.AreEqual("Warrior", records[0].HeroKey);
            Assert.AreEqual(4, records[0].Health);
            Assert.AreEqual("Tank", records[1].HeroKey);
            Assert.AreEqual(11, records[1].Health);
        }

        [Test]
        public void Capture_SkipsHeroesWithNoKey()
        {
            // An unkeyed hero cannot be resolved on the way back in; writing them would only make
            // the file ambiguous.
            var records = PartyHealthSnapshot.Capture(new List<KeyValuePair<string, int>>
            {
                new KeyValuePair<string, int>("", 4),
                new KeyValuePair<string, int>(null, 4),
                new KeyValuePair<string, int>("Tank", 11)
            });

            Assert.AreEqual(1, records.Count);
            Assert.AreEqual("Tank", records[0].HeroKey);
        }

        [Test]
        public void CaptureThenRestore_RoundTripsTheWholeParty()
        {
            var records = PartyHealthSnapshot.Capture(new List<KeyValuePair<string, int>>
            {
                new KeyValuePair<string, int>("Warrior", 4),
                new KeyValuePair<string, int>("Tank", 0)
            });

            Assert.AreEqual(4, PartyHealthSnapshot.HealthFor(records, "Warrior", 13));
            Assert.AreEqual(0, PartyHealthSnapshot.HealthFor(records, "Tank", 17));
        }
    }
}
