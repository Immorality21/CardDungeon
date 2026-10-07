using System.Collections.Generic;
using Assets.Scripts.Progression;
using NUnit.Framework;

namespace Tests.EditMode
{
    public class RunHistoryTests
    {
        private const string Now = "2026-10-07T12:00:00Z";

        private static RunHistorySaveData Started(string run = "DrownedMarch", int floors = 5)
        {
            var data = new RunHistorySaveData();
            RunHistoryOps.FloorStarted(data, run, "The Drowned March", floors, 0, "Silt Shallows", true, false, 0,
                null, Now);
            return data;
        }

        [Test]
        public void FloorStarted_FirstFloor_BeginsAnAttempt()
        {
            var data = Started();

            Assert.IsTrue(data.HasCurrent);
            Assert.AreEqual("DrownedMarch", data.Current.RunKey);
            Assert.AreEqual(5, data.Current.TotalFloors);
            Assert.AreEqual(RunOutcome.InProgress, data.Current.Outcome);
        }

        [Test]
        public void FloorStarted_NoRunKey_RecordsNothing()
        {
            var data = new RunHistorySaveData();
            RunHistoryOps.FloorStarted(data, null, null, 1, 0, null, true, false, 0, null, Now);

            Assert.IsFalse(data.HasCurrent);
        }

        [Test]
        public void FloorStarted_ResumeOrRebuiltFirstFloor_ContinuesTheAttempt()
        {
            var data = Started();
            RunHistoryOps.EnemyDefeated(data, false);

            RunHistoryOps.FloorStarted(data, "DrownedMarch", "The Drowned March", 5, 0, "Silt Shallows", false, false, 0, null, Now);
            RunHistoryOps.FloorStarted(data, "DrownedMarch", "The Drowned March", 5, 0, "Silt Shallows", true, false, 0, null, Now);

            Assert.AreEqual(1, data.Current.Kills);
            Assert.AreEqual(0, data.Records.Count);
        }

        [Test]
        public void FloorStarted_AnotherRun_ClosesTheUnfinishedAttemptAndKeepsIt()
        {
            var data = Started();
            RunHistoryOps.FloorStarted(data, "AshenDeep", "The Ashen Deep", 3, 0, "Cinder Gate", true, false, 0, null, Now);

            Assert.AreEqual("AshenDeep", data.Current.RunKey);
            Assert.AreEqual(1, data.Records.Count);
            Assert.AreEqual("DrownedMarch", data.Records[0].RunKey);
        }

        [Test]
        public void KeptNumbers_WaitForTheFloorToBeBanked()
        {
            var data = Started();
            RunHistoryOps.XpEarned(data, 40);
            RunHistoryOps.ItemsFound(data, 3);

            Assert.AreEqual(0, data.Current.XpKept);

            RunHistoryOps.FloorCleared(data, false, Now);

            Assert.AreEqual(40, data.Current.XpKept);
            Assert.AreEqual(3, data.Current.ItemsKept);
            Assert.AreEqual(1, data.Current.FloorsCleared);
        }

        [Test]
        public void Retreat_DropsTheFloorsShare_AndKeepsTheRunGoing()
        {
            var data = Started();
            RunHistoryOps.XpEarned(data, 40);
            RunHistoryOps.EnemyDefeated(data, false);

            RunHistoryOps.Retreated(data);

            Assert.IsTrue(data.HasCurrent);
            Assert.AreEqual(1, data.Current.Retreats);
            Assert.AreEqual(1, data.Current.Kills, "what happened still happened");
            Assert.AreEqual(0, data.Current.FloorXp);
        }

