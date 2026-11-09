using System.Collections.Generic;
using Assets.Scripts.Dungeon;
using Assets.Scripts.Tutorial;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// The first-time hints' rules (<see cref="TutorialHints"/>): each said once, the log in the order
    /// seen, and the two hub conditions (a full party, a level to replay). Pure - no scene, no save.
    /// </summary>
    public class TutorialHintsTests
    {
        [Test]
        public void IsDue_UnseenHint_IsDue()
        {
            Assert.IsTrue(TutorialHints.IsDue(new List<int>(), TutorialCue.ChargesLastTheLevel));
        }

        [Test]
        public void IsDue_SeenHint_IsNotDueAgain()
        {
            var seen = new List<int> { (int)TutorialCue.ChargesLastTheLevel };
            Assert.IsFalse(TutorialHints.IsDue(seen, TutorialCue.ChargesLastTheLevel));
        }

        [Test]
        public void IsDue_AGuidedStepsLine_IsNeverAHint()
        {
            Assert.IsFalse(TutorialHints.IsDue(new List<int>(), TutorialCue.BuildHallInTown));
        }

        [Test]
        public void SeenInOrder_KeepsTheOrderSeen_DropsRepeatsAndStepLines()
        {
            var seen = new List<int>
            {
                (int)TutorialCue.UltraReady,
                (int)TutorialCue.PickNode,
                (int)TutorialCue.FirstDeath,
                (int)TutorialCue.UltraReady,
            };

            var log = TutorialHints.SeenInOrder(seen);

            CollectionAssert.AreEqual(new[] { TutorialCue.UltraReady, TutorialCue.FirstDeath }, log);
        }

        [Test]
        public void Logged_HoldsEveryFirstTimeHint()
        {
            foreach (var cue in new[]
            {
                TutorialCue.ChargesLastTheLevel, TutorialCue.RefugeOnce, TutorialCue.SummonOncePerLevel,
                TutorialCue.UltraReady, TutorialCue.RevisitAndFear, TutorialCue.PartyFull,
            })
            {
                Assert.IsTrue(TutorialHints.IsLogged(cue), cue.ToString());
            }
        }

        [TestCase(4, 4, false)]
        [TestCase(5, 4, true)]
        [TestCase(2, 4, false)]
        public void PartyIsFull_OnlyWhenSomeoneMustStayHome(int owned, int cap, bool full)
        {
            Assert.AreEqual(full, TutorialHints.PartyIsFull(owned, cap));
        }

        private static CampaignNodeState MapNode(CampaignNodeStatus status, bool canStart, bool repeatable)
        {
            var run = ScriptableObject.CreateInstance<RunDefinitionSO>();
            run.Repeatable = repeatable;
            return new CampaignNodeState { Node = new CampaignNodeEntry { Run = run }, Status = status, CanStart = canStart };
        }

        [Test]
        public void OffersRevisit_ACompletedRepeatableLevel_IsOffered()
        {
            var states = new List<CampaignNodeState> { MapNode(CampaignNodeStatus.Completed, true, true) };
            Assert.IsTrue(TutorialHints.OffersRevisit(states));
        }

        [Test]
        public void OffersRevisit_OnlyNewLevelsOrOneShots_IsNotOffered()
        {
            var states = new List<CampaignNodeState>
            {
                MapNode(CampaignNodeStatus.Available, true, true),
                MapNode(CampaignNodeStatus.Completed, true, false),
                MapNode(CampaignNodeStatus.Completed, false, true),
            };
            Assert.IsFalse(TutorialHints.OffersRevisit(states));
        }
    }
}
