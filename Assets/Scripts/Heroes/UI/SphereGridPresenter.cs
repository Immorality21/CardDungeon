using System;
using System.Collections.Generic;
using Assets.Scripts.Items;
using Assets.Scripts.UnitStats;
using UnityEngine;
using ImmoralityGaming.Localization;

namespace Assets.Scripts.Heroes.UI
{
    /// <summary>How one node should read on screen, given the hero's activations and bank.</summary>
    public enum NodeUiState
    {
        Activated,  // bought
        Available,  // reachable and affordable — the "buy me" state
        Adjacent,   // reachable but the bank does not cover it
        Locked,     // no activated neighbour yet
        Gated       // next to something active, but a node it requires (SphereGridNode.RequiredNodes) is not
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
        /// <param name="canPayMaterials">Whether the rest of a node's price - materials, Essence - is
        /// covered. Null means "assume yes" - the editor window previews a grid with no inventory
        /// behind it, and a preview that painted every material node unaffordable would say nothing
        /// useful.</param>
        public static NodeUiState Classify(
            SphereGridSO grid,
            ICollection<string> activated,
            int bank,
            string key,
            Func<SphereGridNode, bool> canPayMaterials = null)
        {
            // The active set, not the saved one: a default unlock reads as bought without ever
            // having been recorded as such.
            if (SphereGridOps.ActiveNodes(grid, activated).Contains(key))
            {
                return NodeUiState.Activated;
            }
            if (!SphereGridOps.IsReachable(grid, activated, key))
            {
                return NodeUiState.Locked;
            }
            // Its own state rather than Locked: it sits on the frontier, and no amount of XP opens it.
            if (SphereGridOps.MissingRequirements(grid, activated, key).Count > 0)
            {
                return NodeUiState.Gated;
            }

            var node = SphereGridOps.FindNode(grid, key);
            if (node == null || bank < node.XpCost)
            {
                return NodeUiState.Adjacent;
            }
            // Materials and Essence are the rest of the price, so failing them reads exactly like
            // failing the XP half: reachable, wanted, not yet payable.
            if (canPayMaterials != null && !canPayMaterials(node))
            {
                return NodeUiState.Adjacent;
            }
            return NodeUiState.Available;
        }

