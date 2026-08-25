using System.Collections.Generic;
using Assets.Scripts.UnitStats;
using UnityEngine;

namespace Assets.Scripts.Heroes.UI
{
    /// <summary>How one node should read on screen, given the hero's activations and bank.</summary>
    public enum NodeUiState
    {
        Activated,  // bought
        Available,  // reachable and affordable — the "buy me" state
        Adjacent,   // reachable but the bank does not cover it
        Locked      // no activated neighbour yet
    }

    /// <summary>
    /// The decision layer between <see cref="SphereGridOps"/> and <see cref="SphereGridView"/>:
    /// classifies nodes into UI states and describes payloads as text. Pure and scene-free so the
    /// EditMode tests drive it directly — and so the editor window's "preview at N XP" mode shows
    /// exactly the colouring a player with that bank would see, because both call the same
    /// classifier.
    /// </summary>
    public static class SphereGridPresenter
    {
        public static NodeUiState Classify(SphereGridSO grid, ICollection<string> activated, int bank, string key)
        {
            if (activated != null && activated.Contains(key))
            {
                return NodeUiState.Activated;
            }
            if (!SphereGridOps.IsReachable(grid, activated, key))
            {
                return NodeUiState.Locked;
            }

            var node = SphereGridOps.FindNode(grid, key);
            return node != null && bank >= node.XpCost ? NodeUiState.Available : NodeUiState.Adjacent;
        }

        public static Dictionary<string, NodeUiState> ClassifyAll(
            SphereGridSO grid, ICollection<string> activated, int bank)
        {
            var states = new Dictionary<string, NodeUiState>();
            if (grid == null || grid.Nodes == null)
            {
                return states;
            }

            foreach (var node in grid.Nodes)
            {
                if (node != null && !string.IsNullOrEmpty(node.Key) && !states.ContainsKey(node.Key))
                {
                    states[node.Key] = Classify(grid, activated, bank, node.Key);
                }
            }
            return states;
        }

        /// <summary>The sg-node--* USS class for a state (see CardDungeon.uss).</summary>
        public static string StateClass(NodeUiState state)
        {
            switch (state)
            {
                case NodeUiState.Activated:
                    return "sg-node--activated";
                case NodeUiState.Available:
                    return "sg-node--available";
                case NodeUiState.Adjacent:
                    return "sg-node--adjacent";
                default:
                    return "sg-node--locked";
            }
        }

        /// <summary>The sg-node--* USS kind class for a node.</summary>
        public static string KindClass(SphereGridNode node)
        {
            if (node == null)
            {
                return "sg-node--stat";
            }

            switch (node.Kind)
            {
                case SphereNodeKind.Resistance:
                    return "sg-node--resist";
                case SphereNodeKind.MagicSlot:
                case SphereNodeKind.MagicKnown:
                    return "sg-node--slot";
                default:
                    return "sg-node--stat";
            }
        }

        /// <summary>The single character drawn inside a node.</summary>
        public static string Glyph(SphereGridNode node, bool isStart)
        {
            if (isStart)
            {
                return "★";
            }
            if (node == null)
            {
                return "?";
            }

            switch (node.Kind)
            {
                case SphereNodeKind.Resistance:
                    return "R";
                case SphereNodeKind.MagicSlot:
                    return "M";
                case SphereNodeKind.MagicKnown:
                    return "✦";
                default:
                    return "S";
            }
        }

        /// <summary>The player-facing name: authored DisplayName, falling back to the payload.</summary>
        public static string NodeName(SphereGridNode node)
        {
            if (node == null)
            {
                return "";
            }
            return string.IsNullOrEmpty(node.DisplayName) ? DescribePayload(node) : node.DisplayName;
        }

        /// <summary>What activating the node grants, as one line ("+2 STR · +1 END").</summary>
        public static string DescribePayload(SphereGridNode node)
        {
            if (node == null)
            {
                return "";
            }

            if (node.Kind == SphereNodeKind.Resistance)
            {
                return $"{node.ResistType} resistance +{node.ResistPercent:0}%";
            }
            if (node.Kind == SphereNodeKind.MagicSlot)
            {
                return "+1 magic slot";
            }
            if (node.Kind == SphereNodeKind.MagicKnown)
            {
                // The charge count is the payload as much as the magic is: it is the whole run's
                // allowance of that spell, because charges never refill mid-run.
                string name = string.IsNullOrEmpty(node.GrantedMagicKey) ? "(unset)" : node.GrantedMagicKey;
                return $"Always carries {name} x{Mathf.Max(1, node.GrantedCharges)}";
            }

            var parts = new List<string>();
            if (node.Gains != null)
            {
                foreach (var entry in node.Gains.NonZero())
                {
                    parts.Add($"+{entry.Amount} {StatCatalog.ShortName(entry.Type)}");
                }
            }
            return parts.Count > 0 ? string.Join(" · ", parts) : "(grants nothing)";
        }

        /// <summary>
        /// The grid as <see cref="SphereGridView"/> input: one NodeInfo per keyed node, and each
        /// symmetrized edge exactly once. Shared by the hub screen and the editor window so the two
        /// cannot render different shapes from the same asset.
        /// </summary>
        public static void BuildViewModel(
            SphereGridSO grid,
            List<SphereGridView.NodeInfo> nodes,
            List<(string A, string B)> edges)
        {
            nodes.Clear();
            edges.Clear();
            if (grid == null || grid.Nodes == null)
            {
                return;
            }

            string start = SphereGridOps.StartKey(grid);
            foreach (var node in grid.Nodes)
            {
                if (node == null || string.IsNullOrEmpty(node.Key))
                {
                    continue;
                }
                nodes.Add(new SphereGridView.NodeInfo
                {
                    Key = node.Key,
                    Position = node.Position,
                    KindClass = KindClass(node),
                    Glyph = Glyph(node, node.Key == start),
                    IsStart = node.Key == start
                });
            }

            foreach (var pair in SphereGridOps.BuildAdjacency(grid))
            {
                foreach (var neighbor in pair.Value)
                {
                    if (string.CompareOrdinal(pair.Key, neighbor) < 0)
                    {
                        edges.Add((pair.Key, neighbor));
                    }
                }
            }
        }

        /// <summary>The kind as a player-facing word.</summary>
        public static string KindLabel(SphereGridNode node)
        {
            if (node == null)
            {
                return "";
            }

            switch (node.Kind)
            {
                case SphereNodeKind.Resistance:
                    return "Resistance";
                case SphereNodeKind.MagicSlot:
                    return "Magic slot";
                case SphereNodeKind.MagicKnown:
                    return "Known magic";
                default:
                    return "Stat";
            }
        }
    }
}
