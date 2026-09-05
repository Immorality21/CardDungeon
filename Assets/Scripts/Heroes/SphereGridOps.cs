using System.Collections.Generic;
using Assets.Scripts.Combat;
using Assets.Scripts.UnitStats;
using UnityEngine;

namespace Assets.Scripts.Heroes
{
    /// <summary>
    /// Every sphere-grid rule, as pure static functions of the grid asset plus a set of activated
    /// node keys. Nothing here touches disk, singletons or scenes — the balance model and
    /// room-event placement both re-derive stats through this before a <see cref="Hero"/> exists,
    /// and the EditMode tests drive it directly.
    ///
    /// <para>Grids are treated as untrusted data: dangling neighbour keys are dropped, duplicate
    /// keys resolve to the first node in the list, and empty keys are ignored — a half-edited grid
    /// degrades instead of throwing. The analyzer reports those authoring faults; the rules just
    /// survive them.</para>
    /// </summary>
    public static class SphereGridOps
    {
        /// <summary>
        /// Share of the roster's average lifetime XP a new recruit arrives with, banked and
        /// unspent. Less than 1 so a recruit trails the veterans; enough that a late hire is not a
        /// hero the player cannot afford to build.
        /// </summary>
        public const float RecruitSeedRate = 0.55f;

        /// <summary>The entry node's key: <see cref="SphereGridSO.StartNodeKey"/>, falling back to
        /// the first node in the list. Empty string when the grid is null or empty.</summary>
        public static string StartKey(SphereGridSO grid)
        {
            if (grid == null || grid.Nodes == null)
            {
                return "";
            }

            if (!string.IsNullOrEmpty(grid.StartNodeKey) && FindNode(grid, grid.StartNodeKey) != null)
            {
                return grid.StartNodeKey;
            }

            foreach (var node in grid.Nodes)
            {
                if (node != null && !string.IsNullOrEmpty(node.Key))
                {
                    return node.Key;
                }
            }
            return "";
        }

        /// <summary>First node with the given key, or null. Null-safe on every argument.</summary>
        public static SphereGridNode FindNode(SphereGridSO grid, string key)
        {
            if (grid == null || grid.Nodes == null || string.IsNullOrEmpty(key))
            {
                return null;
            }

            foreach (var node in grid.Nodes)
            {
                if (node != null && node.Key == key)
                {
                    return node;
                }
            }
            return null;
        }

        /// <summary>
        /// Whether this node is active from the moment the hero exists - never bought, never
        /// charged, and open on a save with no activations at all.
        /// </summary>
        public static bool IsDefaultUnlocked(SphereGridNode node)
        {
            return node != null && node.UnlockedByDefault && !string.IsNullOrEmpty(node.Key);
        }

        /// <summary>Keys of every <see cref="SphereGridNode.UnlockedByDefault"/> node, in grid
        /// order.</summary>
        public static List<string> DefaultUnlockedKeys(SphereGridSO grid)
        {
            var keys = new List<string>();
            if (grid == null || grid.Nodes == null)
            {
                return keys;
            }

            foreach (var node in grid.Nodes)
            {
                if (IsDefaultUnlocked(node) && !keys.Contains(node.Key))
                {
                    keys.Add(node.Key);
                }
            }
            return keys;
        }

        /// <summary>
        /// Every node that currently counts as active: what the save paid for, plus what the grid
        /// hands over for free. <b>Every rule below reads this rather than the saved list</b>, so a
        /// default-unlocked node grants its payload, opens its neighbours and refuses to be bought
        /// twice without any of those three being remembered separately - and so a save written
        /// before the node was marked default needs no migration.
        ///
        /// <para>Saved keys come first, so the list still reads as the player's own history, and
        /// the result is deduped: a node bought before it was marked default appears once.</para>
        /// </summary>
        public static List<string> ActiveNodes(SphereGridSO grid, IEnumerable<string> activated)
        {
            var result = new List<string>();
            if (activated != null)
            {
                foreach (var key in activated)
                {
                    if (!string.IsNullOrEmpty(key) && !result.Contains(key))
                    {
                        result.Add(key);
                    }
                }
            }

            foreach (var key in DefaultUnlockedKeys(grid))
            {
                if (!result.Contains(key))
                {
                    result.Add(key);
                }
            }
            return result;
        }

