using System.Collections.Generic;
using Assets.Scripts.Heroes;
using Assets.Scripts.Items;
using Assets.Scripts.Tutorial;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// The tutorial's rules (<see cref="TutorialOps"/>): which step a save is on, what each step locks
    /// and points at, and the node it walks the player to. Pure — no scene, no save files.
    /// </summary>
    public class TutorialOpsTests
    {
        private const string Hall = "sphere-hall";

        private static TutorialProgress Fresh()
        {
            return new TutorialProgress { Started = true };
        }

        // --- the step -----------------------------------------------------------

        [Test]
        public void CurrentStep_NeverStarted_IsNone()
        {
            Assert.AreEqual(TutorialStep.None, TutorialOps.CurrentStep(new TutorialProgress()));
        }

        [Test]
        public void CurrentStep_Finished_IsNone()
        {
            var p = Fresh();
            p.Finished = true;
            Assert.AreEqual(TutorialStep.None, TutorialOps.CurrentStep(p));
        }

        [Test]
        public void CurrentStep_WalksTheLoopInOrder()
        {
            var p = Fresh();
            Assert.AreEqual(TutorialStep.ClearFirstFloor, TutorialOps.CurrentStep(p));

            p.FirstFloorCleared = true;
            p.CanAffordGuide = true;
            p.HeroCanAffordNode = true;
            Assert.AreEqual(TutorialStep.BuildHall, TutorialOps.CurrentStep(p));

            p.GuideBuilt = true;
            Assert.AreEqual(TutorialStep.SpendXp, TutorialOps.CurrentStep(p));

            p.HeroHasSpent = true;
            Assert.AreEqual(TutorialStep.TakeTheRoad, TutorialOps.CurrentStep(p));
        }

        /// <summary>A locked town with nothing affordable to click is a soft-lock, so the step is skipped.</summary>
        [Test]
        public void CurrentStep_HallUnaffordable_SkipsToTheRoad()
        {
            var p = Fresh();
            p.FirstFloorCleared = true;
            p.HeroCanAffordNode = true;
            Assert.AreEqual(TutorialStep.TakeTheRoad, TutorialOps.CurrentStep(p));
        }

        [Test]
        public void CurrentStep_NoAffordableNode_SkipsToTheRoad()
        {
            var p = Fresh();
            p.FirstFloorCleared = true;
            p.GuideBuilt = true;
            Assert.AreEqual(TutorialStep.TakeTheRoad, TutorialOps.CurrentStep(p));
        }

        [Test]
        public void FirstFloorCleared_ReadsTheRunSaveAndCompletedRuns()
        {
            Assert.IsFalse(TutorialOps.FirstFloorCleared("T", null, null, 0), "fresh save");
            Assert.IsFalse(TutorialOps.FirstFloorCleared("T", null, "T", 0), "still on floor 1");
            Assert.IsTrue(TutorialOps.FirstFloorCleared("T", null, "T", 1), "moved on to floor 2");
            Assert.IsFalse(TutorialOps.FirstFloorCleared("T", null, "Other", 3), "a different run");
            Assert.IsTrue(TutorialOps.FirstFloorCleared("T", new List<string> { "T" }, null, 0), "run finished");
            Assert.IsFalse(TutorialOps.FirstFloorCleared(null, new List<string> { "T" }, "T", 1), "no tutorial run");
        }

        // --- locks and pointers ------------------------------------------------------

        [Test]
        public void LockedSteps_OnlyTheGuideLotIsClickable()
        {
            foreach (var step in new[] { TutorialStep.BuildHall, TutorialStep.SpendXp })
            {
                Assert.IsTrue(TutorialOps.AllowsLot(step, Hall, Hall), step.ToString());
                Assert.IsFalse(TutorialOps.AllowsLot(step, "merchant", Hall), step.ToString());
                Assert.IsTrue(TutorialOps.GuidesLot(step, Hall, Hall), step.ToString());
                Assert.IsFalse(TutorialOps.AllowsRoad(step), step.ToString());
                Assert.IsFalse(TutorialOps.AllowsLeaving(step), step.ToString());
                Assert.IsTrue(TutorialOps.LocksTown(step), step.ToString());
            }
        }

        [Test]
        public void ClearFirstFloor_OnlyTheRoad()
        {
            Assert.IsFalse(TutorialOps.AllowsLot(TutorialStep.ClearFirstFloor, Hall, Hall));
            Assert.IsTrue(TutorialOps.AllowsRoad(TutorialStep.ClearFirstFloor));
            Assert.IsTrue(TutorialOps.GuidesRoad(TutorialStep.ClearFirstFloor));
            Assert.IsTrue(TutorialOps.LocksTown(TutorialStep.ClearFirstFloor));
        }

        [Test]
        public void AfterTheLoop_EverythingOpens_AndTheRoadIsPointedAt()
        {
            foreach (var step in new[] { TutorialStep.TakeTheRoad, TutorialStep.None })
            {
                Assert.IsTrue(TutorialOps.AllowsLot(step, "merchant", Hall), step.ToString());
                Assert.IsFalse(TutorialOps.GuidesLot(step, Hall, Hall), step.ToString());
                Assert.IsTrue(TutorialOps.AllowsRoad(step), step.ToString());
                Assert.IsTrue(TutorialOps.AllowsLeaving(step), step.ToString());
                Assert.IsFalse(TutorialOps.LocksTown(step), step.ToString());
            }
            Assert.IsTrue(TutorialOps.GuidesRoad(TutorialStep.TakeTheRoad));
            Assert.IsFalse(TutorialOps.GuidesRoad(TutorialStep.None));
        }

        [Test]
        public void CueFor_GridSplitsPickFromActivate()
        {
            Assert.AreEqual(TutorialCue.PickNode, TutorialOps.CueFor(TutorialStep.SpendXp, TutorialScreen.Grid, false));
            Assert.AreEqual(TutorialCue.ActivateNode, TutorialOps.CueFor(TutorialStep.SpendXp, TutorialScreen.Grid, true));
            Assert.AreEqual(TutorialCue.NodeLearned, TutorialOps.CueFor(TutorialStep.TakeTheRoad, TutorialScreen.Grid, false));
        }

        [Test]
        public void CueFor_NothingSaidOnUnguidedScreens_OrWithNoTutorial()
        {
            Assert.AreEqual(TutorialCue.None, TutorialOps.CueFor(TutorialStep.TakeTheRoad, TutorialScreen.Other, false));
            Assert.AreEqual(TutorialCue.None, TutorialOps.CueFor(TutorialStep.None, TutorialScreen.Town, false));
            Assert.AreEqual(TutorialCue.None, TutorialOps.CueFor(TutorialStep.BuildHall, TutorialScreen.Grid, false));
        }

        // --- the guided node -----------------------------------------------------------

        private static SphereGridSO Grid(params SphereGridNode[] nodes)
        {
            var grid = ScriptableObject.CreateInstance<SphereGridSO>();
            grid.StartNodeKey = "start";
            grid.Nodes = new List<SphereGridNode>(nodes);
            return grid;
        }

        private static SphereGridNode Node(string key, int cost, params string[] neighbors)
        {
            return new SphereGridNode { Key = key, XpCost = cost, Neighbors = new List<string>(neighbors) };
        }

        [Test]
        public void GuidedNodeKey_PicksTheCheapestAffordableFrontierNode()
        {
            var sig = Node("sig", 0, "trunk");
            sig.UnlockedByDefault = true;
            var grid = Grid(Node("start", 15, "sig"), sig, Node("trunk", 10));

            // start (15) and trunk (10, opened by the default) are both on the frontier.
            Assert.AreEqual("trunk", TutorialOps.GuidedNodeKey(grid, new List<string>(), 20));
        }

        [Test]
        public void GuidedNodeKey_NothingAffordable_IsNull()
        {
            var grid = Grid(Node("start", 15, "a"), Node("a", 20));
            Assert.IsNull(TutorialOps.GuidedNodeKey(grid, new List<string>(), 14));
            Assert.IsNull(TutorialOps.GuidedNodeKey(null, null, 100));
        }

        /// <summary>The tutorial promises an XP spend, so a node that also asks for materials is passed over.</summary>
        [Test]
        public void GuidedNodeKey_SkipsNodesThatCostMaterials()
        {
            var material = ScriptableObject.CreateInstance<ItemSO>();
            material.Category = ItemCategory.Material;
            var priced = Node("start", 5, "a");
            priced.MaterialCosts = new List<MaterialCost> { new MaterialCost { Material = material, Amount = 1 } };
            var grid = Grid(priced, Node("a", 10));

            Assert.IsNull(TutorialOps.GuidedNodeKey(grid, new List<string>(), 100));
        }
    }
}
