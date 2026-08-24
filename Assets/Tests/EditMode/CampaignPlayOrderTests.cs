using System.Collections.Generic;
using Assets.Scripts.Dungeon;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// Campaign tiering and play order. These matter beyond the map screen: the balance analyzer
    /// walks runs in this order and hands each one the party its prerequisites left behind, so a
    /// wrong order silently mis-measures every downstream run's difficulty.
    /// </summary>
    public class CampaignPlayOrderTests
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

        private RunDefinitionSO MakeRun(string key)
        {
            var run = ScriptableObject.CreateInstance<RunDefinitionSO>();
            run.name = key;
            run.Key = key;
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

        private static CampaignNodeEntry Node(RunDefinitionSO run, params RunDefinitionSO[] requires)
        {
            return new CampaignNodeEntry
            {
                Run = run,
                Requires = new List<RunDefinitionSO>(requires),
                UnlockMode = CampaignUnlockMode.All
            };
        }

        private static List<string> KeysOf(List<CampaignNodeEntry> nodes)
        {
            var keys = new List<string>();
            foreach (var n in nodes)
            {
                keys.Add(CampaignOps.RunKeyOf(n.Run));
            }
            return keys;
        }

        [Test]
        public void ComputeTiers_ChainDeepensByOne()
        {
            var a = MakeRun("A");
            var b = MakeRun("B");
            var c = MakeRun("C");
            var campaign = MakeCampaign(Node(a), Node(b, a), Node(c, b));

            var tiers = CampaignOps.ComputeTiers(campaign);
            Assert.AreEqual(0, tiers["A"]);
            Assert.AreEqual(1, tiers["B"]);
            Assert.AreEqual(2, tiers["C"]);
        }

        [Test]
        public void ComputeTiers_SiblingBranches_ShareATier()
        {
            var root = MakeRun("Root");
            var left = MakeRun("Left");
            var right = MakeRun("Right");
            var campaign = MakeCampaign(Node(root), Node(left, root), Node(right, root));

            var tiers = CampaignOps.ComputeTiers(campaign);
            Assert.AreEqual(1, tiers["Left"]);
            Assert.AreEqual(1, tiers["Right"]);
        }

        [Test]
        public void ComputeTiers_AnyModeRejoin_OpensOffItsShallowestPrerequisite()
        {
            // Any mode is how a branch rejoins: either route opens it, so it sits one past whichever
            // route is shorter - that is the tier the player can actually arrive at it on.
            var root = MakeRun("Root");
            var shortcut = MakeRun("Shortcut");
            var long1 = MakeRun("Long1");
            var long2 = MakeRun("Long2");
            var rejoin = MakeRun("Rejoin");
            var node = Node(rejoin, shortcut, long2);
            node.UnlockMode = CampaignUnlockMode.Any;
            var campaign = MakeCampaign(
                Node(root), Node(shortcut, root), Node(long1, root), Node(long2, long1), node);

            var tiers = CampaignOps.ComputeTiers(campaign);
            Assert.AreEqual(2, tiers["Rejoin"], "one past Shortcut (tier 1), not past Long2 (tier 2)");
        }

        [Test]
        public void ComputeTiers_AllModeRejoin_WaitsForItsDeepestPrerequisite()
        {
            // The shape the campaign is built for: a short branch and a long one both feeding a
            // rejoin. The rejoin is only reachable once the *deepest* path is done.
            var root = MakeRun("Root");
            var shortcut = MakeRun("Shortcut");
            var long1 = MakeRun("Long1");
            var long2 = MakeRun("Long2");
            var rejoin = MakeRun("Rejoin");
            var campaign = MakeCampaign(
                Node(root),
                Node(shortcut, root),
                Node(long1, root),
                Node(long2, long1),
                Node(rejoin, shortcut, long2));

            var tiers = CampaignOps.ComputeTiers(campaign);
            Assert.AreEqual(1, tiers["Shortcut"]);
            Assert.AreEqual(2, tiers["Long2"]);
            Assert.AreEqual(3, tiers["Rejoin"], "All mode waits for the slowest route");
        }

        [Test]
        public void ComputeTiers_PrerequisiteCycle_Terminates()
        {
            var a = MakeRun("A");
            var b = MakeRun("B");
            var campaign = MakeCampaign(Node(a, b), Node(b, a));

            var tiers = CampaignOps.ComputeTiers(campaign);
            Assert.AreEqual(0, tiers["A"], "a node in a cycle never resolves, so it stays at tier 0");
            Assert.AreEqual(0, tiers["B"]);
            CollectionAssert.AreEquivalent(new[] { 0, 1 }, CampaignOps.GetUnreachableNodes(campaign),
                "and the cycle is reported as the authoring fault it is");
        }

        [Test]
        public void GetNodesInPlayOrder_NeverPlacesARunBeforeItsPrerequisite()
        {
            // Authored deliberately backwards, so only real ordering can pass this.
            var root = MakeRun("Root");
            var mid = MakeRun("Mid");
            var deep = MakeRun("Deep");
            var campaign = MakeCampaign(Node(deep, mid), Node(mid, root), Node(root));

            var order = KeysOf(CampaignOps.GetNodesInPlayOrder(campaign));
            CollectionAssert.AreEqual(new[] { "Root", "Mid", "Deep" }, order);
        }

        [Test]
        public void GetNodesInPlayOrder_TiesKeepAuthoredOrder()
        {
            var root = MakeRun("Root");
            var first = MakeRun("First");
            var second = MakeRun("Second");
            var campaign = MakeCampaign(Node(root), Node(first, root), Node(second, root));

            var order = KeysOf(CampaignOps.GetNodesInPlayOrder(campaign));
            CollectionAssert.AreEqual(new[] { "Root", "First", "Second" }, order);
        }

        [Test]
        public void GetNodesInPlayOrder_SkipsNodesWithNoRun()
        {
            var root = MakeRun("Root");
            var campaign = MakeCampaign(Node(root), new CampaignNodeEntry());

            Assert.AreEqual(1, CampaignOps.GetNodesInPlayOrder(campaign).Count);
        }
    }
}