        /// <summary>Whether this node charges materials on top of XP.</summary>
        public static bool HasMaterialCost(SphereGridNode node)
        {
            if (node == null || node.MaterialCosts == null)
            {
                return false;
            }

            foreach (var line in node.MaterialCosts)
            {
                if (line != null && line.IsValid)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Symmetric adjacency over the authored <see cref="SphereGridNode.Neighbors"/> lists:
        /// listing B on A links both directions, duplicates collapse, and neighbour keys that match
        /// no node are dropped. Every node with a key gets an entry, even if isolated.
        /// </summary>
        public static Dictionary<string, List<string>> BuildAdjacency(SphereGridSO grid)
        {
            var adjacency = new Dictionary<string, List<string>>();
            if (grid == null || grid.Nodes == null)
            {
                return adjacency;
            }

            foreach (var node in grid.Nodes)
            {
                if (node != null && !string.IsNullOrEmpty(node.Key) && !adjacency.ContainsKey(node.Key))
                {
                    adjacency[node.Key] = new List<string>();
                }
            }

            foreach (var node in grid.Nodes)
            {
                if (node == null || string.IsNullOrEmpty(node.Key) || node.Neighbors == null)
                {
                    continue;
                }

                foreach (var neighbor in node.Neighbors)
                {
                    // Dangling keys (no such node) are dropped; self-links make no sense.
                    if (string.IsNullOrEmpty(neighbor) || neighbor == node.Key || !adjacency.ContainsKey(neighbor))
                    {
                        continue;
                    }

                    if (!adjacency[node.Key].Contains(neighbor))
                    {
                        adjacency[node.Key].Add(neighbor);
                    }
                    if (!adjacency[neighbor].Contains(node.Key))
                    {
                        adjacency[neighbor].Add(node.Key);
                    }
                }
            }

            return adjacency;
        }

        /// <summary>
        /// Whether a node can be bought next, ignoring cost: it is the start node, or adjacent to a
        /// node that is already active - bought, or <see cref="SphereGridNode.UnlockedByDefault"/>.
        /// The start node itself never needs a neighbour, so a fresh hero always has somewhere to
        /// begin.
        ///
        /// <para>Adjacency to the start node used to count on its own, bought or not, so a new
        /// hero's <i>second</i> node was purchasable while the first still read as unbought - and
        /// the entry node could be skipped outright. The frontier now only ever grows out of
        /// something the hero actually holds.</para>
        /// </summary>
        public static bool IsReachable(SphereGridSO grid, ICollection<string> activated, string key)
        {
            var adjacency = BuildAdjacency(grid);
            return IsReachable(grid, adjacency, ActiveNodes(grid, activated), key);
        }

        private static bool IsReachable(
            SphereGridSO grid,
            Dictionary<string, List<string>> adjacency,
            ICollection<string> active,
            string key)
        {
            if (string.IsNullOrEmpty(key) || !adjacency.ContainsKey(key))
            {
                return false;
            }

            if (key == StartKey(grid))
            {
                return true;
            }

            foreach (var neighbor in adjacency[key])
            {
                if (active != null && active.Contains(neighbor))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Full XP-side activation gate: the node exists, is not already active, is reachable, and
        /// the bank covers its cost. Deliberately says nothing about <see cref="HasMaterialCost"/> -
        /// that half is checked where the inventory lives (<c>HeroRoster.TryActivateNode</c>), so
        /// the balance model can keep spending a grid with no inventory to spend from.
        /// </summary>
        public static bool CanActivate(SphereGridSO grid, ICollection<string> activated, int bank, string key)
        {
            var node = FindNode(grid, key);
            if (node == null)
            {
                return false;
            }

            var active = ActiveNodes(grid, activated);
            if (active.Contains(key))
            {
                return false;
            }

            if (!IsReachable(grid, BuildAdjacency(grid), active, key))
            {
                return false;
            }

            return bank >= node.XpCost;
        }

        /// <summary>
        /// The pure core of hub activation: validate against the save entry, then spend the bank
        /// and append the key. Returns false without mutating anything on any failure. Callers own
        /// persisting the entry (see <c>HeroRoster.TryActivateNode</c>).
        /// </summary>
        public static bool TryActivate(SphereGridSO grid, HeroSaveData entry, string key)
        {
            if (entry == null)
            {
                return false;
            }

            if (entry.ActivatedNodes == null)
            {
                entry.ActivatedNodes = new List<string>();
            }

            if (!CanActivate(grid, entry.ActivatedNodes, entry.CurrentXp, key))
            {
                return false;
            }

            var node = FindNode(grid, key);
            entry.CurrentXp -= node.XpCost;
            entry.ActivatedNodes.Add(key);
            return true;
        }

        /// <summary>
        /// Saved activation keys with unknown keys and duplicates dropped. Deliberately does
        /// <b>not</b> drop activated nodes that are no longer reachable: re-authoring a grid's
        /// edges must not strip grants the player already paid for.
        /// </summary>
        public static List<string> SanitizeActivated(SphereGridSO grid, IEnumerable<string> saved)
        {
            var result = new List<string>();
            if (saved == null)
            {
                return result;
            }

            foreach (var key in saved)
            {
                if (FindNode(grid, key) != null && !result.Contains(key))
                {
                    result.Add(key);
                }
            }
            return result;
        }

        /// <summary>Summed <see cref="SphereGridNode.Gains"/> of every activated Stat node.</summary>
        public static StatBlock StatsForNodes(SphereGridSO grid, IEnumerable<string> activated)
        {
            var block = new StatBlock();
            foreach (var key in ActiveNodes(grid, activated))
            {
                var node = FindNode(grid, key);
                if (node != null && node.Kind == SphereNodeKind.Stat && node.Gains != null)
                {
                    block.Add(node.Gains);
                }
            }
            return block;
        }

        /// <summary>Activated Resistance nodes, summed into one entry per damage type — the same
        /// convention as gear resistance.</summary>
        public static List<Resistance> ResistancesForNodes(SphereGridSO grid, IEnumerable<string> activated)
        {
            var totals = new Dictionary<DamageType, float>();
            foreach (var key in ActiveNodes(grid, activated))
            {
                var node = FindNode(grid, key);
                if (node == null || node.Kind != SphereNodeKind.Resistance)
                {
                    continue;
                }

                if (totals.ContainsKey(node.ResistType))
                {
                    totals[node.ResistType] += node.ResistPercent;
                }
                else
                {
                    totals[node.ResistType] = node.ResistPercent;
                }
            }

            var result = new List<Resistance>();
            foreach (var pair in totals)
            {
                result.Add(new Resistance { DamageType = pair.Key, Percent = pair.Value });
            }
            return result;
        }

        /// <summary>
        /// How many extra equipped-magic slots the activated nodes grant: +1 per MagicSlot node,
        /// and nothing else.
        ///
        /// <para>A MagicKnown node used to bring its own slot, because under Draw the two were the
        /// same thing - magic went straight into a slot, and a spell you knew was a spell you had.
        /// With Draw gone (2026-09-04) knowing and carrying separate: a grid teaches more spells
        /// than a hero can take in, and the gap between the two is the whole reason a kit is a
        /// decision. If MagicKnown still paid for its own slot there would be no gap and no
        /// decision, and MagicSlot nodes would be buying nothing.</para>
        /// </summary>
        public static int SlotBonusForNodes(SphereGridSO grid, IEnumerable<string> activated)
        {
            int bonus = 0;
            foreach (var key in ActiveNodes(grid, activated))
            {
                var node = FindNode(grid, key);
                if (node != null && node.Kind == SphereNodeKind.MagicSlot)
                {
                    bonus += 1;
                }
            }
            return bonus;
        }

        /// <summary>
        /// Magic this hero permanently knows, as (key, charges) pairs in node order - the pool their
        /// hub loadout picks from, and the <b>only</b> source of magic in the game since Draw was
        /// removed on 2026-09-04.
        ///
        /// <para>Node order, not activation order, so a hero's known list reads the same however
        /// they bought it - which is what makes the auto-fill in <c>MagicLoadoutOps.Resolve</c>
        /// deterministic rather than dependent on click history.</para>
        ///
        /// <para>Keys rather than <c>MagicSO</c> references on purpose: the Heroes layer does not know
        /// about Cards (the dependency runs the other way, and the saves reference magic by key too),
        /// so resolution happens where the catalog lives. Duplicate keys collapse - two nodes teaching
        /// the same spell teach it once, at the higher charge count.</para>
        /// </summary>
        public static List<KeyValuePair<string, int>> KnownMagicForNodes(
            SphereGridSO grid, IEnumerable<string> activated)
        {
            var granted = new List<KeyValuePair<string, int>>();
            if (grid == null || grid.Nodes == null)
            {
                return granted;
            }

            var owned = ActiveNodes(grid, activated);

            // Walk the grid, not the save: activation order is click history, and the loadout's
            // auto-fill reads this list positionally.
            foreach (var node in grid.Nodes)
            {
                if (node == null
                    || node.Kind != SphereNodeKind.MagicKnown
                    || string.IsNullOrEmpty(node.Key)
                    || string.IsNullOrEmpty(node.GrantedMagicKey)
                    || !owned.Contains(node.Key))
                {
                    continue;
                }

                int charges = Mathf.Max(1, node.GrantedCharges);
                int existing = granted.FindIndex(pair => pair.Key == node.GrantedMagicKey);
                if (existing >= 0)
                {
                    if (charges > granted[existing].Value)
                    {
                        granted[existing] = new KeyValuePair<string, int>(node.GrantedMagicKey, charges);
                    }
                    continue;
                }

                granted.Add(new KeyValuePair<string, int>(node.GrantedMagicKey, charges));
            }

            return granted;
        }

        /// <summary>XP spent to activate the given keys, at current node prices. Unknown keys count 0.</summary>
        /// <summary>
        /// Edge distance from <c>StartNodeKey</c> to every node, so pricing and reporting share one
        /// notion of "how far in is this". Edges are undirected; unreachable nodes are absent.
        /// </summary>
        public static Dictionary<string, int> DepthsFrom(SphereGridSO grid)
        {
            var depths = new Dictionary<string, int>();
            if (grid == null || grid.Nodes == null || grid.Nodes.Count == 0)
            {
                return depths;
            }

            string start = !string.IsNullOrEmpty(grid.StartNodeKey)
                ? grid.StartNodeKey
                : (grid.Nodes[0] != null ? grid.Nodes[0].Key : null);
            if (string.IsNullOrEmpty(start))
            {
                return depths;
            }

            var adjacency = new Dictionary<string, List<string>>();
            foreach (var node in grid.Nodes)
            {
                if (node == null || string.IsNullOrEmpty(node.Key))
                {
                    continue;
                }
                if (!adjacency.ContainsKey(node.Key))
                {
                    adjacency[node.Key] = new List<string>();
                }
                foreach (var neighbor in node.Neighbors)
                {
                    if (string.IsNullOrEmpty(neighbor))
                    {
                        continue;
                    }
                    adjacency[node.Key].Add(neighbor);
                    if (!adjacency.ContainsKey(neighbor))
                    {
                        adjacency[neighbor] = new List<string>();
                    }
                    adjacency[neighbor].Add(node.Key);
                }
            }

            depths[start] = 0;
            var frontier = new List<string> { start };
            while (frontier.Count > 0)
            {
                var next = new List<string>();
                foreach (var key in frontier)
                {
                    if (!adjacency.TryGetValue(key, out var neighbors))
                    {
                        continue;
                    }
                    foreach (var neighbor in neighbors)
                    {
                        if (depths.ContainsKey(neighbor))
                        {
                            continue;
                        }
                        depths[neighbor] = depths[key] + 1;
                        next.Add(neighbor);
                    }
                }
                frontier = next;
            }
            return depths;
        }

        /// <summary>
        /// The authored price of a node <paramref name="depth"/> edges from the start.
        ///
        /// <para><b>Superlinear on purpose.</b> Before 2026-09-02 the whole spread was 15..80 — 5x
        /// across an entire grid — and a grid filled in about one pass of the campaign. Cost rising
        /// with depth keeps the first nodes cheap, so a new hero still feels like it is moving, while
        /// the far reaches become a long-term goal. Same pacing FFX gets from sphere scarcity,
        /// achieved with price instead. See <c>docs/BALANCING.md</c> §5s.</para>
        ///
        /// <para>Rounded to 5 so a price reads cleanly in the grid UI.</para>
        /// </summary>
        public static int CostForDepth(int depth)
        {
            float raw = CostBase + CostFactor * Mathf.Pow(Mathf.Max(0, depth), CostExponent);
            return Mathf.RoundToInt(raw / 5f) * 5;
        }

        public const int CostBase = 15;
        public const float CostFactor = 3.5f;
        public const float CostExponent = 1.9f;

        public static int TotalCostOf(SphereGridSO grid, IEnumerable<string> activated)
        {
            int total = 0;
            if (activated == null)
            {
                return total;
            }

            var counted = new List<string>();
            foreach (var key in activated)
            {
                var node = FindNode(grid, key);
                if (node != null && !counted.Contains(key))
                {
                    counted.Add(key);
                    total += node.XpCost;
                }
            }
            return total;
        }

        /// <summary>Cost of activating the entire grid.</summary>
        public static int TotalGridCost(SphereGridSO grid)
        {
            int total = 0;
            if (grid == null || grid.Nodes == null)
            {
                return total;
            }

            var counted = new List<string>();
            foreach (var node in grid.Nodes)
            {
                // Default unlocks are handed over, not bought, so they are not part of what the
                // grid costs to complete.
                if (node != null && !string.IsNullOrEmpty(node.Key) && !counted.Contains(node.Key)
                    && !IsDefaultUnlocked(node))
                {
                    counted.Add(node.Key);
                    total += node.XpCost;
                }
            }
            return total;
        }

        /// <summary>Every reachable, unactivated node — the UI's "can buy next" set and the
        /// analyzer's probe. Ordered by position in <see cref="SphereGridSO.Nodes"/>.</summary>
        public static List<SphereGridNode> Frontier(SphereGridSO grid, ICollection<string> activated)
        {
            var result = new List<SphereGridNode>();
            if (grid == null || grid.Nodes == null)
            {
                return result;
            }

            var adjacency = BuildAdjacency(grid);
            var active = ActiveNodes(grid, activated);
            var seen = new List<string>();
            foreach (var node in grid.Nodes)
            {
                if (node == null || string.IsNullOrEmpty(node.Key) || seen.Contains(node.Key))
                {
                    continue;
                }
                seen.Add(node.Key);

                if (active.Contains(node.Key))
                {
                    continue;
                }

                if (IsReachable(grid, adjacency, active, node.Key))
                {
                    result.Add(node);
                }
            }
            return result;
        }

        /// <summary>Cheapest frontier node's cost, or -1 when the frontier is empty (grid fully
        /// activated, or no grid at all).</summary>
        public static int CheapestFrontierCost(SphereGridSO grid, ICollection<string> activated)
        {
            var frontier = Frontier(grid, activated);
            if (frontier.Count == 0)
            {
                return -1;
            }

            int cheapest = int.MaxValue;
            foreach (var node in frontier)
            {
                if (node.XpCost < cheapest)
                {
                    cheapest = node.XpCost;
                }
            }
            return cheapest;
        }

        /// <summary>
        /// Deterministic budget spend for the balance model: repeatedly activate the cheapest
        /// affordable frontier node. Ties break on the node's index in <see cref="SphereGridSO.Nodes"/>
        /// — list order, not key order, so the authored spine is the preference and renaming a key
        /// never changes the model. Returns the full activated set (including
        /// <paramref name="alreadyActivated"/>); <paramref name="spent"/> is the XP consumed from
        /// <paramref name="budget"/> by the new activations only.
        /// </summary>
        public static List<string> GreedySpend(
            SphereGridSO grid,
            IEnumerable<string> alreadyActivated,
            int budget,
            out int spent)
        {
            var activated = SanitizeActivated(grid, alreadyActivated);
            spent = 0;
            if (grid == null || grid.Nodes == null)
            {
                return activated;
            }

            while (true)
            {
                var frontier = Frontier(grid, activated);
                SphereGridNode pick = null;
                foreach (var node in frontier)
                {
                    // Frontier preserves Nodes order, so "strictly cheaper" keeps the first
                    // (lowest-index) node on a cost tie.
                    if (node.XpCost <= budget - spent && (pick == null || node.XpCost < pick.XpCost))
                    {
                        pick = node;
                    }
                }

                if (pick == null)
                {
                    return activated;
                }

                spent += pick.XpCost;
                activated.Add(pick.Key);
            }
        }

        /// <summary>Lifetime XP a save entry represents: the unspent bank plus the cost of
        /// everything activated, at current node prices.</summary>
        public static int LifetimeXpFor(SphereGridSO grid, HeroSaveData entry)
        {
            if (entry == null)
            {
                return 0;
            }

            return entry.CurrentXp + TotalCostOf(grid, entry.ActivatedNodes);
        }

        /// <summary>
        /// Starter bank for a new recruit: <see cref="RecruitSeedRate"/> × the average lifetime XP
        /// of the heroes already owned, floored. One pure function so the tavern, the dungeon
        /// rescue and the balance model cannot drift. Zero for an empty roster.
        /// </summary>
        public static int StarterBank(IReadOnlyList<int> ownedLifetimeXp)
        {
            if (ownedLifetimeXp == null || ownedLifetimeXp.Count == 0)
            {
                return 0;
            }

            float total = 0f;
            foreach (var xp in ownedLifetimeXp)
            {
                total += Mathf.Max(0, xp);
            }
            return Mathf.FloorToInt(RecruitSeedRate * (total / ownedLifetimeXp.Count));
        }
    }
}
