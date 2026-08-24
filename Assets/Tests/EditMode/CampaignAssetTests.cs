using System.Collections.Generic;
using Assets.Scripts.Dungeon;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// The project's real campaign asset, checked the same way the manual level layouts are: a graph
    /// the player walks through once, where an authoring mistake is invisible until someone reaches
    /// the broken branch. Everything here is a fault no amount of play skill can work around.
    /// </summary>
    public class CampaignAssetTests
    {
        private static CampaignSO LoadCampaign()
        {
            return UnityEngine.Resources.Load<CampaignSO>(CampaignSO.ResourcePath);
        }

        [Test]
        public void Campaign_ExistsInResources()
        {
            Assert.IsNotNull(LoadCampaign(),
                $"No campaign at Assets/Resources/{CampaignSO.ResourcePath}.asset - the hub loads it " +
                "from Resources, so without it the story map has nothing to draw.");
        }

        [Test]
        public void Campaign_HasSomewhereToBegin()
        {
            var campaign = LoadCampaign();
            CollectionAssert.IsNotEmpty(CampaignOps.GetRootNodes(campaign),
                "Every node has a prerequisite, so a fresh save can never start a run.");
        }

        [Test]
        public void Campaign_EveryNodeHasARun()
        {
            var campaign = LoadCampaign();
            var broken = CampaignOps.GetNodesWithoutRun(campaign);
            CollectionAssert.IsEmpty(broken,
                $"Node(s) {string.Join(", ", broken)} have no RunDefinitionSO assigned.");
        }

        [Test]
        public void Campaign_NoRunAppearsTwice()
        {
            var campaign = LoadCampaign();
            var duplicates = CampaignOps.GetDuplicateRunKeys(campaign);
            CollectionAssert.IsEmpty(duplicates,
                $"Run(s) {string.Join(", ", duplicates)} appear on more than one node; a single clear " +
                "would complete all of them.");
        }

        [Test]
        public void Campaign_NoPrerequisiteOutsideTheCampaign()
        {
            var campaign = LoadCampaign();
            var broken = CampaignOps.GetNodesWithOutsidePrerequisites(campaign);
            CollectionAssert.IsEmpty(broken,
                $"Node(s) {string.Join(", ", broken)} require a run that is not in the campaign, so " +
                "they can never unlock.");
        }

        [Test]
        public void Campaign_EveryNodeIsReachable()
        {
            var campaign = LoadCampaign();
            var stranded = CampaignOps.GetUnreachableNodes(campaign);
            CollectionAssert.IsEmpty(stranded,
                $"Node(s) {string.Join(", ", stranded)} can never unlock however well the campaign is " +
                "played - usually a prerequisite cycle.");
        }

        [Test]
        public void Campaign_ContainsEveryRunInTheProject()
        {
            var campaign = LoadCampaign();
            Assert.IsNotNull(campaign);

            var inCampaign = new HashSet<string>();
            foreach (var node in campaign.Nodes)
            {
                if (node?.Run != null)
                {
                    inCampaign.Add(CampaignOps.RunKeyOf(node.Run));
                }
            }

            var missing = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:RunDefinitionSO"))
            {
                var run = AssetDatabase.LoadAssetAtPath<RunDefinitionSO>(AssetDatabase.GUIDToAssetPath(guid));
                if (run != null && !inCampaign.Contains(CampaignOps.RunKeyOf(run)))
                {
                    missing.Add(run.name);
                }
            }

            CollectionAssert.IsEmpty(missing,
                $"Run asset(s) {string.Join(", ", missing)} exist but are not on the campaign map, so " +
                "there is no way to play them. Add a node, or delete the run.");
        }

        [Test]
        public void Campaign_TutorialIsARootAndIsNotRepeatable()
        {
            var campaign = LoadCampaign();
            Assert.IsNotNull(campaign);

            var tutorial = campaign.FindNode("TutorialRun");
            Assert.IsNotNull(tutorial, "The tutorial run is missing from the campaign.");
            Assert.IsTrue(tutorial.Requires == null || tutorial.Requires.Count == 0,
                "The tutorial must be startable on a fresh save.");
            Assert.IsFalse(tutorial.Run.Repeatable,
                "The tutorial is one-shot content; replaying it is the bug this gate was added for.");
        }

        /// <summary>
        /// The hub's hard guarantee, walked forward one clear at a time: however far a save gets,
        /// it must never reach a state with no run to start and none to continue. Clearing the
        /// tutorial did exactly that once - the run existed, the menu just refused to offer it - so
        /// this asserts the property rather than any one screen's wiring.
        /// </summary>
        [Test]
        public void Campaign_NeverStrandsASaveWithNothingToPlay()
        {
            var campaign = LoadCampaign();
            Assert.IsNotNull(campaign);

            var completed = new HashSet<string>();
            var guard = campaign.Nodes.Count + 1;

            for (int step = 0; step <= guard; step++)
            {
                Assert.IsTrue(CampaignOps.HasSomethingToPlay(campaign, completed, string.Empty),
                    $"After clearing [{string.Join(", ", completed)}] the campaign offers no run to "
                    + "start and none to continue - the save is stuck at the hub forever.");

                string next = null;
                foreach (var state in CampaignOps.GetStates(campaign, completed, string.Empty))
                {
                    if (state.CanStart && !completed.Contains(CampaignOps.RunKeyOf(state.Node.Run)))
                    {
                        next = CampaignOps.RunKeyOf(state.Node.Run);
                        break;
                    }
                }
                if (next == null)
                {
                    // Everything clearable is cleared, and the check above proved a repeatable run
                    // is still on offer. That is a finished campaign, not a stranded one.
                    return;
                }
                completed.Add(next);
            }

            Assert.Fail("Walking the campaign did not terminate.");
        }

        [Test]
        public void Campaign_ClearingTheTutorial_OpensMoreThanOneRun()
        {
            var campaign = LoadCampaign();
            var completed = new HashSet<string> { "TutorialRun" };

            var startable = new List<string>();
            foreach (var state in CampaignOps.GetStates(campaign, completed, string.Empty))
            {
                if (state.CanStart)
                {
                    startable.Add(CampaignOps.RunKeyOf(state.Node.Run));
                }
            }

            CollectionAssert.Contains(startable, "DrownedMarch", "the main line must open");
            CollectionAssert.Contains(startable, "TheWarrens", "the repeatable branch must open");
        }

        [Test]
        public void Campaign_HasARepeatableRun_SoGoldCanAlwaysBeFarmed()
        {
            var campaign = LoadCampaign();
            Assert.IsNotNull(campaign);

            bool anyRepeatable = false;
            foreach (var node in campaign.Nodes)
            {
                if (node?.Run != null && node.Run.Repeatable)
                {
                    anyRepeatable = true;
                }
            }

            Assert.IsTrue(anyRepeatable,
                "Every run is one-shot, so a save that clears them all can never earn Gold again - "
                + "and party slots cost 300/600.");
        }

        [Test]
        public void Campaign_LaidOutPositions_DoNotOverlap()
        {
            var campaign = LoadCampaign();
            var positions = Assets.Scripts.MainMenu.CampaignPresenter.ResolvePositions(campaign);

            var seen = new Dictionary<Vector2, string>();
            foreach (var pair in positions)
            {
                Assert.IsFalse(seen.ContainsKey(pair.Value),
                    $"'{pair.Key}' and '{seen.GetValueOrDefault(pair.Value)}' sit on the same map " +
                    "position, so one hides the other.");
                seen[pair.Value] = pair.Key;
            }
        }
    }
}
