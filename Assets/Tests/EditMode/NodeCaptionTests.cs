using System.Collections.Generic;
using Assets.Scripts.Dungeon;
using Assets.Scripts.Heroes;
using Assets.Scripts.Heroes.UI;
using Assets.Scripts.Hub;
using NUnit.Framework;
using UnityEditor;

namespace Tests.EditMode
{
    /// <summary>
    /// The campaign map and the sphere grid share <see cref="SphereGridView"/>. Run names were added
    /// under the map's nodes (playtest 2026-09-28, finding 11) through an opt-in caption, and these
    /// pin both halves: every visible run is named, and no sphere grid node ever gets a caption.
    /// </summary>
    public class NodeCaptionTests
    {
        [Test]
        public void CampaignMap_EveryVisibleRun_IsNamedOnItsNode()
        {
            var campaign = UnityEngine.Resources.Load<CampaignSO>(CampaignSO.ResourcePath);
            Assert.IsNotNull(campaign, "No campaign asset in Resources.");

            // A fresh save: nothing completed, no run underway.
            var states = CampaignOps.GetStates(campaign, new HashSet<string>(), string.Empty);
            var nodes = new List<SphereGridView.NodeInfo>();
            var edges = new List<(string A, string B)>();
            CampaignPresenter.BuildViewModel(campaign, states, nodes, edges);

            Assert.IsNotEmpty(nodes);
            foreach (var node in nodes)
            {
                Assert.IsFalse(string.IsNullOrEmpty(node.Caption), $"Run node '{node.Key}' has no name on the map.");
            }
        }

        [Test]
        public void SphereGrid_NoNode_HasACaption()
        {
            var guids = AssetDatabase.FindAssets("t:SphereGridSO");
            Assert.IsNotEmpty(guids);

            var nodes = new List<SphereGridView.NodeInfo>();
            var edges = new List<(string A, string B)>();
            foreach (var guid in guids)
            {
                var grid = AssetDatabase.LoadAssetAtPath<SphereGridSO>(AssetDatabase.GUIDToAssetPath(guid));
                SphereGridPresenter.BuildViewModel(grid, nodes, edges);
                foreach (var node in nodes)
                {
                    Assert.IsTrue(string.IsNullOrEmpty(node.Caption),
                        $"{grid.name} node '{node.Key}' has a caption - captions are for the campaign map only.");
                }
            }
        }
    }
}
