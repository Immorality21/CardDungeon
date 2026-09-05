using System.Collections.Generic;
using Assets.Scripts.Combat;
using Assets.Scripts.Heroes;
using Assets.Scripts.Heroes.UI;
using Assets.Scripts.UnitStats;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// The grid screen's decision layer (<see cref="SphereGridPresenter"/>): how nodes classify
    /// into UI states for a given bank, and how payloads read as text. The hub screen and the
    /// editor window's preview mode both call this one classifier, so these tests pin what the
    /// player sees in both places.
    /// </summary>
    public class SphereGridPresenterTests
    {
        /// <summary>start(10) — a(20) — b(30).</summary>
        private static SphereGridSO Chain()
        {
            var grid = ScriptableObject.CreateInstance<SphereGridSO>();
            grid.StartNodeKey = "start";
            grid.Nodes = new List<SphereGridNode>
            {
                new SphereGridNode { Key = "start", XpCost = 10, Neighbors = new List<string> { "a" } },
                new SphereGridNode { Key = "a", XpCost = 20, Neighbors = new List<string> { "b" } },
                new SphereGridNode { Key = "b", XpCost = 30 }
            };
            return grid;
        }

        [Test]
        public void ClassifyAll_FreshHeroWithAffordableStart_ReadsRight()
        {
            var states = SphereGridPresenter.ClassifyAll(Chain(), new List<string>(), 15);

            Assert.AreEqual(NodeUiState.Available, states["start"], "Reachable and affordable.");
            Assert.AreEqual(NodeUiState.Locked, states["a"],
                "The start node is not bought yet, so nothing has opened behind it.");
            Assert.AreEqual(NodeUiState.Locked, states["b"], "No activated neighbour yet.");
        }

        [Test]
        public void ClassifyAll_DefaultUnlock_ReadsActivatedAndOpensItsNeighbours()
        {
            var grid = Chain();
            SphereGridOps.FindNode(grid, "a").UnlockedByDefault = true;
            SphereGridOps.FindNode(grid, "a").XpCost = 0;

            var states = SphereGridPresenter.ClassifyAll(grid, new List<string>(), 30);

            Assert.AreEqual(NodeUiState.Activated, states["a"],
                "A default unlock is held, not offered - even with nothing in the save.");
            Assert.AreEqual(NodeUiState.Available, states["b"], "30 covers b, and a is active.");
        }

        [Test]
        public void Classify_UnpayableMaterials_ReadsAdjacentNotAvailable()
        {
            var grid = Chain();

            // Materials are the second half of the price, so failing them has to read exactly like
            // failing the XP half - wanted and reachable, but not yet payable.
            var affordable = SphereGridPresenter.Classify(
                grid, new List<string>(), 10, "start", _ => true);
            var unpayable = SphereGridPresenter.Classify(
                grid, new List<string>(), 10, "start", _ => false);

            Assert.AreEqual(NodeUiState.Available, affordable);
            Assert.AreEqual(NodeUiState.Adjacent, unpayable);
        }

        [Test]
        public void DescribeCost_SaysWhatEachKindOfNodeCosts()
        {
            var grid = Chain();
            var start = SphereGridOps.FindNode(grid, "start");
            var a = SphereGridOps.FindNode(grid, "a");

            Assert.AreEqual("Costs 10 XP", SphereGridPresenter.DescribeCost(start, false));
            Assert.AreEqual("Activated", SphereGridPresenter.DescribeCost(start, true));

            a.UnlockedByDefault = true;
            a.XpCost = 0;
            Assert.AreEqual("Known from the start — costs nothing",
                SphereGridPresenter.DescribeCost(a, true),
                "A node that was never for sale must not read as a purchase.");
        }

        [Test]
        public void DescribeMaterialCost_ListsEachLine_AndIsEmptyWithoutOne()
        {
            var node = new SphereGridNode { Key = "tip", XpCost = 350 };
            Assert.AreEqual("", SphereGridPresenter.DescribeMaterialCost(node));

            var iron = ScriptableObject.CreateInstance<Assets.Scripts.Items.ItemSO>();
            iron.Key = "EmberIron";
            iron.DisplayName = "Ember Iron";
            iron.Category = Assets.Scripts.Items.ItemCategory.Material;
            node.MaterialCosts = new List<Assets.Scripts.Items.MaterialCost>
            {
                new Assets.Scripts.Items.MaterialCost { Material = iron, Amount = 2 }
            };

            Assert.AreEqual("2 Ember Iron", SphereGridPresenter.DescribeMaterialCost(node));
            Assert.AreEqual("Costs 350 XP + 2 Ember Iron",
                SphereGridPresenter.DescribeCost(node, false));
        }

        [Test]
        public void ClassifyAll_ActivatedNodesReadActivated_AndOpenTheirNeighbours()
        {
            var states = SphereGridPresenter.ClassifyAll(Chain(), new List<string> { "start", "a" }, 30);

            Assert.AreEqual(NodeUiState.Activated, states["start"]);
            Assert.AreEqual(NodeUiState.Activated, states["a"]);
            Assert.AreEqual(NodeUiState.Available, states["b"], "a is activated and 30 covers b.");
        }

        [Test]
        public void StateClass_MapsEveryStateToItsUssClass()
        {
            Assert.AreEqual("sg-node--activated", SphereGridPresenter.StateClass(NodeUiState.Activated));
            Assert.AreEqual("sg-node--available", SphereGridPresenter.StateClass(NodeUiState.Available));
            Assert.AreEqual("sg-node--adjacent", SphereGridPresenter.StateClass(NodeUiState.Adjacent));
            Assert.AreEqual("sg-node--locked", SphereGridPresenter.StateClass(NodeUiState.Locked));
        }

        [Test]
        public void DescribePayload_StatNode_ListsNonZeroGains()
        {
            var node = new SphereGridNode
            {
                Key = "n",
                Gains = new StatBlock(new UnitStat(StatType.Strength, 2), new UnitStat(StatType.MaxHealth, 5))
            };

            string text = SphereGridPresenter.DescribePayload(node);

            StringAssert.Contains("+2 " + StatCatalog.ShortName(StatType.Strength), text);
            StringAssert.Contains("+5 " + StatCatalog.ShortName(StatType.MaxHealth), text);
        }

        [Test]
        public void DescribePayload_ResistanceAndSlotNodes()
        {
            var resist = new SphereGridNode
            {
                Key = "r",
                Kind = SphereNodeKind.Resistance,
                ResistType = DamageType.Fire,
                ResistPercent = 15f
            };
            var slot = new SphereGridNode { Key = "m", Kind = SphereNodeKind.MagicSlot };

            Assert.AreEqual("Fire resistance +15%", SphereGridPresenter.DescribePayload(resist));
            Assert.AreEqual("+1 magic slot (carry one more known spell)", SphereGridPresenter.DescribePayload(slot));
        }

        [Test]
        public void NodeName_PrefersDisplayName_FallsBackToPayload()
        {
            var named = new SphereGridNode { Key = "n", DisplayName = "Iron Skin", Kind = SphereNodeKind.MagicSlot };
            var unnamed = new SphereGridNode { Key = "m", Kind = SphereNodeKind.MagicSlot };

            Assert.AreEqual("Iron Skin", SphereGridPresenter.NodeName(named));
            Assert.AreEqual("+1 magic slot (carry one more known spell)", SphereGridPresenter.NodeName(unnamed));
        }

        [Test]
        public void Glyph_StartWinsOverKind()
        {
            var node = new SphereGridNode { Key = "n", Kind = SphereNodeKind.Resistance };

            Assert.AreEqual("★", SphereGridPresenter.Glyph(node, true));
            Assert.AreEqual("R", SphereGridPresenter.Glyph(node, false));
        }
    }
}