        [Test]
        public void Fell_EndsTheAttempt_WithWhoItFellTo_AndLosesTheFloor()
        {
            var data = Started();
            RunHistoryOps.FloorCleared(data, false, Now);
            RunHistoryOps.FloorStarted(data, "DrownedMarch", "The Drowned March", 5, 1, "The Reedcage", true, false, 0, null, Now);
            RunHistoryOps.XpEarned(data, 25);

            RunHistoryOps.Fell(data, new[] { "Bog Shaman", "Bog Shaman" }, Now);

            Assert.IsFalse(data.HasCurrent);
            var record = data.Records[0];
            Assert.AreEqual(RunOutcome.Fell, record.Outcome);
            Assert.AreEqual(1, record.FloorIndex);
            Assert.AreEqual(0, record.XpKept);
            CollectionAssert.AreEqual(new[] { "Bog Shaman", "Bog Shaman" }, record.FellTo);
        }

        [Test]
        public void FloorCleared_LastFloor_FinishesAsCleared()
        {
            var data = Started("TheWarrens", 1);
            RunHistoryOps.CurrencyKept(data, false, 120);

            RunHistoryOps.FloorCleared(data, true, Now);

            Assert.IsFalse(data.HasCurrent);
            Assert.AreEqual(RunOutcome.Cleared, data.Records[0].Outcome);
            Assert.AreEqual(120, data.Records[0].GoldKept);
        }

        [Test]
        public void Records_AreCapped_NewestFirst()
        {
            var data = new RunHistorySaveData();
            for (int i = 0; i < RunHistoryOps.MaxRecords + 5; i++)
            {
                RunHistoryOps.FloorStarted(data, "Run" + i, "Run", 1, 0, null, true, false, 0, null, Now);
                RunHistoryOps.FloorCleared(data, true, Now);
            }

            Assert.AreEqual(RunHistoryOps.MaxRecords, data.Records.Count);
            Assert.AreEqual("Run" + (RunHistoryOps.MaxRecords + 4), data.Records[0].RunKey);
        }

        [Test]
        public void Summarize_CountsOneRunsAttempts()
        {
            var data = Started();
            RunHistoryOps.Fell(data, new[] { "Mirefather" }, Now);
            RunHistoryOps.FloorStarted(data, "DrownedMarch", "The Drowned March", 5, 0, "Silt Shallows", true, false, 0, null, Now);
            RunHistoryOps.FloorStarted(data, "DrownedMarch", "The Drowned March", 5, 3, "Rotwater Deep", true, false, 0, null, Now);
            RunHistoryOps.Fell(data, null, Now);
            RunHistoryOps.FloorStarted(data, "TheWarrens", "The Warrens", 2, 0, null, true, false, 0, null, Now);
            RunHistoryOps.FloorCleared(data, true, Now);

            var summary = RunHistoryOps.Summarize(data, "DrownedMarch");

            Assert.AreEqual(2, summary.Attempts);
            Assert.AreEqual(2, summary.Falls);
            Assert.AreEqual(4, summary.DeepestFloor);
            Assert.AreEqual(3, summary.Last.FloorIndex, "the most recent attempt");
        }

        [Test]
        public void DescribeFoes_GroupsRepeats()
        {
            Assert.AreEqual("Mirefather and 2 Bog Shamans",
                RunHistoryOps.DescribeFoes(new List<string> { "Mirefather", "Bog Shaman", "Bog Shaman" }));
            Assert.AreEqual("2 Floating Eyes, Hex Weaver and 3 Cinder Imps",
                RunHistoryOps.DescribeFoes(new List<string> { "Floating Eye", "Hex Weaver", "Floating Eye", "Cinder Imp", "Cinder Imp", "Cinder Imp" }));
            Assert.AreEqual(string.Empty, RunHistoryOps.DescribeFoes(null));
        }

        [Test]
        public void DescribeEnd_SaysWhereAndToWhat()
        {
            var record = new RunRecord
            {
                Outcome = RunOutcome.Fell, FloorIndex = 2, TotalFloors = 5, FloorName = "The Weeping Causeway",
                FellTo = new List<string> { "Mirefather" }
            };

            Assert.AreEqual("Fell on floor 3 of 5 (The Weeping Causeway) to Mirefather", RunHistoryOps.DescribeEnd(record));
        }
    }
}
