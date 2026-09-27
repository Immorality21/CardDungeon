using System.Collections.Generic;
using System.IO;
using Assets.Scripts.Cards;
using Assets.Scripts.Heroes;
using Assets.Scripts.IO;
using Assets.Scripts.Sandbox;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// The sandbox's pure half: <see cref="SphereGridOps.PathTo"/>, the save entries
    /// <see cref="SandboxSetup"/> builds from a config, and the <see cref="FileHandler"/> folder
    /// override that keeps a sandbox session off the player's save.
    /// </summary>
    public class SandboxSetupTests
    {
        // start — a — b        (branch A, b teaches "bolt")
        //   \
        //    c — d             (branch B)
        // island               (no edges)
        private static SphereGridSO TwoBranches()
        {
            var grid = ScriptableObject.CreateInstance<SphereGridSO>();
            grid.StartNodeKey = "start";
            grid.Nodes = new List<SphereGridNode>
            {
                Node("start", 10, "a", "c"),
                Node("a", 20, "b"),
                Magic("b", 40, "bolt"),
                Node("c", 15, "d"),
                Node("d", 30),
                Node("island", 5),
            };
            return grid;
        }

        private static SphereGridNode Node(string key, int cost, params string[] neighbors)
        {
            return new SphereGridNode { Key = key, XpCost = cost, Neighbors = new List<string>(neighbors) };
        }

        private static SphereGridNode Magic(string key, int cost, string magicKey)
        {
            var node = Node(key, cost);
            node.Kind = SphereNodeKind.MagicKnown;
            node.GrantedMagicKey = magicKey;
            node.GrantedCharges = 2;
            return node;
        }

        private static HeroSO Hero(string key, SphereGridSO grid)
        {
            var hero = ScriptableObject.CreateInstance<HeroSO>();
            hero.Key = key;
            hero.SphereGrid = grid;
            return hero;
        }

        private static MagicSO Spell(string key)
        {
            var magic = ScriptableObject.CreateInstance<MagicSO>();
            magic.Key = key;
            return magic;
        }

        // --- PathTo ----------------------------------------------------------

        [Test]
        public void PathTo_DeepNode_WalksFromTheStartInOrder()
        {
            CollectionAssert.AreEqual(new[] { "start", "a", "b" }, SphereGridOps.PathTo(TwoBranches(), "b"));
        }

        [Test]
        public void PathTo_TheStartNode_IsJustTheStart()
        {
            CollectionAssert.AreEqual(new[] { "start" }, SphereGridOps.PathTo(TwoBranches(), "start"));
        }

        [TestCase("island")]
        [TestCase("no-such-node")]
        [TestCase("")]
        public void PathTo_UnreachableOrMissing_IsEmpty(string target)
        {
            Assert.IsEmpty(SphereGridOps.PathTo(TwoBranches(), target));
        }

        [Test]
        public void PathTo_EdgeListedOnlyOnTheFarNode_StillWalks()
        {
            // Neighbours are undirected: "a" lists "b", and b lists nothing back.
            var path = SphereGridOps.PathTo(TwoBranches(), "b");
            Assert.AreEqual("a", path[1]);
        }

        // --- BuildHeroSave ---------------------------------------------------

        [Test]
        public void BuildHeroSave_UnlockPathTo_ActivatesThatBranchOnly()
        {
            var setup = new SandboxHeroSetup { Hero = Hero("H", TwoBranches()), UnlockPathTo = { "b" } };

            var entry = SandboxSetup.BuildHeroSave(setup, new List<string>());

            CollectionAssert.AreEqual(new[] { "start", "a", "b" }, entry.ActivatedNodes);
        }

        [Test]
        public void BuildHeroSave_TwoTargets_SharedTrunkIsListedOnce()
        {
            var setup = new SandboxHeroSetup { Hero = Hero("H", TwoBranches()), UnlockPathTo = { "b", "d" } };

            var entry = SandboxSetup.BuildHeroSave(setup, new List<string>());

            CollectionAssert.AreEqual(new[] { "start", "a", "b", "c", "d" }, entry.ActivatedNodes);
        }

        [Test]
        public void BuildHeroSave_UnknownTarget_IsReportedNotThrown()
        {
            var problems = new List<string>();
            var setup = new SandboxHeroSetup { Hero = Hero("H", TwoBranches()), UnlockPathTo = { "typo" } };

            var entry = SandboxSetup.BuildHeroSave(setup, problems);

            Assert.IsEmpty(entry.ActivatedNodes);
            Assert.AreEqual(1, problems.Count);
            StringAssert.Contains("typo", problems[0]);
        }

        [Test]
        public void BuildHeroSave_EntireGrid_ActivatesEveryReachableNode()
        {
            var setup = new SandboxHeroSetup { Hero = Hero("H", TwoBranches()), UnlockEntireGrid = true };

            var entry = SandboxSetup.BuildHeroSave(setup, new List<string>());

            CollectionAssert.AreEquivalent(new[] { "start", "a", "b", "c", "d" }, entry.ActivatedNodes);
            Assert.AreEqual("start", entry.ActivatedNodes[0], "Depth order, like a real walk.");
        }

        [Test]
        public void BuildHeroSave_SpendXp_BuysOnTopOfTheForcedPath()
        {
            // Path to "a" is free; 15 XP then buys "c" (the cheapest frontier node), not "b" (40).
            var setup = new SandboxHeroSetup
            {
                Hero = Hero("H", TwoBranches()),
                UnlockPathTo = { "a" },
                SpendXpOnGrid = 15
            };

            var entry = SandboxSetup.BuildHeroSave(setup, new List<string>());

            CollectionAssert.AreEqual(new[] { "start", "a", "c" }, entry.ActivatedNodes);
        }

        [Test]
        public void BuildHeroSave_BankedXp_IsTheUnspentBank()
        {
            var setup = new SandboxHeroSetup { Hero = Hero("H", TwoBranches()), BankedXp = 1000, SpendXpOnGrid = 25 };

            var entry = SandboxSetup.BuildHeroSave(setup, new List<string>());

            Assert.AreEqual(1000, entry.CurrentXp, "The spend budget is separate from what is left in the bank.");
            Assert.AreEqual("H", entry.HeroKey);
        }

        // --- Party + loadout -------------------------------------------------

        [Test]
        public void BuildPartySave_OwnsAndSelectsInOrder_AndReportsDuplicates()
        {
            var first = Hero("First", TwoBranches());
            var second = Hero("Second", TwoBranches());
            var problems = new List<string>();

            var save = SandboxSetup.BuildPartySave(new[]
            {
                new SandboxHeroSetup { Hero = first },
                new SandboxHeroSetup { Hero = second },
                new SandboxHeroSetup { Hero = first, BankedXp = 99 },
                null,
            }, problems);

            CollectionAssert.AreEqual(new[] { "First", "Second" }, save.SelectedHeroKeys);
            CollectionAssert.AreEqual(new[] { "First", "Second" }, save.OwnedHeroKeys);
            Assert.AreEqual(2, save.Heroes.Count);
            Assert.AreEqual(0, save.Heroes[0].CurrentXp, "The first entry wins.");
            Assert.AreEqual(1, problems.Count);
        }

        [Test]
        public void BuildLoadout_OnlyHeroesWithChosenAbilities_GetAnEntry()
        {
            var chooser = new SandboxHeroSetup { Hero = Hero("Chooser", TwoBranches()), Abilities = { Spell("bolt"), null } };
            var autoFill = new SandboxHeroSetup { Hero = Hero("AutoFill", TwoBranches()) };

            var loadout = SandboxSetup.BuildLoadout(new[] { chooser, autoFill });

            CollectionAssert.AreEqual(new[] { "bolt" }, loadout.ChosenFor("Chooser"));
            Assert.IsEmpty(loadout.ChosenFor("AutoFill"), "No entry means auto-fill from the grid.");
            Assert.AreEqual(1, loadout.Heroes.Count);
        }

        [Test]
        public void UnlearnedAbilities_FlagsASpellTheSetupDoesNotTeach()
        {
            var setup = new SandboxHeroSetup
            {
                Hero = Hero("H", TwoBranches()),
                UnlockPathTo = { "d" },               // branch B: teaches nothing
                Abilities = { Spell("bolt") }         // taught at "b", on branch A
            };
            var entry = SandboxSetup.BuildHeroSave(setup, new List<string>());

            CollectionAssert.AreEqual(new[] { "bolt" }, SandboxSetup.UnlearnedAbilities(setup, entry));

            setup.UnlockPathTo.Add("b");
            entry = SandboxSetup.BuildHeroSave(setup, new List<string>());
            Assert.IsEmpty(SandboxSetup.UnlearnedAbilities(setup, entry));
        }

        // --- FileHandler override --------------------------------------------

        [Test]
        public void FileHandler_DirectoryOverride_RedirectsHandlersCreatedAfterIt()
        {
            var temp = Path.Combine(Path.GetTempPath(), "CardDungeonSandboxTest_" + System.Guid.NewGuid().ToString("N"));
            try
            {
                FileHandler.DirectoryOverride = temp;
                Assert.AreEqual(temp, FileHandler.CurrentDirectory);

                new FileHandler().Save(new PartySaveData { OwnedHeroKeys = { "Probe" } });

                Assert.IsTrue(File.Exists(Path.Combine(temp, "Party.json")), "Wrote into the override folder.");
                CollectionAssert.AreEqual(new[] { "Probe" }, new FileHandler(temp).Load<PartySaveData>().OwnedHeroKeys);
            }
            finally
            {
                FileHandler.DirectoryOverride = null;
                if (Directory.Exists(temp))
                {
                    Directory.Delete(temp, true);
                }
            }
            Assert.AreEqual(FileHandler.DefaultDirectory, FileHandler.CurrentDirectory);
        }
    }
}
