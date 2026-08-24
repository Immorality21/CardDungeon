using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.Dungeon
{
    /// <summary>
    /// The story line: every <see cref="RunDefinitionSO"/> in the game and the order they open in.
    ///
    /// <para>A directed graph rather than a list, because the campaign branches - clearing one run can
    /// open two, one of which rejoins the main line while the other is an optional dead end. The graph
    /// lives here, in one asset, rather than as prerequisites scattered across the run assets: a
    /// campaign has to be readable and validatable as a whole, and the map screen needs a single
    /// object to draw.</para>
    ///
    /// <para>Loaded from Resources (like <c>ItemCatalogSO</c>) so the hub scene can resolve it without
    /// scene wiring and without <c>AssetDatabase</c>, which does not exist in a build. Lives at
    /// <c>Assets/Resources/Campaign.asset</c>.</para>
    ///
    /// <para>Progress is not stored here - it is <c>MetaProgressSaveData.CompletedRunKeys</c>, so the
    /// campaign asset stays pure content and the same graph reads differently per save. See
    /// <see cref="CampaignOps"/> for the rules that combine the two.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "SO/Campaign")]
    public class CampaignSO : ScriptableObject
    {
        public const string ResourcePath = "Campaign";

        public string DisplayName = "Campaign";

        public List<CampaignNodeEntry> Nodes = new List<CampaignNodeEntry>();

        /// <summary>The node holding this run, or null when the run is not in the campaign.</summary>
        public CampaignNodeEntry FindNode(RunDefinitionSO run)
        {
            if (run == null)
            {
                return null;
            }
            foreach (var node in Nodes)
            {
                if (node != null && node.Run == run)
                {
                    return node;
                }
            }
            return null;
        }

        /// <summary>The node whose run has this save key, or null. Key matching mirrors the save file.</summary>
        public CampaignNodeEntry FindNode(string runKey)
        {
            if (string.IsNullOrEmpty(runKey))
            {
                return null;
            }
            foreach (var node in Nodes)
            {
                if (node?.Run != null && CampaignOps.RunKeyOf(node.Run) == runKey)
                {
                    return node;
                }
            }
            return null;
        }
    }
}