        public static Dictionary<string, NodeUiState> ClassifyAll(
            SphereGridSO grid,
            ICollection<string> activated,
            int bank,
            Func<SphereGridNode, bool> canPayMaterials = null)
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
                    states[node.Key] = Classify(grid, activated, bank, node.Key, canPayMaterials);
                }
            }
            return states;
        }

        /// <summary>
        /// A node's material price as one line ("2 Ember Iron - 1 Void Shard"), or empty when it
        /// charges XP alone.
        /// </summary>
        public static string DescribeMaterialCost(SphereGridNode node)
        {
            if (!SphereGridOps.HasMaterialCost(node))
            {
                return "";
            }

            var parts = new List<string>();
            foreach (var line in node.MaterialCosts)
            {
                if (line == null || !line.IsValid)
                {
                    continue;
                }
                string name = string.IsNullOrEmpty(line.Material.DisplayName)
                    ? line.Material.Key
                    : line.Material.DisplayName;
                parts.Add(Loc.Format("grid.cost.material", ("amount", Mathf.Max(1, line.Amount)), ("material", name)));
            }
            return string.Join(" · ", parts);
        }

        /// <summary>
        /// The cost line under a node's payload: what it costs, or that it was never for sale.
        /// One function so the hub screen and the authoring preview cannot describe the same node
        /// differently.
        /// </summary>
        public static string DescribeCost(SphereGridNode node, bool isActive)
        {
            if (node == null)
            {
                return "";
            }

            if (SphereGridOps.IsDefaultUnlocked(node))
            {
                return Loc.Get("grid.cost.default-unlock");
            }
            if (isActive)
            {
                return Loc.Get("grid.cost.activated");
            }

            // An Essence node is priced in Essence instead of XP, so a 0 XP line is left out.
            var parts = new List<string>();
            if (node.XpCost > 0 || !SphereGridOps.HasEssenceCost(node))
            {
                parts.Add(Loc.Format("grid.cost.xp", ("xp", node.XpCost)));
            }
            if (SphereGridOps.HasEssenceCost(node))
            {
                parts.Add(Loc.Format("grid.cost.essence", ("essence", node.EssenceCost)));
            }
            string materials = DescribeMaterialCost(node);
            if (!string.IsNullOrEmpty(materials))
            {
                parts.Add(materials);
            }
            return Loc.Format("grid.cost.costs", ("price", string.Join(" + ", parts)));
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
                case NodeUiState.Gated:
                    return "sg-node--gated";
                default:
                    return "sg-node--locked";
            }
        }

        /// <summary>
        /// What one ability upgrade adds, in the words the grid screen and the payload line share:
        /// "+2 power", "+15% crit chance", "+1 turn", "+10% to land its debuffs", "+1 charge a run".
        /// </summary>
        public static string DescribeUpgrade(AbilityUpgradeType type, int amount)
        {
            switch (type)
            {
                case AbilityUpgradeType.Power:
                    return Loc.Format("grid.upgrade.power", ("amount", amount));
                case AbilityUpgradeType.CritChance:
                    return Loc.Format("grid.upgrade.crit-chance", ("amount", amount));
                case AbilityUpgradeType.Duration:
                    return amount == 1 ? Loc.Get("grid.upgrade.duration.one") : Loc.Format("grid.upgrade.duration.many", ("amount", amount));
                case AbilityUpgradeType.ApplyChance:
                    return Loc.Format("grid.upgrade.apply-chance", ("amount", amount));
                case AbilityUpgradeType.Charges:
                    return amount == 1 ? Loc.Get("grid.upgrade.charges.one") : Loc.Format("grid.upgrade.charges.many", ("amount", amount));
                default:
                    return type.ToString();
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
                case SphereNodeKind.MagicAwaken:
                case SphereNodeKind.MagicUpgrade:
                case SphereNodeKind.Summon:
                case SphereNodeKind.SummonPower:
                case SphereNodeKind.SummonDuration:
                case SphereNodeKind.SummonCharge:
                case SphereNodeKind.SummonSize:
                case SphereNodeKind.SummonPromote:
                case SphereNodeKind.SummonApplyChance:
                case SphereNodeKind.SummonDamage:
                case SphereNodeKind.Ultra:
                    return "sg-node--slot";
                default:
                    return "sg-node--stat";
            }
        }

        /// <summary>
        /// What is drawn inside a node: one character for the special kinds, and for a stat node the
        /// short name of the stat it grows most ("STR", "HP") - every stat node used to read "S", so
        /// HP, STR, AGI and SPR looked the same until clicked (playtest 2 finding 7).
        /// </summary>
        public static string Glyph(SphereGridNode node, bool isStart)
        {
            // Every grid starts on the hero's first ability since 2026-10-03, and a star would hide
            // what it teaches - the start ring (sg-node--start) already says where the grid begins.
            if (isStart && (node == null || node.Kind != SphereNodeKind.MagicKnown))
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
                    return Loc.Get("grid.glyph.resistance");
                case SphereNodeKind.MagicSlot:
                    // "+1": one more ability carried. It read "M" (for the code's MagicSlot), which
                    // told the player nothing (playtest 3 finding 4).
                    return "+1";
                case SphereNodeKind.MagicKnown:
                    return "✦";
                case SphereNodeKind.MagicAwaken:
                    return "✧";
                case SphereNodeKind.MagicUpgrade:
                    return "▲";
                case SphereNodeKind.Summon:
                    return "◆";
                case SphereNodeKind.Ultra:
                    return "✸";
                case SphereNodeKind.SummonPower:
                case SphereNodeKind.SummonDuration:
                case SphereNodeKind.SummonCharge:
                case SphereNodeKind.SummonSize:
                case SphereNodeKind.SummonPromote:
                case SphereNodeKind.SummonApplyChance:
                case SphereNodeKind.SummonDamage:
                    return "+";
                default:
                    return StatGlyph(node.Gains);
            }
        }

        /// <summary>The short name of a stat node's largest gain; "S" for a node that grants nothing.</summary>
        private static string StatGlyph(StatBlock gains)
        {
            UnitStat largest = null;
            if (gains != null)
            {
                foreach (var gain in gains.NonZero())
                {
                    if (largest == null || gain.Amount > largest.Amount)
                    {
                        largest = gain;
                    }
                }
            }
            return largest == null ? Loc.Get("grid.glyph.stat") : StatCatalog.ShortName(largest.Type);
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
                return Loc.Format("grid.payload.resistance", ("element", node.ResistType), ("percent", node.ResistPercent.ToString("0")));
            }
            if (node.Kind == SphereNodeKind.MagicSlot)
            {
                return Loc.Get("grid.payload.ability-slot");
            }
            if (node.Kind == SphereNodeKind.MagicKnown)
            {
                // Two things the player has to be able to read off this line. That the spell is
                // *learned*, not automatically carried - it still has to win a slot on the Spells
                // screen - and the charge count, which is the whole run's allowance of it and so is
                // as much the payload as the spell name is.
                string name = string.IsNullOrEmpty(node.GrantedMagicKey) ? Loc.Get("grid.payload.unset") : node.GrantedMagicKey;
                return Loc.Format("grid.payload.learns", ("ability", name), ("charges", Mathf.Max(1, node.GrantedCharges)));
            }
            if (node.Kind == SphereNodeKind.MagicAwaken)
            {
                string from = string.IsNullOrEmpty(node.GrantedMagicKey) ? Loc.Get("grid.payload.unset") : node.GrantedMagicKey;
                string to = string.IsNullOrEmpty(node.AwakenedMagicKey) ? Loc.Get("grid.payload.unset") : node.AwakenedMagicKey;
                return Loc.Format("grid.payload.awakens", ("from", from), ("to", to));
            }
            if (node.Kind == SphereNodeKind.MagicUpgrade)
            {
                string name = string.IsNullOrEmpty(node.GrantedMagicKey) ? Loc.Get("grid.payload.unset") : node.GrantedMagicKey;
                return Loc.Format("grid.payload.ability-upgrade", ("ability", name), ("upgrade", DescribeUpgrade(node.UpgradeType, node.UpgradeAmount)));
            }
            if (node.Kind == SphereNodeKind.Ultra)
            {
                string ultra = string.IsNullOrEmpty(node.GrantedUltraKey) ? Loc.Get("grid.payload.unset") : node.GrantedUltraKey;
                return Loc.Format("grid.payload.ultra", ("ultra", ultra));
            }
            string summon = string.IsNullOrEmpty(node.GrantedSummonKey) ? Loc.Get("grid.payload.unset") : node.GrantedSummonKey;
            if (node.Kind == SphereNodeKind.Summon)
            {
                // Always carried - a summon takes no ability slot - and cast from its own command.
                return Loc.Format("grid.payload.summon", ("summon", summon));
            }
            if (node.Kind == SphereNodeKind.SummonPower)
            {
                return Loc.Format("grid.payload.summon-power", ("summon", summon), ("amount", node.SummonAmount));
            }
            if (node.Kind == SphereNodeKind.SummonDuration)
            {
                return node.SummonAmount == 1
                    ? Loc.Format("grid.payload.summon-duration.one", ("summon", summon), ("amount", node.SummonAmount))
                    : Loc.Format("grid.payload.summon-duration.many", ("summon", summon), ("amount", node.SummonAmount));
            }
            if (node.Kind == SphereNodeKind.SummonCharge)
            {
                return node.SummonAmount == 1
                    ? Loc.Format("grid.payload.summon-charge.one", ("summon", summon), ("amount", node.SummonAmount))
                    : Loc.Format("grid.payload.summon-charge.many", ("summon", summon), ("amount", node.SummonAmount));
            }
            if (node.Kind == SphereNodeKind.SummonSize)
            {
                return node.SummonAmount == 1
                    ? Loc.Format("grid.payload.summon-size.one", ("summon", summon), ("amount", node.SummonAmount))
                    : Loc.Format("grid.payload.summon-size.many", ("summon", summon), ("amount", node.SummonAmount));
            }
            if (node.Kind == SphereNodeKind.SummonPromote)
            {
                return node.SummonAmount == 1
                    ? Loc.Format("grid.payload.summon-promote.one", ("summon", summon))
                    : Loc.Format("grid.payload.summon-promote.many", ("summon", summon), ("amount", node.SummonAmount));
            }
            if (node.Kind == SphereNodeKind.SummonApplyChance)
            {
                return Loc.Format("grid.payload.summon-apply-chance", ("summon", summon), ("amount", node.SummonAmount));
            }
            if (node.Kind == SphereNodeKind.SummonDamage)
            {
                return Loc.Format("grid.payload.summon-damage", ("summon", summon), ("amount", node.SummonAmount));
            }

            var parts = new List<string>();
            if (node.Gains != null)
            {
                foreach (var entry in node.Gains.NonZero())
                {
                    parts.Add(Loc.Format("grid.payload.stat-gain", ("amount", entry.Amount), ("stat", StatCatalog.ShortName(entry.Type))));
                }
            }
            return parts.Count > 0 ? string.Join(" · ", parts) : Loc.Get("grid.payload.nothing");
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
                    IsStart = node.Key == start,
                    Requires = RequiredKeys(node)
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

        /// <summary>The node's required keys, blanks and repeats dropped; null when it has none.</summary>
        private static List<string> RequiredKeys(SphereGridNode node)
        {
            if (!SphereGridOps.HasRequirements(node))
            {
                return null;
            }
            var keys = new List<string>();
            foreach (var key in node.RequiredNodes)
            {
                if (!string.IsNullOrEmpty(key) && !keys.Contains(key))
                {
                    keys.Add(key);
                }
            }
            return keys;
        }

        /// <summary>
        /// Why a gated node is shut, naming what it waits for: "Requires Cairn Golem first." or, for
        /// several, "Requires Cairn Golem and Boar first." Empty when nothing is missing.
        /// </summary>
        public static string DescribeMissingRequirements(SphereGridSO grid, ICollection<string> activated, string key,
            Func<SphereGridNode, string> nameOf = null)
        {
            var missing = SphereGridOps.MissingRequirements(grid, activated, key);
            if (missing.Count == 0)
            {
                return "";
            }
            var names = new List<string>();
            foreach (var required in missing)
            {
                var node = SphereGridOps.FindNode(grid, required);
                names.Add(node == null ? required : (nameOf != null ? nameOf(node) : NodeName(node)));
            }
            string list = names.Count == 1
                ? names[0]
                : Loc.Format("grid.requires.list", ("names", string.Join(", ", names.GetRange(0, names.Count - 1))), ("last", names[names.Count - 1]));
            return Loc.Format("grid.requires", ("list", list));
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
                    return Loc.Get("grid.kind.resistance");
                case SphereNodeKind.MagicSlot:
                    return Loc.Get("grid.kind.ability-slot");
                case SphereNodeKind.MagicKnown:
                    return Loc.Get("grid.kind.known-ability");
                case SphereNodeKind.MagicAwaken:
                    return Loc.Get("grid.kind.awakening");
                case SphereNodeKind.MagicUpgrade:
                    return Loc.Get("grid.kind.ability-upgrade");
                case SphereNodeKind.Summon:
                    return Loc.Get("grid.kind.summon");
                case SphereNodeKind.Ultra:
                    return Loc.Get("grid.kind.ultra");
                case SphereNodeKind.SummonPower:
                case SphereNodeKind.SummonDuration:
                case SphereNodeKind.SummonCharge:
                case SphereNodeKind.SummonSize:
                case SphereNodeKind.SummonPromote:
                case SphereNodeKind.SummonApplyChance:
                case SphereNodeKind.SummonDamage:
                    return Loc.Get("grid.kind.summon-upgrade");
                default:
                    return Loc.Get("grid.kind.stat");
            }
        }
    }
}
