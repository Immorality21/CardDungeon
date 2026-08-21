using System.Collections.Generic;
using Assets.Scripts.Cards;
using Assets.Scripts.Rooms.Events;
using Assets.Scripts.UnitStats;
using NUnit.Framework;

namespace Tests.EditMode
{
    /// <summary>
    /// Level afflictions are what let a room event cost the party something that outlives the room.
    /// The awkward part is that <see cref="CombatBuffTracker"/> is rebuilt per fight and ticks per
    /// turn, so the standing state has to live outside it and be re-seeded - these cover that seam.
    /// </summary>
    public class LevelAfflictionTrackerTests
    {
        [Test]
        public void Add_SameBuffTwice_Sums()
        {
            var tracker = new LevelAfflictionTracker();

            tracker.Add("Warrior", BuffType.Strength, -2);
            tracker.Add("Warrior", BuffType.Strength, -3);

            Assert.AreEqual(1, tracker.Entries.Count, "The same curse twice is one entry, not two.");
            Assert.AreEqual(-5, tracker.Entries[0].Amount, "Two cursed idols are worse than one.");
        }

        [Test]
        public void Add_DifferentHeroesAndBuffs_StaySeparate()
        {
            var tracker = new LevelAfflictionTracker();

            tracker.Add("Warrior", BuffType.Strength, -2);
            tracker.Add("Tank", BuffType.Strength, -2);
            tracker.Add("Warrior", BuffType.Agility, -1);

            Assert.AreEqual(3, tracker.Entries.Count);
            Assert.AreEqual(2, tracker.For("Warrior").Count);
            Assert.AreEqual(1, tracker.For("Tank").Count);
        }

        [Test]
        public void Add_IgnoresNoOpEntries()
        {
            var tracker = new LevelAfflictionTracker();

            tracker.Add(null, BuffType.Strength, -2);
            tracker.Add("Warrior", BuffType.None, -2);
            tracker.Add("Warrior", BuffType.Strength, 0);

            Assert.IsTrue(tracker.IsEmpty,
                "An unkeyed, untyped or zero affliction is authoring noise, not state.");
        }

        [Test]
        public void SeedCombat_AppliesTheDebuffToTheFightsTracker()
        {
            var tracker = new LevelAfflictionTracker();
            tracker.Add("Warrior", BuffType.Strength, -3);

            var hero = new MockCombatUnit("Warrior", 10, 5, 13);
            var buffTracker = new CombatBuffTracker();

            tracker.SeedCombat("Warrior", hero, buffTracker);

            Assert.AreEqual(-3, buffTracker.GetBuffAmount(hero, StatType.Strength),
                "Without re-seeding, a curse would only affect the fight it was picked up in - and "
                + "there is no fight when it is picked up.");
        }

        [Test]
        public void SeedCombat_StatusEffectsGoThroughTheirOwnHandler()
        {
            var tracker = new LevelAfflictionTracker();
            tracker.Add("Warrior", BuffType.Slow, -1);

            var hero = new MockCombatUnit("Warrior", 10, 5, 13);
            var buffTracker = new CombatBuffTracker();

            tracker.SeedCombat("Warrior", hero, buffTracker);

            Assert.IsTrue(buffTracker.HasStatusEffect(hero, BuffType.Slow),
                "Routing through BuffHandlerRegistry is what makes a status effect apply as one.");
        }

        [Test]
        public void SeedCombat_OtherHeroesAreUnaffected()
        {
            var tracker = new LevelAfflictionTracker();
            tracker.Add("Warrior", BuffType.Strength, -3);

            var tank = new MockCombatUnit("Tank", 5, 15, 17);
            var buffTracker = new CombatBuffTracker();

            tracker.SeedCombat("Tank", tank, buffTracker);

            Assert.AreEqual(0, buffTracker.GetBuffAmount(tank, StatType.Strength));
        }

        [Test]
        public void SeedCombat_SurvivesALongFight()
        {
            var tracker = new LevelAfflictionTracker();
            tracker.Add("Warrior", BuffType.Strength, -3);

            var hero = new MockCombatUnit("Warrior", 10, 5, 13);
            var buffTracker = new CombatBuffTracker();
            tracker.SeedCombat("Warrior", hero, buffTracker);

            for (int turn = 0; turn < 200; turn++)
            {
                buffTracker.TickBuffs(hero);
            }

            Assert.AreEqual(-3, buffTracker.GetBuffAmount(hero, StatType.Strength),
                "These expire with the level, not with a turn count, so no fight may outlast them.");
        }

        [Test]
        public void SaveAndRestore_RoundTrips()
        {
            var tracker = new LevelAfflictionTracker();
            tracker.Add("Warrior", BuffType.Strength, -3);
            tracker.Add("Tank", BuffType.Slow, -1);

            var saved = tracker.GetSaveData();
            var restored = new LevelAfflictionTracker();
            restored.Restore(saved);

            Assert.AreEqual(2, restored.Entries.Count,
                "Quitting to the menu must not be a cure for a curse.");
            Assert.AreEqual(-3, restored.For("Warrior")[0].Amount);
            Assert.AreEqual(BuffType.Slow, restored.For("Tank")[0].Buff);
        }

        [Test]
        public void GetSaveData_IsACopy()
        {
            var tracker = new LevelAfflictionTracker();
            tracker.Add("Warrior", BuffType.Strength, -3);

            var saved = tracker.GetSaveData();
            saved[0].Amount = -99;

            Assert.AreEqual(-3, tracker.For("Warrior")[0].Amount,
                "A snapshot handed to the save layer must not be a live handle on the tracker.");
        }

        [Test]
        public void Restore_ReplacesRatherThanAppends()
        {
            var tracker = new LevelAfflictionTracker();
            tracker.Add("Warrior", BuffType.Strength, -3);

            tracker.Restore(new List<LevelAffliction> { new LevelAffliction("Tank", BuffType.Agility, -1) });

            Assert.AreEqual(1, tracker.Entries.Count);
            Assert.IsEmpty(tracker.For("Warrior"));
        }

        [Test]
        public void Clear_EmptiesForTheNextLevel()
        {
            var tracker = new LevelAfflictionTracker();
            tracker.Add("Warrior", BuffType.Strength, -3);

            tracker.Clear();

            Assert.IsTrue(tracker.IsEmpty, "Afflictions are level-scoped, exactly like health.");
        }
    }
}
