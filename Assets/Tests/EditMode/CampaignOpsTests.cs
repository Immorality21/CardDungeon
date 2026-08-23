using System.Collections.Generic;
using Assets.Scripts.Dungeon;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// The campaign graph's rules. These matter more than most: a mis-authored graph can strand a
    /// save forever (a branch whose prerequisites can never all be met), and the failure is invisible
    /// until a player reaches it. Every unlock decision is pure, so all of it is asserted here rather
    /// than discovered in play.
    ///
    /// <para>The fixture is the shape from the design discussion: a tutorial, then a fork into a
    /// mainline branch that rejoins and an optional secret branch that dead-ends.</para>
    /// </summary>
    public class CampaignOpsTests
    {
        private readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var asset in _created)
            {
                Object.DestroyImmediate(asset);
            }
            _created.Clear();
        }

        private RunDefinitionSO MakeRun(string key, bool repeatable = false)
        {
            var run = ScriptableObject.CreateInstance<RunDefinitionSO>();
            run.name = key;
            run.Key = key;
            run.DisplayName = key;
            run.Repeatable = repeatable;
            _created.Add(run);
            return run;
        }

        private CampaignSO MakeCampaign(params CampaignNodeEntry[] nodes)
        {
            var campaign = ScriptableObject.CreateInstance<CampaignSO>();
            campaign.Nodes = new List<CampaignNodeEntry>(nodes);
            _created.Add(campaign);
            return campaign;
        }

        private static CampaignNodeEntry Node(
            RunDefinitionSO run,
            CampaignUnlockMode mode = CampaignUnlockMode.All,
            bool secret = false,
            params RunDefinitionSO[] requires)
        {
            return new CampaignNodeEntry
            {
                Run = run,
                Requires = new List<RunDefinitionSO>(requires),
                UnlockMode = mode,
                Secret = secret
            };
        }

        private static HashSet<string> Completed(params string[] keys)
        {
            return new HashSet<string>(keys);
        }

        // --- Unlocking -----------------------------------------------------------------------

        [Test]
        public void IsUnlocked_NodeWithNoPrerequisites_IsAlwaysUnlocked()
        {
            var node = Node(MakeRun("Tutorial"));
            Assert.IsTrue(CampaignOps.IsUnlocked(node, Completed()));
        }

        [Test]
        public void IsUnlocked_AllMode_RequiresEveryPrerequisite()
        {
            var a = MakeRun("A");
            var b = MakeRun("B");
            var node = Node(MakeRun("C"), CampaignUnlockMode.All, false, a, b);

            Assert.IsFalse(CampaignOps.IsUnlocked(node, Completed("A")), "one of two is not enough in All mode");
            Assert.IsTrue(CampaignOps.IsUnlocked(node, Completed("A", "B")));
        }

        [Test]
        public void IsUnlocked_AnyMode_NeedsOnlyOnePrerequisite()
        {
            var viaMainline = MakeRun("BranchA3");
            var viaSecret = MakeRun("BranchB3");
            var node = Node(MakeRun("Four"), CampaignUnlockMode.Any, false, viaMainline, viaSecret);

            Assert.IsFalse(CampaignOps.IsUnlocked(node, Completed()));
            Assert.IsTrue(CampaignOps.IsUnlocked(node, Completed("BranchB3")), "either branch may rejoin");
        }

        [Test]
        public void RunKeyOf_FallsBackToAssetNameWhenKeyIsBlank()
        {
            var run = MakeRun("Tutorial");
            run.Key = string.Empty;
            run.name = "TutorialRun";
            Assert.AreEqual("TutorialRun", CampaignOps.RunKeyOf(run),
                "must match how MainMenuManager writes RunSaveData.RunKey");
        }

        // --- Node state ----------------------------------------------------------------------

        [Test]
        public void GetState_LockedNode_ListsWhatIsMissing()
        {
            var gate = MakeRun("Three");
            gate.DisplayName = "The Sunken Gate";
            var node = Node(MakeRun("Four"), CampaignUnlockMode.All, false, gate);

            var state = CampaignOps.GetState(node, Completed(), string.Empty);

            Assert.AreEqual(CampaignNodeStatus.Locked, state.Status);
            Assert.IsFalse(state.CanStart);
            CollectionAssert.AreEqual(new[] { "The Sunken Gate" }, state.MissingRequirements);
        }

        [Test]
        public void GetState_SecretNode_IsHiddenUntilUnlockedThenAvailable()
        {
            var gate = MakeRun("Three");
            var node = Node(MakeRun("SecretB1"), CampaignUnlockMode.All, true, gate);

            Assert.AreEqual(CampaignNodeStatus.Hidden,
                CampaignOps.GetState(node, Completed(), string.Empty).Status);
            Assert.AreEqual(CampaignNodeStatus.Available,
                CampaignOps.GetState(node, Completed("Three"), string.Empty).Status);
        }

        [Test]
        public void GetState_CompletedNonRepeatableRun_CannotBeStartedAgain()
        {
            var node = Node(MakeRun("Tutorial"));
            var state = CampaignOps.GetState(node, Completed("Tutorial"), string.Empty);

            Assert.AreEqual(CampaignNodeStatus.Completed, state.Status);
            Assert.IsFalse(state.CanStart, "the tutorial is one-shot");
        }

        [Test]
        public void GetState_CompletedRepeatableRun_CanBeStartedAgain()
        {
            var node = Node(MakeRun("Farmable", repeatable: true));
            var state = CampaignOps.GetState(node, Completed("Farmable"), string.Empty);

            Assert.AreEqual(CampaignNodeStatus.Completed, state.Status);
            Assert.IsTrue(state.CanStart);
        }

        [Test]
        public void GetState_ActiveRun_IsContinuedNotRestarted()
        {
            var node = Node(MakeRun("Tutorial"));
            var state = CampaignOps.GetState(node, Completed(), "Tutorial");

            Assert.AreEqual(CampaignNodeStatus.InProgress, state.Status);
            Assert.IsTrue(state.CanContinue);
            Assert.IsFalse(state.CanStart);
        }

        [Test]
        public void GetState_WhileAnotherRunIsActive_OtherNodesCannotBeStarted()
        {
            var other = Node(MakeRun("Elsewhere"));
            var state = CampaignOps.GetState(other, Completed(), "Tutorial");

            Assert.AreEqual(CampaignNodeStatus.Available, state.Status,
                "it is still visibly available - just not startable while a run is underway");
            Assert.IsFalse(state.CanStart, "starting it would overwrite the in-progress run save");
        }

        [Test]
        public void GetState_CompletedRunWhosePrerequisitesNoLongerHold_StaysCompleted()
        {
            // Re-authoring the graph must never read as lost progress.
            var addedLater = MakeRun("NewGate");
            var node = Node(MakeRun("Old"), CampaignUnlockMode.All, false, addedLater);

            var state = CampaignOps.GetState(node, Completed("Old"), string.Empty);
            Assert.AreEqual(CampaignNodeStatus.Completed, state.Status);
        }

        // --- The branching shape end to end ---------------------------------------------------

        [Test]
        public void GetStates_BranchingCampaign_OpensBothBranchesAndKeepsTheRejoinLocked()
        {
            var tutorial = MakeRun("Tutorial");
            var three = MakeRun("Three");
            var a1 = MakeRun("A1");
            var b1 = MakeRun("B1");
            var four = MakeRun("Four");

            var campaign = MakeCampaign(
                Node(tutorial),
                Node(three, CampaignUnlockMode.All, false, tutorial),
                Node(a1, CampaignUnlockMode.All, false, three),
                Node(b1, CampaignUnlockMode.All, true, three),
                Node(four, CampaignUnlockMode.All, false, a1));

            var states = CampaignOps.GetStates(campaign, Completed("Tutorial", "Three"), string.Empty);

            Assert.AreEqual(CampaignNodeStatus.Completed, states[0].Status, "tutorial");
            Assert.AreEqual(CampaignNodeStatus.Completed, states[1].Status, "the fork itself");
            Assert.AreEqual(CampaignNodeStatus.Available, states[2].Status, "mainline branch opens");
            Assert.AreEqual(CampaignNodeStatus.Available, states[3].Status, "secret branch is revealed too");
            Assert.AreEqual(CampaignNodeStatus.Locked, states[4].Status, "the rejoin waits on the branch");
        }

        [Test]
        public void GetStates_FreshSave_ExposesExactlyTheRootRun()
        {
            var tutorial = MakeRun("Tutorial");
            var later = MakeRun("Later");
            var campaign = MakeCampaign(
                Node(tutorial),
                Node(later, CampaignUnlockMode.All, false, tutorial));

            var states = CampaignOps.GetStates(campaign, Completed(), string.Empty);

            Assert.IsTrue(states[0].CanStart, "a fresh save must have somewhere to begin");
            Assert.IsFalse(states[1].CanStart);
        }

        // --- Authoring validation --------------------------------------------------------------

        [Test]
        public void GetUnreachableNodes_HealthyCampaign_IsEmpty()
        {
            var tutorial = MakeRun("Tutorial");
            var next = MakeRun("Next");
            var campaign = MakeCampaign(
                Node(tutorial),
                Node(next, CampaignUnlockMode.All, false, tutorial));

            CollectionAssert.IsEmpty(CampaignOps.GetUnreachableNodes(campaign));
        }

        [Test]
        public void GetUnreachableNodes_PrerequisiteCycle_IsReported()
        {
            var a = MakeRun("A");
            var b = MakeRun("B");
            var campaign = MakeCampaign(
                Node(a, CampaignUnlockMode.All, false, b),
                Node(b, CampaignUnlockMode.All, false, a));

            CollectionAssert.AreEquivalent(new[] { 0, 1 }, CampaignOps.GetUnreachableNodes(campaign));
        }

        [Test]
        public void GetUnreachableNodes_NodeDownstreamOfACycle_IsAlsoReported()
        {
            var a = MakeRun("A");
            var b = MakeRun("B");
            var downstream = MakeRun("C");
            var campaign = MakeCampaign(
                Node(a, CampaignUnlockMode.All, false, b),
                Node(b, CampaignUnlockMode.All, false, a),
                Node(downstream, CampaignUnlockMode.All, false, a));

            CollectionAssert.AreEquivalent(new[] { 0, 1, 2 }, CampaignOps.GetUnreachableNodes(campaign));
        }

        [Test]
        public void GetRootNodes_CampaignWithNoRoot_HasNowhereToBegin()
        {
            var a = MakeRun("A");
            var b = MakeRun("B");
            var campaign = MakeCampaign(
                Node(a, CampaignUnlockMode.All, false, b),
                Node(b, CampaignUnlockMode.All, false, a));

            CollectionAssert.IsEmpty(CampaignOps.GetRootNodes(campaign));
        }

        [Test]
        public void GetDuplicateRunKeys_SameRunTwice_IsReported()
        {
            var run = MakeRun("Twice");
            var campaign = MakeCampaign(Node(run), Node(run));

            CollectionAssert.AreEqual(new[] { "Twice" }, CampaignOps.GetDuplicateRunKeys(campaign));
        }

        [Test]
        public void GetNodesWithoutRun_EmptyNode_IsReported()
        {
            var campaign = MakeCampaign(Node(MakeRun("Real")), new CampaignNodeEntry());
            CollectionAssert.AreEqual(new[] { 1 }, CampaignOps.GetNodesWithoutRun(campaign));
        }

        [Test]
        public void GetNodesWithOutsidePrerequisites_RunNotInTheCampaign_IsReported()
        {
            var outsider = MakeRun("Outsider");
            var campaign = MakeCampaign(
                Node(MakeRun("Root")),
                Node(MakeRun("Needy"), CampaignUnlockMode.All, false, outsider));

            CollectionAssert.AreEqual(new[] { 1 }, CampaignOps.GetNodesWithOutsidePrerequisites(campaign));
        }
    }
}
