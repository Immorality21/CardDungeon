using System.Collections.Generic;

namespace Assets.Scripts.Dungeon
{
    /// <summary>
    /// The campaign's rules, as pure functions: which runs a save may start, and whether a campaign
    /// asset is authored soundly. No Unity objects beyond the assets themselves, no managers, no
    /// save-file access - callers pass the completed-run keys in - so the whole progression graph is
    /// testable without entering play mode. Same split as <c>SphereGridOps</c> and <c>PartySlots</c>.
    /// </summary>
    public static class CampaignOps
    {
        /// <summary>
        /// The save key for a run. Mirrors how <c>MainMenuManager</c> and <c>DungeonManager</c> write
        /// <c>RunSaveData.RunKey</c>: the explicit Key when set, otherwise the asset name.
        /// </summary>
        public static string RunKeyOf(RunDefinitionSO run)
        {
            if (run == null)
            {
                return string.Empty;
            }
            return !string.IsNullOrEmpty(run.Key) ? run.Key : run.name;
        }

        /// <summary>
        /// Whether a node's prerequisites are satisfied. A node with no prerequisites is a starting
        /// point and is always unlocked.
        /// </summary>
        public static bool IsUnlocked(CampaignNodeEntry node, ICollection<string> completedRunKeys)
        {
            if (node == null)
            {
                return false;
            }

            var required = node.Requires;
            if (required == null || required.Count == 0)
            {
                return true;
            }

            bool anySatisfied = false;
            foreach (var prerequisite in required)
            {
                if (prerequisite == null)
                {
                    continue;
                }

                bool done = completedRunKeys != null && completedRunKeys.Contains(RunKeyOf(prerequisite));
                if (node.UnlockMode == CampaignUnlockMode.All && !done)
                {
                    return false;
                }
                if (done)
                {
                    anySatisfied = true;
                }
            }

            return node.UnlockMode == CampaignUnlockMode.All || anySatisfied;
        }

        /// <summary>
        /// Resolves one node against a save. <paramref name="activeRunKey"/> is
        /// <c>RunSaveData.RunKey</c> - empty when no run is underway.
        ///
        /// <para>While a run is in progress every other node is un-startable: starting a second run
        /// would overwrite <c>Run.json</c> and silently discard the first one's progress. The player
        /// finishes or dies out of a run; the map does not offer a quiet way to abandon it.</para>
        /// </summary>
        public static CampaignNodeState GetState(
            CampaignNodeEntry node,
            ICollection<string> completedRunKeys,
            string activeRunKey)
        {
            var state = new CampaignNodeState { Node = node };
            if (node?.Run == null)
            {
                state.Status = CampaignNodeStatus.Hidden;
                return state;
            }

            string key = RunKeyOf(node.Run);
            bool completed = completedRunKeys != null && completedRunKeys.Contains(key);
            bool unlocked = IsUnlocked(node, completedRunKeys);
            bool isActive = !string.IsNullOrEmpty(activeRunKey) && activeRunKey == key;
            bool runInProgressElsewhere = !string.IsNullOrEmpty(activeRunKey) && !isActive;

            if (isActive)
            {
                state.Status = CampaignNodeStatus.InProgress;
                state.CanContinue = true;
                return state;
            }

            if (!unlocked)
            {
                // A cleared run stays visible even if its prerequisites were later re-authored away;
                // hiding history would read as lost progress.
                state.Status = completed
                    ? CampaignNodeStatus.Completed
                    : node.Secret ? CampaignNodeStatus.Hidden : CampaignNodeStatus.Locked;
                if (state.Status == CampaignNodeStatus.Locked)
                {
                    state.MissingRequirements = GetMissingRequirementNames(node, completedRunKeys);
                }
                return state;
            }

            if (completed)
            {
                state.Status = CampaignNodeStatus.Completed;
                state.CanStart = node.Run.Repeatable && !runInProgressElsewhere;
                return state;
            }

            state.Status = CampaignNodeStatus.Available;
            state.CanStart = !runInProgressElsewhere;
            return state;
        }

