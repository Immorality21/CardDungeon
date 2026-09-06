using System.Collections.Generic;
using Assets.Scripts.Heroes;

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
        /// Whether a node's prerequisites are satisfied. A node with no prerequisites of either kind
        /// is a starting point and is always unlocked.
        ///
        /// <para>There are two kinds of prerequisite and they compose as an AND: the runs in
        /// <c>Requires</c> (softened by <c>UnlockMode</c>), and the heroes in <c>RequiresHeroes</c>,
        /// which are always all-of. <paramref name="ownedHeroKeys"/> is
        /// <c>PartySaveData.OwnedHeroKeys</c> - null means the caller knows of no owned heroes, so a
        /// hero gate closes rather than opens. Failing shut is deliberate: a caller that forgets to
        /// pass the roster locks a run, which shows up immediately, instead of quietly handing the
        /// player a run the gate exists to withhold.</para>
        /// </summary>
        public static bool IsUnlocked(
            CampaignNodeEntry node,
            ICollection<string> completedRunKeys,
            ICollection<string> ownedHeroKeys = null)
        {
            if (node == null)
            {
                return false;
            }

            if (!HeroGateSatisfied(node, ownedHeroKeys))
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
            string activeRunKey,
            ICollection<string> ownedHeroKeys = null)
        {
            var state = new CampaignNodeState { Node = node };
            if (node?.Run == null)
            {
                state.Status = CampaignNodeStatus.Hidden;
                return state;
            }

            string key = RunKeyOf(node.Run);
            bool completed = completedRunKeys != null && completedRunKeys.Contains(key);
            bool unlocked = IsUnlocked(node, completedRunKeys, ownedHeroKeys);
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
                    state.MissingHeroes = GetMissingHeroNames(node, ownedHeroKeys);
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
            string activeRunKey,
            ICollection<string> ownedHeroKeys = null)
        {
            foreach (var state in GetStates(campaign, completedRunKeys, activeRunKey, ownedHeroKeys))
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
            string activeRunKey,
            ICollection<string> ownedHeroKeys = null)
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
                states.Add(GetState(node, completedRunKeys, activeRunKey, ownedHeroKeys));
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

        // --- Hero gates ------------------------------------------------------------------------
        //
        // A hero is the only *key-shaped* gate the campaign has: every other requirement is more of
        // something the player can go and get, so it can only ever delay a branch. See
        // NEXT_STEPS.md section 5b.

        /// <summary>
        /// Passed as the owned set by callers doing *authoring* analysis rather than resolving a
        /// save: they have no roster, and a hero gate fails shut without one, so a gated node would
        /// read as unreachable content. Reference equality, so no real save can collide with it.
        /// </summary>
        public static readonly ICollection<string> IgnoreHeroGate = new List<string>();

        /// <summary>
        /// The save key for a hero. Mirrors <c>HeroSO.SaveKey</c>, which is what
        /// <c>PartySaveData.OwnedHeroKeys</c> stores.
        /// </summary>
        public static string HeroKeyOf(HeroSO hero)
        {
            return hero != null ? hero.SaveKey : string.Empty;
        }

        /// <summary>
        /// Whether the player owns every hero this node asks for. Always all-of - a node needing two
        /// heroes needs both, whatever <c>UnlockMode</c> says about its runs. A null owned set counts
        /// as owning nobody, so an ungated node is unaffected and a gated one stays shut.
        /// </summary>
        public static bool HeroGateSatisfied(CampaignNodeEntry node, ICollection<string> ownedHeroKeys)
        {
            if (node?.RequiresHeroes == null || node.RequiresHeroes.Count == 0)
            {
                return true;
            }

            if (ReferenceEquals(ownedHeroKeys, IgnoreHeroGate))
            {
                return true;
            }

            foreach (var hero in node.RequiresHeroes)
            {
                string key = HeroKeyOf(hero);
                if (string.IsNullOrEmpty(key))
                {
                    // An empty row is an authoring slip, not a gate. Ignoring it keeps a
                    // half-authored asset playable; GetNodesWithBrokenHeroGates reports it.
                    continue;
                }
                if (ownedHeroKeys == null || !ownedHeroKeys.Contains(key))
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>Display names of the heroes this save is still missing, for the locked-node line.</summary>
        public static List<string> GetMissingHeroNames(
            CampaignNodeEntry node,
            ICollection<string> ownedHeroKeys)
        {
            var missing = new List<string>();
            if (node?.RequiresHeroes == null)
            {
                return missing;
            }

            foreach (var hero in node.RequiresHeroes)
            {
                string key = HeroKeyOf(hero);
                if (string.IsNullOrEmpty(key))
                {
                    continue;
                }
                if (ownedHeroKeys == null || !ownedHeroKeys.Contains(key))
                {
                    missing.Add(!string.IsNullOrEmpty(hero.DisplayName) ? hero.DisplayName : key);
                }
            }
            return missing;
        }

        /// <summary>
        /// Every hero key a save is guaranteed to hold by the time it reaches
        /// <paramref name="node"/>: the roster's starting lineup, plus the captive on every level of
        /// every run the player *must* clear to get here.
        ///
        /// <para>Deliberately walks only the runs on the required path, not every run in the
        /// campaign. A hero found down an optional branch is not a hero the player is guaranteed to
        /// have, and gating on one is how a save gets stranded - which is the whole reason this
        /// exists. An <c>Any</c>-mode node contributes nothing for the same reason: the player may
        /// have taken either branch, so neither is guaranteed.</para>
        ///
        /// <para>A node inside a prerequisite cycle simply resolves to the starting lineup;
        /// <see cref="GetUnreachableNodes"/> is what reports the cycle itself.</para>
        /// </summary>
        public static HashSet<string> GetGuaranteedHeroKeys(
            CampaignSO campaign,
            CampaignNodeEntry node,
            PartyRosterSO roster)
        {
            var keys = new HashSet<string>();
            if (roster != null)
            {
                foreach (var hero in roster.StartingLineup())
                {
                    string key = HeroKeyOf(hero);
                    if (!string.IsNullOrEmpty(key))
                    {
                        keys.Add(key);
                    }
                }
            }

            if (campaign == null || node == null)
            {
                return keys;
            }

            var seen = new HashSet<CampaignNodeEntry> { node };
            var pending = new Queue<CampaignNodeEntry>();
            pending.Enqueue(node);

            while (pending.Count > 0)
            {
                var current = pending.Dequeue();
                if (current.Requires == null || current.UnlockMode != CampaignUnlockMode.All)
                {
                    continue;
                }

                foreach (var prerequisite in current.Requires)
                {
                    if (prerequisite == null)
                    {
                        continue;
                    }

                    AddRescuedHeroKeys(prerequisite, keys);

                    var prerequisiteNode = FindNode(campaign, prerequisite);
                    if (prerequisiteNode != null && seen.Add(prerequisiteNode))
                    {
                        pending.Enqueue(prerequisiteNode);
                    }
                }
            }

            return keys;
        }

        /// <summary>Every hero a run can hand over, one captive per level.</summary>
        public static void AddRescuedHeroKeys(RunDefinitionSO run, HashSet<string> into)
        {
            if (run?.Levels == null || into == null)
            {
                return;
            }
            foreach (var level in run.Levels)
            {
                string key = HeroKeyOf(level?.RescueHero);
                if (!string.IsNullOrEmpty(key))
                {
                    into.Add(key);
                }
            }
        }

        /// <summary>The node holding <paramref name="run"/>, or null when it is not on the map.</summary>
        public static CampaignNodeEntry FindNode(CampaignSO campaign, RunDefinitionSO run)
        {
            if (campaign?.Nodes == null || run == null)
            {
                return null;
            }
            string key = RunKeyOf(run);
            foreach (var candidate in campaign.Nodes)
            {
                if (candidate?.Run != null && RunKeyOf(candidate.Run) == key)
                {
                    return candidate;
                }
            }
            return null;
        }

        /// <summary>
        /// Indices of nodes whose hero gate can never open: an empty row, or a hero the player is not
        /// guaranteed to hold by the time they arrive. The second is the one that matters - a gate on
        /// a hero found down an optional branch, or on a captive the player was never forced past, is
        /// a run some perfectly reasonable save can never start.
        /// </summary>
        public static List<int> GetNodesWithBrokenHeroGates(CampaignSO campaign, PartyRosterSO roster)
        {
            var broken = new List<int>();
            if (campaign?.Nodes == null)
            {
                return broken;
            }

            for (int i = 0; i < campaign.Nodes.Count; i++)
            {
                var node = campaign.Nodes[i];
                if (node?.RequiresHeroes == null || node.RequiresHeroes.Count == 0)
                {
                    continue;
                }

                var guaranteed = GetGuaranteedHeroKeys(campaign, node, roster);
                foreach (var hero in node.RequiresHeroes)
                {
                    string key = HeroKeyOf(hero);
                    if (string.IsNullOrEmpty(key) || !guaranteed.Contains(key))
                    {
                        broken.Add(i);
                        break;
                    }
                }
            }
            return broken;
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
                    // Run gates only. This walk has no roster, and a hero gate fails shut without
                    // one - so applying it here would report every hero-gated node as a
                    // prerequisite cycle. GetNodesWithBrokenHeroGates is the hero half of this
                    // check, and it is the one that can actually answer the question.
                    if (IsUnlocked(node, completed, IgnoreHeroGate))
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
