using System.Collections.Generic;
using Assets.Scripts.Combat;
using Assets.Scripts.Heroes;
using Assets.Scripts.UnitStats;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// The sphere-grid rules (<see cref="SphereGridOps"/>): adjacency and reachability, the
    /// activation gate, the save-entry spend, node-grant aggregation, the deterministic greedy
    /// budget spend the balance model runs on, and the recruit starter bank. All pure — grids are
    /// built in memory via ScriptableObject.CreateInstance, no assets and no scene.
    /// </summary>
    public class SphereGridOpsTests
    {
        // --- fixtures ------------------------------------------------------

        private static SphereGridSO Grid(string startKey, params SphereGridNode[] nodes)
        {
            var grid = ScriptableObject.CreateInstance<SphereGridSO>();
            grid.StartNodeKey = startKey;
            grid.Nodes = new List<SphereGridNode>(nodes);
            return grid;
        }

        private static SphereGridNode Node(string key, int cost, params string[] neighbors)
        {
            return new SphereGridNode
            {
                Key = key,
                XpCost = cost,
                Neighbors = new List<string>(neighbors)
            };
        }

        /// <summary>start(10) — a(20) — b(30), plus island(5) with no edges.</summary>
        private static SphereGridSO Chain()
        {
            return Grid("start",
                Node("start", 10, "a"),
                Node("a", 20, "b"),   // edge to start is implied by start's own list
                Node("b", 30),
                Node("island", 5));
        }

        // --- adjacency -------------------------------------------------------

        [Test]
        public void BuildAdjacency_SymmetrizesUnion_DropsDanglingAndDuplicates()
        {
            // "a" is listed on start but does not list start back; "a" lists "b" twice and one
            // key that matches nothing.
            var grid = Grid("start",
                Node("start", 10, "a"),
                Node("a", 20, "b", "b", "no-such-node"),
                Node("b", 30));

            var adjacency = SphereGridOps.BuildAdjacency(grid);

            Assert.AreEqual(new List<string> { "a" }, adjacency["start"]);
            Assert.AreEqual(new List<string> { "start", "b" }, adjacency["a"]);
            Assert.AreEqual(new List<string> { "a" }, adjacency["b"]);
        }

        // --- reachability ----------------------------------------------------

        [Test]
        public void IsReachable_StartNode_NeedsNoNeighbour()
        {
            Assert.IsTrue(SphereGridOps.IsReachable(Chain(), new List<string>(), "start"),
                "A fresh hero has to have somewhere to begin.");
        }

        [Test]
        public void IsReachable_NeighbourOfAnUnboughtStart_False()
        {
            // The bug this pins: adjacency to the *start node* used to grant reachability whether or
            // not the start had been bought, so a new hero's second node was purchasable while the
            // first still read as unbought - and the entry node could be skipped entirely.
            var grid = Chain();

            Assert.IsFalse(SphereGridOps.IsReachable(grid, new List<string>(), "a"),
                "The start node is not bought yet, so nothing grows out of it.");
            Assert.IsTrue(SphereGridOps.IsReachable(grid, new List<string> { "start" }, "a"),
                "Buying the start node opens its neighbours.");
        }

        [Test]
        public void IsReachable_BeyondTheFrontier_False()
        {
            var grid = Chain();

            Assert.IsFalse(SphereGridOps.IsReachable(grid, new List<string>(), "b"));
            Assert.IsTrue(SphereGridOps.IsReachable(grid, new List<string> { "a" }, "b"),
                "Activating the node between them opens it.");
        }

        [Test]
        public void IsReachable_IslandNode_False()
        {
            var grid = Chain();

            Assert.IsFalse(SphereGridOps.IsReachable(grid, new List<string> { "start", "a", "b" }, "island"));
        }

        // --- CanActivate -------------------------------------------------------

        [Test]
        public void CanActivate_RejectsActivated_Unreachable_Unaffordable()
        {
            var grid = Chain();

            Assert.IsFalse(SphereGridOps.CanActivate(grid, new List<string> { "a" }, 100, "a"),
                "Already activated.");
            Assert.IsFalse(SphereGridOps.CanActivate(grid, new List<string>(), 100, "b"),
                "Not reachable yet.");
            Assert.IsFalse(SphereGridOps.CanActivate(grid, new List<string>(), 19, "a"),
                "Bank does not cover the cost.");
            Assert.IsFalse(SphereGridOps.CanActivate(grid, new List<string>(), 100, "no-such-node"));
        }

        [Test]
        public void CanActivate_ExactBank_True()
        {
            Assert.IsTrue(SphereGridOps.CanActivate(Chain(), new List<string> { "start" }, 20, "a"));
        }

        // --- default unlocks ---------------------------------------------------

        /// <summary>start(10) — free(20) — b(30): "free" is handed over, never bought.</summary>
        private static SphereGridSO ChainWithDefault()
        {
            var free = Node("free", 20, "b");
            free.UnlockedByDefault = true;
            free.Gains = new StatBlock(new UnitStat(StatType.Strength, 4));
            return Grid("start", Node("start", 10, "free"), free, Node("b", 30));
        }

        [Test]
        public void ActiveNodes_FoldsDefaultsIntoTheSavedList_WithoutDuplicating()
        {
            var grid = ChainWithDefault();

            Assert.AreEqual(new List<string> { "free" }, SphereGridOps.ActiveNodes(grid, null),
                "A save with no activations still holds what the grid gives away.");
            Assert.AreEqual(new List<string> { "start", "free" },
                SphereGridOps.ActiveNodes(grid, new List<string> { "start" }));
            Assert.AreEqual(new List<string> { "free" },
                SphereGridOps.ActiveNodes(grid, new List<string> { "free" }),
                "A node bought before it was marked default must not appear twice.");
        }

        [Test]
        public void DefaultUnlockedNode_GrantsItsPayloadOnAnEmptySave()
        {
            var stats = SphereGridOps.StatsForNodes(ChainWithDefault(), new List<string>());

            Assert.AreEqual(4, stats[StatType.Strength]);
        }

        [Test]
        public void DefaultUnlockedNode_OpensItsNeighbours_AndIsNeverForSale()
        {
            var grid = ChainWithDefault();
            var none = new List<string>();

            Assert.IsTrue(SphereGridOps.IsReachable(grid, none, "b"),
                "The default unlock is active, so what it touches is on the frontier.");
            Assert.IsFalse(SphereGridOps.CanActivate(grid, none, 999, "free"),
                "Already active - buying it again would charge for nothing.");
        }

        [Test]
        public void DefaultUnlockedMagic_IsKnownWithNothingActivated()
        {
            var known = Node("known", 20);
            known.Kind = SphereNodeKind.MagicKnown;
            known.GrantedMagicKey = "Slash";
            known.GrantedCharges = 2;
            known.UnlockedByDefault = true;
            var grid = Grid("start", Node("start", 10, "known"), known);

            var granted = SphereGridOps.KnownMagicForNodes(grid, new List<string>());

            Assert.AreEqual(1, granted.Count);
            Assert.AreEqual("Slash", granted[0].Key);
            Assert.AreEqual(2, granted[0].Value);
        }

        [Test]
        public void TotalGridCost_ExcludesDefaultUnlocks()
        {
            // start(10) + b(30); free(20) is given away, so it is not part of the price.
            Assert.AreEqual(40, SphereGridOps.TotalGridCost(ChainWithDefault()));
        }

        // --- material prices ---------------------------------------------------

        [Test]
        public void HasMaterialCost_IgnoresEmptyAndUnauthoredLines()
        {
            var node = Node("node", 10);
            Assert.IsFalse(SphereGridOps.HasMaterialCost(node), "No lines at all.");

            node.MaterialCosts = new List<Assets.Scripts.Items.MaterialCost>
            {
                null,
                new Assets.Scripts.Items.MaterialCost { Material = null, Amount = 3 }
            };
            Assert.IsFalse(SphereGridOps.HasMaterialCost(node),
                "A line with no material authored is a half-edited price, not a price.");
        }

        // --- TryActivate (save-entry spend) -------------------------------------

        [Test]
        public void TryActivate_SpendsBankAndAppends()
        {
            var entry = new HeroSaveData
            {
                HeroKey = "hero",
                CurrentXp = 25,
                ActivatedNodes = new List<string> { "start" }
            };

            bool activated = SphereGridOps.TryActivate(Chain(), entry, "a");

            Assert.IsTrue(activated);
            Assert.AreEqual(5, entry.CurrentXp);
            Assert.AreEqual(new List<string> { "start", "a" }, entry.ActivatedNodes);
        }

        [Test]
        public void TryActivate_FailureLeavesEntryUntouched()
        {
            var entry = new HeroSaveData { HeroKey = "hero", CurrentXp = 25 };

            bool activated = SphereGridOps.TryActivate(Chain(), entry, "b");

            Assert.IsFalse(activated, "b is beyond the frontier.");
            Assert.AreEqual(25, entry.CurrentXp);
            Assert.IsEmpty(entry.ActivatedNodes);
        }

        // --- aggregation ---------------------------------------------------------

        [Test]
        public void StatsForNodes_SumsGains_IncludingTheSameStatTwice()
        {
            var one = Node("one", 10);
            one.Gains = new StatBlock(new UnitStat(StatType.Strength, 2), new UnitStat(StatType.MaxHealth, 3));
            var two = Node("two", 10);
            two.Gains = new StatBlock(new UnitStat(StatType.Strength, 1));
            var grid = Grid("one", one, two);

            var stats = SphereGridOps.StatsForNodes(grid, new List<string> { "one", "two" });

            Assert.AreEqual(3, stats[StatType.Strength]);
            Assert.AreEqual(3, stats[StatType.MaxHealth]);
        }

        [Test]
        public void ResistancesForNodes_SumsPerDamageType()
        {
            var fire1 = Node("fire1", 10);
            fire1.Kind = SphereNodeKind.Resistance;
            fire1.ResistType = DamageType.Fire;
            fire1.ResistPercent = 15f;
            var fire2 = Node("fire2", 10);
            fire2.Kind = SphereNodeKind.Resistance;
            fire2.ResistType = DamageType.Fire;
            fire2.ResistPercent = 10f;
            var ice = Node("ice", 10);
            ice.Kind = SphereNodeKind.Resistance;
            ice.ResistType = DamageType.Ice;
            ice.ResistPercent = 20f;
            var grid = Grid("fire1", fire1, fire2, ice);

            var resistances = SphereGridOps.ResistancesForNodes(
                grid, new List<string> { "fire1", "fire2", "ice" });

            Assert.AreEqual(2, resistances.Count);
            Assert.AreEqual(25f, resistances.Find(r => r.DamageType == DamageType.Fire).Percent);
            Assert.AreEqual(20f, resistances.Find(r => r.DamageType == DamageType.Ice).Percent);
        }

        [Test]
        public void SlotBonusForNodes_CountsSlotNodesOnly()
        {
            var stat = Node("stat", 10);
            var slot1 = Node("slot1", 10);
            slot1.Kind = SphereNodeKind.MagicSlot;
            var slot2 = Node("slot2", 10);
            slot2.Kind = SphereNodeKind.MagicSlot;
            var grid = Grid("stat", stat, slot1, slot2);

            Assert.AreEqual(2, SphereGridOps.SlotBonusForNodes(
                grid, new List<string> { "stat", "slot1", "slot2" }));
        }

        // --- cost + sanitize -----------------------------------------------------

        [Test]
        public void TotalCostOf_IgnoresUnknownKeys_AndCountsEachOnce()
        {
            var grid = Chain();

            int total = SphereGridOps.TotalCostOf(
                grid, new List<string> { "start", "a", "a", "no-such-node" });

            Assert.AreEqual(30, total);
        }

        [Test]
        public void SanitizeActivated_DropsUnknownAndDupes_KeepsUnreachablePaidNodes()
        {
            var grid = Chain();

            // "island" is unreachable but exists — a paid grant must survive a re-authored grid.
            var sanitized = SphereGridOps.SanitizeActivated(
                grid, new List<string> { "island", "a", "a", "gone" });

            Assert.AreEqual(new List<string> { "island", "a" }, sanitized);
        }

        // --- greedy budget spend ---------------------------------------------------

        [Test]
        public void GreedySpend_BuysCheapestFrontierFirst()
        {
            // start(10) opens cheap(15) and dear(40); cheap opens tail(20).
            var grid = Grid("start",
                Node("start", 10, "cheap", "dear"),
                Node("cheap", 15, "tail"),
                Node("dear", 40),
                Node("tail", 20));

            var activated = SphereGridOps.GreedySpend(grid, null, 45, out int spent);

            Assert.AreEqual(new List<string> { "start", "cheap", "tail" }, activated,
                "10 + 15 + 20 = 45 spends the whole budget before dear(40) is ever affordable.");
            Assert.AreEqual(45, spent);
        }

        [Test]
        public void GreedySpend_TieBreaksByNodeIndex()
        {
            // Two frontier nodes at the same price: the one earlier in Nodes wins.
            var grid = Grid("start",
                Node("start", 10, "first", "second"),
                Node("first", 20),
                Node("second", 20));

            var activated = SphereGridOps.GreedySpend(grid, null, 30, out _);

            Assert.AreEqual(new List<string> { "start", "first" }, activated);
        }

        [Test]
        public void GreedySpend_IsDeterministic()
        {
            var first = SphereGridOps.GreedySpend(Chain(), null, 60, out int spentFirst);
            var second = SphereGridOps.GreedySpend(Chain(), null, 60, out int spentSecond);

            Assert.AreEqual(first, second);
            Assert.AreEqual(spentFirst, spentSecond);
        }

        [Test]
        public void GreedySpend_StopsWhenFrontierUnaffordable_SpentIsExact()
        {
            var grid = Chain();

            var activated = SphereGridOps.GreedySpend(grid, null, 35, out int spent);

            Assert.AreEqual(new List<string> { "start", "a" }, activated,
                "10 + 20 fits in 35; b costs 30 and only 5 remains.");
            Assert.AreEqual(30, spent, "Spent is what was actually consumed, not the budget.");
        }

        [Test]
        public void GreedySpend_RespectsAlreadyActivated()
        {
            var grid = Chain();

            var activated = SphereGridOps.GreedySpend(grid, new List<string> { "start", "a" }, 30, out int spent);

            Assert.AreEqual(new List<string> { "start", "a", "b" }, activated);
            Assert.AreEqual(30, spent, "Only the new activation draws on the budget.");
        }

        // --- recruit seeding ---------------------------------------------------------

        [Test]
        public void LifetimeXpFor_IsBankPlusSpent()
        {
            var entry = new HeroSaveData
            {
                HeroKey = "hero",
                CurrentXp = 40,
                ActivatedNodes = new List<string> { "start", "a" }
            };

            Assert.AreEqual(70, SphereGridOps.LifetimeXpFor(Chain(), entry));
        }

        [Test]
        public void StarterBank_IsRateTimesAverage_FloorRounded()
        {
            // Average of 100 and 201 is 150.5; 55% of that is 82.775 → floors to 82.
            Assert.AreEqual(82, SphereGridOps.StarterBank(new List<int> { 100, 201 }));
        }

        [Test]
        public void StarterBank_EmptyRoster_Zero()
        {
            Assert.AreEqual(0, SphereGridOps.StarterBank(new List<int>()));
            Assert.AreEqual(0, SphereGridOps.StarterBank(null));
        }

        // --- save-format contract ------------------------------------------------------

        [Test]
        public void PreGridSaveJson_DeserializesWithEmptyActivatedNodes()
        {
            // A Party.json written before ActivatedNodes existed. FromJsonOverwrite must leave the
            // field initializer's empty list in place — this pins the no-migration-code contract.
            var entry = new HeroSaveData();
            JsonUtility.FromJsonOverwrite("{\"HeroKey\":\"Warrior\",\"CurrentXp\":168}", entry);

            Assert.AreEqual("Warrior", entry.HeroKey);
            Assert.AreEqual(168, entry.CurrentXp);
            Assert.IsNotNull(entry.ActivatedNodes);
            Assert.IsEmpty(entry.ActivatedNodes);
        }

        // --- BaseStatsForNodes (the balance-model seam) -----------------------------------

        [Test]
        public void HeroStatCalculator_BaseStatsForNodes_AppliesEveryActivatedGain()
        {
            var hero = ScriptableObject.CreateInstance<HeroSO>();
            hero.BaseStats = new StatBlock(
                new UnitStat(StatType.Strength, 5),
                new UnitStat(StatType.MaxHealth, 40));

            var one = Node("one", 10);
            one.Gains = new StatBlock(new UnitStat(StatType.Strength, 2), new UnitStat(StatType.MaxHealth, 10));
            var two = Node("two", 10, "one");
            two.Gains = new StatBlock(new UnitStat(StatType.Strength, 3), new UnitStat(StatType.MaxHealth, 10));
            hero.SphereGrid = Grid("one", one, two);

            var stats = Assets.Scripts.Balance.HeroStatCalculator.BaseStatsForNodes(
                hero, new List<string> { "one", "two" });

            Assert.AreEqual(10, stats[StatType.Strength]);
            Assert.AreEqual(60, stats.MaxHealth);
            Assert.AreEqual(stats.MaxHealth, stats.Health,
                "A freshly derived hero should start at full health.");
        }
    }
}
