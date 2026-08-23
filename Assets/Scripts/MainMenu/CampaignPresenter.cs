using System.Collections.Generic;
using Assets.Scripts.Dungeon;
using Assets.Scripts.Heroes.UI;
using UnityEngine;

namespace Assets.Scripts.MainMenu
{
    /// <summary>
    /// Turns campaign state into things a graph view can draw: a USS class per node, a glyph, a
    /// position, and the edge list. Pure and static, exactly like <c>SphereGridPresenter</c> - the map
    /// screen makes no progression decisions of its own, it renders what this returns.
    ///
    /// <para>The renderer is <see cref="SphereGridView"/>, reused rather than reimplemented: it is
    /// already a generic pan/zoom node-graph widget whose only vocabulary is keys, positions and class
    /// names. The campaign supplies its own <c>cm-node--*</c> classes so the two graphs cannot clear
    /// each other's state.</para>
    /// </summary>
    public static class CampaignPresenter
    {
        /// <summary>State classes in the order <see cref="SphereGridView.StateClassNames"/> expects.</summary>
        public static readonly string[] StateClasses =
        {
            "cm-node--completed", "cm-node--available", "cm-node--active", "cm-node--locked"
        };

        public const string CompletedClass = "cm-node--completed";
        public const string AvailableClass = "cm-node--available";
        public const string ActiveClass = "cm-node--active";
        public const string LockedClass = "cm-node--locked";

        /// <summary>Horizontal gap between campaign tiers when positions are laid out automatically.</summary>
        private const float TierSpacing = 190f;

        /// <summary>Vertical gap between the branches sharing a tier.</summary>
        private const float BranchSpacing = 110f;

        public static string StateClass(CampaignNodeStatus status)
        {
            switch (status)
            {
                case CampaignNodeStatus.Completed:
                    return CompletedClass;
                case CampaignNodeStatus.Available:
                    return AvailableClass;
                case CampaignNodeStatus.InProgress:
                    return ActiveClass;
                default:
                    return LockedClass;
            }
        }

        /// <summary>A one-word status word for the detail panel.</summary>
        public static string StatusLabel(CampaignNodeState state)
        {
            if (state == null)
            {
                return string.Empty;
            }
            switch (state.Status)
            {
                case CampaignNodeStatus.Completed:
                    return state.CanStart ? "Cleared - can be run again" : "Cleared";
                case CampaignNodeStatus.Available:
                    return "Open";
                case CampaignNodeStatus.InProgress:
                    return "In progress";
                case CampaignNodeStatus.Locked:
                    return "Locked";
                default:
                    return string.Empty;
            }
        }

        /// <summary>The glyph drawn inside a node: a tick for cleared, a marker for the run underway.</summary>
        public static string Glyph(CampaignNodeState state)
        {
            if (state == null)
            {
                return "?";
            }
            switch (state.Status)
            {
                case CampaignNodeStatus.Completed:
                    return "✓";
                case CampaignNodeStatus.InProgress:
                    return "▶";
                case CampaignNodeStatus.Locked:
                    return "🔒";
                default:
                    return state.Node != null && state.Node.Optional ? "?" : "★";
            }
        }

        /// <summary>Kind class, so optional side content can be drawn differently from the main line.</summary>
        public static string KindClass(CampaignNodeEntry node)
        {
            return node != null && node.Optional ? "cm-node--optional" : "cm-node--main";
        }

        /// <summary>
        /// Fills <paramref name="nodes"/> and <paramref name="edges"/> for the visible part of the
        /// campaign. Hidden (secret, still locked) nodes are omitted entirely - and so is any edge
        /// touching one, or the map would leak the existence of the secret it is hiding.
        /// </summary>
        public static void BuildViewModel(
            CampaignSO campaign,
            IReadOnlyList<CampaignNodeState> states,
            List<SphereGridView.NodeInfo> nodes,
            List<(string A, string B)> edges)
        {
            nodes.Clear();
            edges.Clear();
            if (campaign == null || states == null)
            {
                return;
            }

            var positions = ResolvePositions(campaign);
            var visible = new HashSet<string>();

            foreach (var state in states)
            {
                if (state?.Node?.Run == null || !state.IsVisible)
                {
                    continue;
                }
                string key = CampaignOps.RunKeyOf(state.Node.Run);
                if (!visible.Add(key))
                {
                    continue;
                }
                nodes.Add(new SphereGridView.NodeInfo
                {
                    Key = key,
                    Position = positions.TryGetValue(key, out var p) ? p : Vector2.zero,
                    KindClass = KindClass(state.Node),
                    Glyph = Glyph(state),
                    IsStart = state.Node.Requires == null || state.Node.Requires.Count == 0
                });
            }

            foreach (var state in states)
            {
                var node = state?.Node;
                if (node?.Run == null || !state.IsVisible || node.Requires == null)
                {
                    continue;
                }
                string to = CampaignOps.RunKeyOf(node.Run);
                foreach (var prerequisite in node.Requires)
                {
                    if (prerequisite == null)
                    {
                        continue;
                    }
                    string from = CampaignOps.RunKeyOf(prerequisite);
                    if (visible.Contains(from) && visible.Contains(to))
                    {
                        edges.Add((from, to));
                    }
                }
            }
        }

        /// <summary>
        /// Where each node sits on the map. Authored <see cref="CampaignNodeEntry.MapPosition"/> wins;
        /// a campaign that has authored none gets a tidy generated layout instead of every node piled
        /// on the origin - tier (longest prerequisite chain) left to right, branches stacked within a
        /// tier. Authoring positions is therefore a polish step, not a prerequisite for playing.
        /// </summary>
        public static Dictionary<string, Vector2> ResolvePositions(CampaignSO campaign)
        {
            var positions = new Dictionary<string, Vector2>();
            if (campaign == null)
            {
                return positions;
            }

            bool anyAuthored = false;
            foreach (var node in campaign.Nodes)
            {
                if (node?.Run != null && node.MapPosition != Vector2.zero)
                {
                    anyAuthored = true;
                    break;
                }
            }

            if (anyAuthored)
            {
                foreach (var node in campaign.Nodes)
                {
                    if (node?.Run != null)
                    {
                        positions[CampaignOps.RunKeyOf(node.Run)] = node.MapPosition;
                    }
                }
                return positions;
            }

            var tiers = CampaignOps.ComputeTiers(campaign);
            var usedPerTier = new Dictionary<int, int>();
            foreach (var node in campaign.Nodes)
            {
                if (node?.Run == null)
                {
                    continue;
                }
                string key = CampaignOps.RunKeyOf(node.Run);
                int tier = tiers.TryGetValue(key, out var t) ? t : 0;
                usedPerTier.TryGetValue(tier, out int row);
                usedPerTier[tier] = row + 1;
                positions[key] = new Vector2(tier * TierSpacing, row * BranchSpacing);
            }
            return positions;
        }

    }
}
