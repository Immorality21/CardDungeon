using System.Collections.Generic;
using UnityEngine;
using ImmoralityGaming.Localization;

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

        [System.NonSerialized] private string _displayName;

        /// <summary>From the text table (<c>campaign.name</c>). Set only on instances built in code.</summary>
        public string DisplayName
        {
            get { return _displayName ?? Loc.GetOrEmpty("campaign.name"); }
            set { _displayName = value; }
        }

        public List<CampaignNodeEntry> Nodes = new List<CampaignNodeEntry>();

        [Header("Story map art")]
        [Tooltip("The ground under the story map: a seamless tile, repeated under every level " +
                 "however far the campaign reaches (Hub/CampaignScenery). Empty draws the plain panel.")]
        public Texture2D MapGround;

        [Tooltip("How big one ground tile is drawn, in map units.")]
        [Min(8f)] public float MapGroundTileSize = 128f;

        [Tooltip("Scenery for the woods (trees). Woods and open moor come from a slow noise over the " +
                 "map, and every piece is placed per map cell from MapScenerySeed, so a new level " +
                 "never moves what is already there.")]
        public List<Sprite> MapScenery = new List<Sprite>();

        [Tooltip("Scenery for the open moor between the woods (rocks, ruins, a gravestone), much " +
                 "sparser than the woods.")]
        public List<Sprite> MapSceneryOpen = new List<Sprite>();

        [Tooltip("The share of map cells that hold a piece of scenery.")]
        [Range(0f, 1f)] public float MapSceneryDensity = 0.3f;

        public int MapScenerySeed = 7;

        [Tooltip("Hand-placed pieces of the map that are not levels: mountains, lakes, snowfields. " +
                 "Placed in Tools > Campaign Map Editor (Add Feature).")]
        public List<CampaignMapFeature> MapFeatures = new List<CampaignMapFeature>();

        [Tooltip("Rivers across the map, drawn in Tools > Campaign Map Editor (Draw River).")]
        public List<CampaignMapRiver> MapRivers = new List<CampaignMapRiver>();

        [Tooltip("The art laid along every river: a horizontal strip of water with its banks, flowing " +
                 "left to right, seamless end to end (it is cut into sections and laid in order along " +
                 "the curve). Empty draws the plain painted river.")]
        public Texture2D MapRiverStrip;

        [Tooltip("What a river can start or end in (mountain springs, lakes), each with the way its " +
                 "water runs. A river names one by its sprite (CampaignMapRiver.Source / Mouth).")]
        public List<CampaignMapRiverCap> MapRiverCaps = new List<CampaignMapRiverCap>();

        /// <summary>The cap entry for <paramref name="sprite"/>, or null.</summary>
        public CampaignMapRiverCap FindRiverCap(Sprite sprite)
        {
            if (sprite == null || MapRiverCaps == null)
            {
                return null;
            }
            foreach (var cap in MapRiverCaps)
            {
                if (cap != null && cap.Sprite == sprite)
                {
                    return cap;
                }
            }
            return null;
        }

        [Tooltip("Draw levels that are not open yet (greyed, with a lock). Off: the map shows only " +
                 "what the player can play or has played (owner, 2026-11-22).")]
        public bool ShowLockedLevels;

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
