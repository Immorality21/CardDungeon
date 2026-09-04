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
            Assert.AreEqual(NodeUiState.Adjacent, states["a"], "Reachable (start's neighbour) but 20 > 15.");
            Assert.AreEqual(NodeUiState.Locked, states["b"], "No activated neighbour yet.");
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
