using System.Collections.Generic;
using Assets.Scripts.Dungeon;
using Assets.Scripts.Heroes;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// Heroes as progression unlocks (<c>docs/NEXT_STEPS.md</c> §5b): gold never buys a hero, so a
    /// hero is the one requirement in the game that is a *key* rather than more of a general thing.
    ///
    /// <para>That is also what makes it dangerous. A run gate can only ever delay a branch - the
    /// player can always go and clear the run. A hero gate can shut a branch permanently, because a
    /// hero may sit behind an optional branch or a captive the player walked past. The asset sweeps
    /// at the bottom are the guard rail for exactly that.</para>
    /// </summary>
    public class HeroUnlockTests
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

        // --- Fixtures ----------------------------------------------------------------------

        private HeroSO MakeHero(string key)
        {
            var hero = ScriptableObject.CreateInstance<HeroSO>();
            hero.name = key;
            hero.Key = key;
            hero.Label = key;
            _created.Add(hero);
            return hero;
        }

        private RunDefinitionSO MakeRun(string key, params HeroSO[] captives)
        {
            var run = ScriptableObject.CreateInstance<RunDefinitionSO>();
            run.name = key;
            run.Key = key;
            run.DisplayName = key;
            run.Levels = new List<RunLevelEntry>();
            foreach (var captive in captives)
            {
                run.Levels.Add(new RunLevelEntry { LevelName = key, RescueHero = captive });
            }
            _created.Add(run);
            return run;
        }

        private PartyRosterSO MakeRoster(params HeroSO[] starting)
        {
            var roster = ScriptableObject.CreateInstance<PartyRosterSO>();
            roster.Heroes = new List<HeroSO>(starting);
            roster.StartingHeroes = new List<HeroSO>(starting);
            _created.Add(roster);
            return roster;
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
            IEnumerable<RunDefinitionSO> requires = null,
            IEnumerable<HeroSO> requiresHeroes = null,
            CampaignUnlockMode mode = CampaignUnlockMode.All)
        {
            return new CampaignNodeEntry
            {
                Run = run,
                Requires = requires != null ? new List<RunDefinitionSO>(requires) : new List<RunDefinitionSO>(),
                RequiresHeroes = requiresHeroes != null ? new List<HeroSO>(requiresHeroes) : new List<HeroSO>(),
                UnlockMode = mode
            };
        }

        private static HashSet<string> Keys(params string[] keys)
        {
            return new HashSet<string>(keys);
        }

        // --- The gate itself ---------------------------------------------------------------

        [Test]
        public void IsUnlocked_NoHeroGate_IsUnaffectedByOwnership()
        {
            var node = Node(MakeRun("Open"));
            Assert.IsTrue(CampaignOps.IsUnlocked(node, Keys(), null),
                "A node with no hero gate must not care that the caller passed no roster.");
        }

        [Test]
        public void IsUnlocked_HeroGate_WithoutTheHero_StaysLocked()
        {
            var rogue = MakeHero("Rogue");
            var node = Node(MakeRun("Warrens"), requiresHeroes: new[] { rogue });

            Assert.IsFalse(CampaignOps.IsUnlocked(node, Keys(), Keys()));
            Assert.IsTrue(CampaignOps.IsUnlocked(node, Keys(), Keys("Rogue")));
        }

        [Test]
        public void IsUnlocked_HeroGate_WithNoOwnedSetAtAll_FailsShut()
        {
            // A caller that forgets to pass the roster must lock the run, not open it: a run held
            // shut shows up the moment someone looks at the map, a run wrongly offered does not.
            var node = Node(MakeRun("Warrens"), requiresHeroes: new[] { MakeHero("Rogue") });
            Assert.IsFalse(CampaignOps.IsUnlocked(node, Keys(), null));
        }

        [Test]
        public void IsUnlocked_HeroGate_IsAlwaysAllOf_EvenInAnyMode()
        {
            var a = MakeHero("A");
            var b = MakeHero("B");
            var node = Node(MakeRun("Vault"), requiresHeroes: new[] { a, b }, mode: CampaignUnlockMode.Any);

            Assert.IsFalse(CampaignOps.IsUnlocked(node, Keys(), Keys("A")),
                "UnlockMode softens the run list, never the hero list.");
            Assert.IsTrue(CampaignOps.IsUnlocked(node, Keys(), Keys("A", "B")));
        }

        [Test]
        public void IsUnlocked_HeroGate_AndsWithTheRunGate()
        {
            var tutorial = MakeRun("Tutorial");
            var hero = MakeHero("Cleric");
            var node = Node(MakeRun("Deep"), requires: new[] { tutorial }, requiresHeroes: new[] { hero });

            Assert.IsFalse(CampaignOps.IsUnlocked(node, Keys(), Keys("Cleric")), "the run is not cleared");
            Assert.IsFalse(CampaignOps.IsUnlocked(node, Keys("Tutorial"), Keys()), "the hero is not owned");
            Assert.IsTrue(CampaignOps.IsUnlocked(node, Keys("Tutorial"), Keys("Cleric")));
        }

        [Test]
        public void GetState_LockedByAHero_NamesTheHeroSeparatelyFromTheRuns()
        {
            var tutorial = MakeRun("Tutorial");
            var hero = MakeHero("Cleric");
            var node = Node(MakeRun("Deep"), requires: new[] { tutorial }, requiresHeroes: new[] { hero });

            var state = CampaignOps.GetState(node, Keys(), string.Empty, Keys());

            Assert.AreEqual(CampaignNodeStatus.Locked, state.Status);
            CollectionAssert.AreEqual(new[] { "Tutorial" }, state.MissingRequirements);
            CollectionAssert.AreEqual(new[] { "Cleric" }, state.MissingHeroes,
                "A missing hero reads as someone to find, not somewhere to go, so it is reported apart.");
        }

        [Test]
        public void GetState_HeroGateSatisfied_ReportsNoMissingHeroes()
        {
            var hero = MakeHero("Cleric");
            var node = Node(MakeRun("Deep"), requiresHeroes: new[] { hero });

            var state = CampaignOps.GetState(node, Keys(), string.Empty, Keys("Cleric"));

            Assert.AreEqual(CampaignNodeStatus.Available, state.Status);
            CollectionAssert.IsEmpty(state.MissingHeroes);
        }

        [Test]
        public void HeroGateSatisfied_EmptyRow_IsIgnoredRatherThanBlocking()
        {
            // A half-authored asset should stay playable; GetNodesWithBrokenHeroGates reports it.
            var node = Node(MakeRun("Deep"), requiresHeroes: new HeroSO[] { null });
            Assert.IsTrue(CampaignOps.HeroGateSatisfied(node, Keys()));
        }

        // --- What the player is guaranteed to be holding ------------------------------------

        [Test]
        public void GetGuaranteedHeroKeys_CountsTheStartingLineupAndEveryRequiredRunsCaptives()
        {
            var warrior = MakeHero("Warrior");
            var paladin = MakeHero("Paladin");
            var ranger = MakeHero("Ranger");

            var tutorial = MakeRun("Tutorial", paladin);
            var march = MakeRun("March", ranger);
            var deep = MakeRun("Deep");

            var marchNode = Node(march, requires: new[] { tutorial });
            var deepNode = Node(deep, requires: new[] { march });
            var campaign = MakeCampaign(Node(tutorial), marchNode, deepNode);

            var guaranteed = CampaignOps.GetGuaranteedHeroKeys(campaign, deepNode, MakeRoster(warrior));

            CollectionAssert.AreEquivalent(new[] { "Warrior", "Paladin", "Ranger" }, guaranteed,
                "Everything on the required path counts, transitively.");
        }

        [Test]
        public void GetGuaranteedHeroKeys_IgnoresAHeroDownAnOptionalBranch()
        {
            var warrior = MakeHero("Warrior");
            var rogue = MakeHero("Rogue");

            var tutorial = MakeRun("Tutorial");
            var sideBranch = MakeRun("Side", rogue);
            var deep = MakeRun("Deep");

            var deepNode = Node(deep, requires: new[] { tutorial });
            var campaign = MakeCampaign(
                Node(tutorial),
                Node(sideBranch, requires: new[] { tutorial }),
                deepNode);

            var guaranteed = CampaignOps.GetGuaranteedHeroKeys(campaign, deepNode, MakeRoster(warrior));

            CollectionAssert.DoesNotContain(guaranteed, "Rogue",
                "The player never had to play the side branch, so the Rogue is not guaranteed.");
        }

        [Test]
        public void GetGuaranteedHeroKeys_IgnoresAnAnyModePrerequisitesCaptives()
        {
            var warrior = MakeHero("Warrior");
            var rogue = MakeHero("Rogue");

            var left = MakeRun("Left", rogue);
            var right = MakeRun("Right");
            var deepNode = Node(MakeRun("Deep"), requires: new[] { left, right }, mode: CampaignUnlockMode.Any);
            var campaign = MakeCampaign(Node(left), Node(right), deepNode);

            var guaranteed = CampaignOps.GetGuaranteedHeroKeys(campaign, deepNode, MakeRoster(warrior));

            CollectionAssert.DoesNotContain(guaranteed, "Rogue",
                "Either branch opens the node, so neither branch's captive is guaranteed.");
        }

        [Test]
        public void GetGuaranteedHeroKeys_PrerequisiteCycle_Terminates()
        {
            var a = MakeRun("A");
            var b = MakeRun("B");
            var nodeA = Node(a, requires: new[] { b });
            var nodeB = Node(b, requires: new[] { a });
            var campaign = MakeCampaign(nodeA, nodeB);

            var guaranteed = CampaignOps.GetGuaranteedHeroKeys(campaign, nodeA, MakeRoster(MakeHero("Warrior")));

            CollectionAssert.AreEquivalent(new[] { "Warrior" }, guaranteed);
        }

        [Test]
        public void GetNodesWithBrokenHeroGates_FlagsAGateOnAnUnreachableHero()
        {
            var warrior = MakeHero("Warrior");
            var ghost = MakeHero("Ghost");

            var tutorial = MakeRun("Tutorial");
            var deepNode = Node(MakeRun("Deep"), requires: new[] { tutorial }, requiresHeroes: new[] { ghost });
            var campaign = MakeCampaign(Node(tutorial), deepNode);

            CollectionAssert.AreEqual(
                new[] { 1 },
                CampaignOps.GetNodesWithBrokenHeroGates(campaign, MakeRoster(warrior)));
        }

        [Test]
        public void GetNodesWithBrokenHeroGates_SatisfiableGate_IsNotFlagged()
        {
            var warrior = MakeHero("Warrior");
            var ranger = MakeHero("Ranger");

            var tutorial = MakeRun("Tutorial", ranger);
            var deepNode = Node(MakeRun("Deep"), requires: new[] { tutorial }, requiresHeroes: new[] { ranger });
            var campaign = MakeCampaign(Node(tutorial), deepNode);

            CollectionAssert.IsEmpty(CampaignOps.GetNodesWithBrokenHeroGates(campaign, MakeRoster(warrior)));
        }

        // --- The project's real assets ------------------------------------------------------

        private static CampaignSO LoadCampaign()
        {
            return UnityEngine.Resources.Load<CampaignSO>(CampaignSO.ResourcePath);
        }

        private static PartyRosterSO LoadRoster()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:PartyRosterSO"))
            {
                var roster = AssetDatabase.LoadAssetAtPath<PartyRosterSO>(AssetDatabase.GUIDToAssetPath(guid));
                if (roster != null)
                {
                    return roster;
                }
            }
            return null;
        }

        [Test]
        public void Roster_StartsWithExactlyOneHero()
        {
            var roster = LoadRoster();
            Assert.IsNotNull(roster, "No PartyRosterSO in the project.");
            Assert.AreEqual(1, roster.StartingLineup().Count,
                "The player starts with one hero and unlocks the rest (NEXT_STEPS.md §5b). Party "
                + "width is the game's strongest difficulty lever, so a second starting hero is a "
                + "balance change wearing an authoring change's clothes.");
        }

        [Test]
        public void EveryHeroGateInTheCampaign_IsSatisfiableWithoutLuckOrDetours()
        {
            var campaign = LoadCampaign();
            var roster = LoadRoster();
            Assert.IsNotNull(campaign);
            Assert.IsNotNull(roster);

            var broken = CampaignOps.GetNodesWithBrokenHeroGates(campaign, roster);

            var names = new List<string>();
            foreach (int index in broken)
            {
                names.Add(CampaignOps.DisplayNameOf(campaign.Nodes[index].Run));
            }

            CollectionAssert.IsEmpty(names,
                $"Node(s) {string.Join(", ", names)} require a hero the player is not guaranteed to "
                + "own by the time they arrive - the hero is in the catalog but not on the required "
                + "path, so that run can never be started. Gate only on a captive the player must "
                + "pass, or move the captive onto the path.");
        }

        [Test]
        public void EveryCaptiveInTheCampaign_IsAHeroTheRosterKnowsAbout()
        {
            var campaign = LoadCampaign();
            var roster = LoadRoster();
            Assert.IsNotNull(campaign);
            Assert.IsNotNull(roster);

            var orphaned = new List<string>();
            foreach (var node in campaign.Nodes)
            {
                if (node?.Run?.Levels == null)
                {
                    continue;
                }
                foreach (var level in node.Run.Levels)
                {
                    var captive = level?.RescueHero;
                    if (captive != null && roster.Find(captive.SaveKey) == null)
                    {
                        orphaned.Add($"{CampaignOps.DisplayNameOf(node.Run)} / {captive.DisplayName}");
                    }
                }
            }

            CollectionAssert.IsEmpty(orphaned,
                $"Captive(s) {string.Join(", ", orphaned)} are rescuable but are not in the party "
                + "roster catalog, so HeroRoster cannot resolve them back into a hero after the run.");
        }
    }
}
