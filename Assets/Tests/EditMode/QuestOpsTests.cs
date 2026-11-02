using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Combat;
using Assets.Scripts.Enemies;
using Assets.Scripts.Hub.Quests;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>The tavern's quest board: three drawn from the unlocked pool, taken, counted,
    /// rerolled for gold while untaken, collected and recycled.</summary>
    public class QuestOpsTests
    {
        private readonly List<ScriptableObject> _created = new List<ScriptableObject>();
        private static readonly string[] NoRuns = new string[0];
        private static int First(int count) => 0;

        [TearDown]
        public void TearDown()
        {
            foreach (var asset in _created)
            {
                Object.DestroyImmediate(asset);
            }
            _created.Clear();
        }

        private QuestSO Quest(string key, int count = 3, QuestKind kind = QuestKind.KillCount, params string[] runs)
        {
            var quest = ScriptableObject.CreateInstance<QuestSO>();
            _created.Add(quest);
            quest.Key = key;
            quest.Kind = kind;
            quest.Count = count;
            quest.RequiredRunKeys = runs.ToList();
            return quest;
        }

        private QuestSO SkeletonQuest()
        {
            var skeleton = ScriptableObject.CreateInstance<EnemySO>();
            _created.Add(skeleton);
            skeleton.Key = "Skeleton";
            var quest = Quest("KillSkeletons");
            quest.Target = skeleton;
            return quest;
        }

        private static QuestSaveData Taken(QuestSO quest)
        {
            var save = new QuestSaveData();
            save.Board.Add(quest.Key);
            QuestOps.Accept(save, quest);
            return save;
        }

        [Test]
        public void FillBoard_DrawsThree_FromUnlockedQuestsOnly()
        {
            var pool = new List<QuestSO> { Quest("A"), Quest("B"), Quest("Locked", 3, QuestKind.KillCount, "Catacombs"), Quest("C"), Quest("D") };
            var save = new QuestSaveData();

            QuestOps.FillBoard(save, pool, NoRuns, First);

            Assert.AreEqual(QuestOps.BoardSize, save.Board.Count);
            CollectionAssert.DoesNotContain(save.Board, "Locked");
            CollectionAssert.AllItemsAreUnique(save.Board);
        }

        [Test]
        public void Reroll_SwapsAnUntakenQuestInItsSlot_NeverATakenOne()
        {
            var a = Quest("A");
            var b = Quest("B");
            var spare = Quest("Spare");
            var pool = new List<QuestSO> { a, b, spare };
            var save = new QuestSaveData();
            save.Board.AddRange(new[] { "A", "B" });

            Assert.IsTrue(QuestOps.Reroll(save, pool, NoRuns, a, First));
            CollectionAssert.AreEqual(new[] { "Spare", "B" }, save.Board, "same slot");

            QuestOps.Accept(save, b);
            Assert.IsFalse(QuestOps.Reroll(save, pool, NoRuns, b, First), "a taken quest stays");
        }

        [Test]
        public void Kills_CountOnlyForTakenQuests_AndTheRightEnemy()
        {
            var quest = SkeletonQuest();
            var untaken = new QuestSaveData();
            untaken.Board.Add(quest.Key);
            Assert.IsFalse(QuestOps.RecordKill(untaken, new[] { quest }, "Skeleton"));

            var save = Taken(quest);
            Assert.IsFalse(QuestOps.RecordKill(save, new[] { quest }, "Ghoul"));
            for (int i = 0; i < 3; i++)
            {
                QuestOps.RecordKill(save, new[] { quest }, "Skeleton");
            }
            Assert.AreEqual(QuestState.Ready, QuestOps.StateOf(save, quest));
        }

        [Test]
        public void KindQuest_CountsAnyEnemyOfThatKind_AndRevisitOnlyNeedsARevisit()
        {
            var undead = Quest("KillUndead", 2);
            undead.TargetTraits = UnitTraits.Undead;
            var save = Taken(undead);
            Assert.IsFalse(QuestOps.RecordKill(save, new[] { undead }, "SlagHound", UnitTraits.Beast));
            QuestOps.RecordKill(save, new[] { undead }, "Skeleton", UnitTraits.Undead | UnitTraits.Bloodless);
            QuestOps.RecordKill(save, new[] { undead }, "Ghoul", UnitTraits.Undead);
            Assert.AreEqual(QuestState.Ready, QuestOps.StateOf(save, undead));

            var warden = SkeletonQuest();
            warden.RevisitOnly = true;
            var revisit = Taken(warden);
            Assert.IsFalse(QuestOps.RecordKill(revisit, new[] { warden }, "Skeleton", UnitTraits.None, onRevisit: false));
            Assert.IsTrue(QuestOps.RecordKill(revisit, new[] { warden }, "Skeleton", UnitTraits.None, onRevisit: true));
        }

        [Test]
        public void WipeQuest_CountsWipes_NotKills()
        {
            var quest = Quest("Wipes", 2, QuestKind.Wipes);
            var save = Taken(quest);

            Assert.IsFalse(QuestOps.RecordKill(save, new[] { quest }, "Skeleton"));
            QuestOps.RecordEvent(save, new[] { quest }, QuestKind.Wipes);
            Assert.IsFalse(QuestOps.RecordEvent(save, new[] { quest }, QuestKind.ClearFloors));
            QuestOps.RecordEvent(save, new[] { quest }, QuestKind.Wipes);

            Assert.AreEqual(QuestState.Ready, QuestOps.StateOf(save, quest));
        }

        [Test]
        public void ACollectedQuest_SitsOut_UntilOthersHaveBeenCollected()
        {
            var a = Quest("A", 1);
            var pool = new List<QuestSO> { a };
            var save = Taken(a);
            QuestOps.RecordKill(save, pool, "Anything");
            QuestOps.Claim(save, a);

            QuestOps.FillBoard(save, pool, NoRuns, First);
            CollectionAssert.IsEmpty(save.Board, "cooling down: the slot stays empty rather than repeat it");

            save.Completed += QuestOps.CooldownCollections;
            QuestOps.FillBoard(save, pool, NoRuns, First);
            CollectionAssert.AreEqual(new[] { "A" }, save.Board, "back once enough others were collected");
        }

        [Test]
        public void Collecting_RecyclesTheQuest_AndOnlyWhenReady()
        {
            var quest = Quest("A", 1);
            var save = Taken(quest);
            Assert.IsFalse(QuestOps.Claim(save, quest), "not done yet");

            QuestOps.RecordKill(save, new[] { quest }, "Anything");
            Assert.IsTrue(QuestOps.Claim(save, quest));

            Assert.IsFalse(QuestOps.IsOnBoard(save, quest), "off the board");
            Assert.AreEqual(QuestState.Offered, QuestOps.StateOf(save, quest), "back in the pool, fresh");
            Assert.IsFalse(QuestOps.Claim(save, quest), "collected once");
            Assert.AreEqual(1, save.Completed);
        }
    }
}