        /// <summary>
        /// Whether this save has anywhere at all to go: some run it may start, or one to continue.
        /// The hub's one hard guarantee - a save that can reach the menu and find no way into any
        /// dungeon is stuck for good, which is exactly what a completed tutorial with no successor
        /// (or a mis-gated menu button) produces.
        /// </summary>
        public static bool HasSomethingToPlay(
            CampaignSO campaign,
            ICollection<string> completedRunKeys,
            string activeRunKey)
        {
            foreach (var state in GetStates(campaign, completedRunKeys, activeRunKey))
            {
                if (state.CanStart || state.CanContinue)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>Resolves every node in the campaign against a save, in authored order.</summary>
        public static List<CampaignNodeState> GetStates(
            CampaignSO campaign,
            ICollection<string> completedRunKeys,
            string activeRunKey)
        {
            var states = new List<CampaignNodeState>();
            if (campaign == null)
            {
                return states;
            }
            foreach (var node in campaign.Nodes)
            {
                if (node == null)
                {
                    continue;
                }
                states.Add(GetState(node, completedRunKeys, activeRunKey));
            }
            return states;
        }

        /// <summary>Display names of the prerequisites this save has not cleared yet.</summary>
        public static List<string> GetMissingRequirementNames(
            CampaignNodeEntry node,
            ICollection<string> completedRunKeys)
        {
            var missing = new List<string>();
            if (node?.Requires == null)
            {
                return missing;
            }
            foreach (var prerequisite in node.Requires)
            {
                if (prerequisite == null)
                {
                    continue;
                }
                if (completedRunKeys == null || !completedRunKeys.Contains(RunKeyOf(prerequisite)))
                {
                    missing.Add(DisplayNameOf(prerequisite));
                }
            }
            return missing;
        }

        /// <summary>What to call a run on screen: its DisplayName when authored, else its key.</summary>
        public static string DisplayNameOf(RunDefinitionSO run)
        {
            if (run == null)
            {
                return "(missing run)";
            }
            return !string.IsNullOrEmpty(run.DisplayName) ? run.DisplayName : RunKeyOf(run);
        }

        // --- Play order ------------------------------------------------------------------------

        /// <summary>
        /// How deep each run sits in the campaign: how many runs the player must clear before this
        /// one opens. This is the real play order - <c>RunDefinitionSO.SequenceIndex</c> is a
        /// hand-typed hint the graph now supersedes.
        ///
        /// <para>Resolved the same way unlocking is, so the tier matches the route a player actually
        /// takes: an <c>All</c> node sits one past its <b>deepest</b> prerequisite (it waits for the
        /// slowest), an <c>Any</c> node one past its <b>shallowest</b> (it opens on the first).
        /// Nodes inside a prerequisite cycle never resolve and are left at tier 0 - they can never be
        /// played at all, which <see cref="GetUnreachableNodes"/> reports as the authoring fault it
        /// is.</para>
        /// </summary>
        public static Dictionary<string, int> ComputeTiers(CampaignSO campaign)
        {
            var tiers = new Dictionary<string, int>();
            if (campaign == null)
            {
                return tiers;
            }

            foreach (var node in campaign.Nodes)
            {
                if (node?.Run != null)
                {
                    tiers[RunKeyOf(node.Run)] = 0;
                }
            }

            var resolved = new HashSet<string>();
            bool progressed = true;
            while (progressed)
            {
                progressed = false;
                foreach (var node in campaign.Nodes)
                {
                    if (node?.Run == null)
                    {
                        continue;
                    }
                    string key = RunKeyOf(node.Run);
                    if (resolved.Contains(key))
                    {
                        continue;
                    }

                    if (node.Requires == null || node.Requires.Count == 0)
                    {
                        tiers[key] = 0;
                        resolved.Add(key);
                        progressed = true;
                        continue;
                    }

                    bool all = node.UnlockMode == CampaignUnlockMode.All;
                    int deepest = 0;
                    int shallowest = int.MaxValue;
                    bool everyPrerequisiteResolved = true;
                    bool anyPrerequisiteResolved = false;

                    foreach (var prerequisite in node.Requires)
                    {
                        if (prerequisite == null)
                        {
                            continue;
                        }
                        string pk = RunKeyOf(prerequisite);
                        if (resolved.Contains(pk))
                        {
                            anyPrerequisiteResolved = true;
                            int t = tiers[pk];
                            if (t > deepest)
                            {
                                deepest = t;
                            }
                            if (t < shallowest)
                            {
                                shallowest = t;
                            }
                        }
                        else
                        {
                            everyPrerequisiteResolved = false;
                        }
                    }

                    if (all && everyPrerequisiteResolved)
                    {
                        tiers[key] = deepest + 1;
                        resolved.Add(key);
                        progressed = true;
                    }
                    else if (!all && anyPrerequisiteResolved)
                    {
                        tiers[key] = shallowest + 1;
                        resolved.Add(key);
                        progressed = true;
                    }
                }
            }

            return tiers;
        }

        /// <summary>
        /// The campaign's nodes shallowest-first, so a consumer can process a run only after
        /// everything that unlocks it. Ties keep authored order.
        /// </summary>
        public static List<CampaignNodeEntry> GetNodesInPlayOrder(CampaignSO campaign)
        {
            var ordered = new List<CampaignNodeEntry>();
            if (campaign == null)
            {
                return ordered;
            }
            foreach (var node in campaign.Nodes)
            {
                if (node?.Run != null)
                {
                    ordered.Add(node);
                }
            }

            var tiers = ComputeTiers(campaign);
            var authored = new Dictionary<CampaignNodeEntry, int>();
            for (int i = 0; i < ordered.Count; i++)
            {
                authored[ordered[i]] = i;
            }

            ordered.Sort((a, b) =>
            {
                tiers.TryGetValue(RunKeyOf(a.Run), out int ta);
                tiers.TryGetValue(RunKeyOf(b.Run), out int tb);
                int byTier = ta.CompareTo(tb);
                return byTier != 0 ? byTier : authored[a].CompareTo(authored[b]);
            });
            return ordered;
        }

        // --- Authoring validation --------------------------------------------------------------
        //
        // A campaign is a graph the player can be permanently stranded in, so the same guard-rail
        // treatment as manual level layouts: every way it can be authored wrong is a query the editor
        // window and a test can both run.

        /// <summary>Nodes with no run assigned - they can never be started or satisfy a prerequisite.</summary>
        public static List<int> GetNodesWithoutRun(CampaignSO campaign)
        {
            var broken = new List<int>();
            if (campaign == null)
            {
                return broken;
            }
            for (int i = 0; i < campaign.Nodes.Count; i++)
            {
                if (campaign.Nodes[i]?.Run == null)
                {
                    broken.Add(i);
                }
            }
            return broken;
        }

        /// <summary>
        /// Run keys appearing on more than one node. Two nodes for one run would both flip to
        /// Completed off a single clear, so the duplicate is never really playable.
        /// </summary>
        public static List<string> GetDuplicateRunKeys(CampaignSO campaign)
        {
            var duplicates = new List<string>();
            if (campaign == null)
            {
                return duplicates;
            }
            var seen = new HashSet<string>();
            foreach (var node in campaign.Nodes)
            {
                if (node?.Run == null)
                {
                    continue;
                }
                string key = RunKeyOf(node.Run);
                if (!seen.Add(key) && !duplicates.Contains(key))
                {
                    duplicates.Add(key);
                }
            }
            return duplicates;
        }

        /// <summary>
        /// Nodes requiring a run that is not itself a node in this campaign. Its key can never enter
        /// the completed set through the map, so the node is unreachable in practice.
        /// </summary>
        public static List<int> GetNodesWithOutsidePrerequisites(CampaignSO campaign)
        {
            var broken = new List<int>();
            if (campaign == null)
            {
                return broken;
            }

            var present = new HashSet<string>();
            foreach (var node in campaign.Nodes)
            {
                if (node?.Run != null)
                {
                    present.Add(RunKeyOf(node.Run));
                }
            }

            for (int i = 0; i < campaign.Nodes.Count; i++)
            {
                var node = campaign.Nodes[i];
                if (node?.Requires == null)
                {
                    continue;
                }
                foreach (var prerequisite in node.Requires)
                {
                    if (prerequisite == null || !present.Contains(RunKeyOf(prerequisite)))
                    {
                        broken.Add(i);
                        break;
                    }
                }
            }
            return broken;
        }

        /// <summary>Nodes with no prerequisites - where a fresh save can begin. A campaign needs at least one.</summary>
        public static List<int> GetRootNodes(CampaignSO campaign)
        {
            var roots = new List<int>();
            if (campaign == null)
            {
                return roots;
            }
            for (int i = 0; i < campaign.Nodes.Count; i++)
            {
                var node = campaign.Nodes[i];
                if (node?.Run == null)
                {
                    continue;
                }
                if (node.Requires == null || node.Requires.Count == 0)
                {
                    roots.Add(i);
                }
            }
            return roots;
        }

        /// <summary>
        /// Nodes that can never unlock, however well the player plays: prerequisite cycles, and
        /// anything downstream of one. Found by repeatedly clearing whatever is unlockable and seeing
        /// what is left - the same fixed-point walk the player performs one run at a time.
        /// </summary>
        public static List<int> GetUnreachableNodes(CampaignSO campaign)
        {
            var unreachable = new List<int>();
            if (campaign == null)
            {
                return unreachable;
            }

            var completed = new HashSet<string>();
            var resolved = new HashSet<int>();

            bool progressed = true;
            while (progressed)
            {
                progressed = false;
                for (int i = 0; i < campaign.Nodes.Count; i++)
                {
                    var node = campaign.Nodes[i];
                    if (node?.Run == null || resolved.Contains(i))
                    {
                        continue;
                    }
                    if (IsUnlocked(node, completed))
                    {
                        resolved.Add(i);
                        completed.Add(RunKeyOf(node.Run));
                        progressed = true;
                    }
                }
            }

            for (int i = 0; i < campaign.Nodes.Count; i++)
            {
                if (campaign.Nodes[i]?.Run != null && !resolved.Contains(i))
                {
                    unreachable.Add(i);
                }
            }
            return unreachable;
        }
    }
}
