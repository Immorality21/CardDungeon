using System.Collections.Generic;
using Assets.Scripts.Cards;
using Assets.Scripts.Combat;
using Assets.Scripts.Dungeon;
using Assets.Scripts.Enemies;
using Assets.Scripts.Heroes;
using Assets.Scripts.Items;
using UnityEngine;

namespace Assets.Scripts.Balance
{
    /// <summary>
    /// One place a magic can be learned: a <c>MagicKnown</c> node on a specific hero's sphere grid.
    ///
    /// <para><b>This replaced <c>DrawSource</c> on 2026-09-04.</b> A source used to be an enemy in a
    /// level of a run, and the question the map answered was "has the player walked past something
    /// carrying this yet". Magic is bought on the grid now, so a source is a <i>price</i> instead of
    /// a place, and the question is "can a campaign afford this". That is why the ordering key is XP
    /// rather than run sequence.</para>
    /// </summary>
    public class MagicSource
    {
        public HeroSO Hero;
        public string HeroName = "";
        public string HeroKey = "";
        public string NodeKey = "";

        /// <summary>Edges from the grid's start node — how far into a branch the spell sits.</summary>
        public int Depth;

        /// <summary>The node's own price.</summary>
        public int NodeCost;

        /// <summary>
        /// Total XP to <b>own</b> the node: the cheapest chain of activations from the start node to
        /// it, inclusive. This, not <see cref="NodeCost"/>, is what a player actually pays — nodes
        /// are priced by depth (<c>SphereGridOps.CostForDepth</c>), so a deep spell's real cost is
        /// dominated by the branch leading to it.
        /// </summary>
        public int PathCost;

        /// <summary>Charges the node grants — the run's whole allowance of the spell.</summary>
        public int Charges;

        /// <summary>Cheapest-first ordering key. The grid analogue of "earliest in play order".</summary>
        public long Order => PathCost;
    }

    /// <summary>Which heroes can learn a magic, and what the cheapest route to it costs.</summary>
    public class MagicAvailability
    {
        public MagicSO Magic;
        public string Key = "";
        public string DisplayName = "";
        public List<DamageType> DamageTypes = new List<DamageType>();
        public List<MagicTag> Tags = new List<MagicTag>();
        public List<MagicSource> Sources = new List<MagicSource>();

        /// <summary>False means <b>no hero can ever cast this</b> — with Draw gone, a magic on no
        /// grid is dead content, not merely late content.</summary>
        public bool IsReachable => Sources.Count > 0;

        /// <summary>The cheapest way in — which hero to invest in, and what it costs.</summary>
        public MagicSource FirstSource;

        /// <summary>True when only one hero's grid teaches it, so fielding that hero is a
        /// precondition rather than a preference.</summary>
        public bool SingleHeroOnly
        {
            get
            {
                if (Sources.Count == 0)
                {
                    return false;
                }
                for (int i = 1; i < Sources.Count; i++)
                {
                    if (Sources[i].HeroKey != Sources[0].HeroKey)
                    {
                        return false;
                    }
                }
                return true;
            }
        }
    }

    /// <summary>Where in the run order the modelled party first holds every piece of a combo.</summary>
    public class ProgressionPoint
    {
        public RunDefinitionSO Run;
        public string RunName = "";
        public int RunSequence;
        public int LevelIndex;
        public string LevelReference = "";

        /// <summary>Play-order key: run sequence first, then level index.</summary>
        public long Order => (long)RunSequence * 10000L + LevelIndex;
    }

    /// <summary>
    /// Whether a combo can ever fire, and when. A combo needs one required tag already on the target and
    /// another arriving with the incoming cast (see <see cref="ComboDetector"/>), so every required tag
    /// has to be carried by magic some hero can actually learn — a combo whose tags live only on
    /// unlearnable magic is dead content no stat tuning will reveal.
    /// </summary>
    public class ComboAvailability
    {
        public MagicComboSO Combo;
        public string Key = "";
        public string Name = "";
        public List<MagicTag> RequiredTags = new List<MagicTag>();

        /// <summary>Required tags no magic in the catalog carries at all.</summary>
        public List<MagicTag> TagsWithNoMagic = new List<MagicTag>();

        /// <summary>Required tags carried only by magic no sphere grid teaches.</summary>
        public List<MagicTag> TagsNotLearnable = new List<MagicTag>();

        /// <summary>Magic that satisfies the requirement, one entry per required tag.</summary>
        public List<string> EnablingMagic = new List<string>();

        public bool IsReachable => TagsWithNoMagic.Count == 0 && TagsNotLearnable.Count == 0;

        /// <summary>
        /// XP to buy every piece: the sum, over the required tags, of the cheapest route to a magic
        /// carrying that tag. Approximate on purpose — two tags on one branch share most of their
        /// path, so this is an upper bound rather than a quote.
        /// </summary>
        public int InvestmentToEnable;

        /// <summary>
        /// Where the combo actually becomes possible in play: the first level at which the modelled
        /// party holds a magic for every required tag. Null when no modelled party ever does, which
        /// is a stronger finding than "it costs a lot" — it means the campaign never pays for it.
        /// </summary>
        public ProgressionPoint UnlockedAt;
    }

    /// <summary>The elemental picture for one level: what it resists, and what the player can bring to it.</summary>
    public class LevelElementProfile
    {
        public int LevelIndex;
        public string Reference = "";

        /// <summary>Expected enemies in the level (fractional — spawn tables are rolls).</summary>
        public float EnemyWeight;

        public float ResistingWeight;
        public float WeakWeight;

        public Dictionary<DamageType, float> ResistWeightByType = new Dictionary<DamageType, float>();
        public Dictionary<DamageType, float> WeakWeightByType = new Dictionary<DamageType, float>();

        /// <summary>Magic the modelled party first knows by this level.</summary>
        public List<MagicSO> NewlyKnown = new List<MagicSO>();

        /// <summary>
        /// What the level's enemies attack *with*, weighted by expected count. The defensive mirror of
        /// the resistance columns: elemental resistance on the hero side is only worth anything against
        /// elements something actually deals.
        /// </summary>
        public Dictionary<DamageType, float> IncomingWeightByType = new Dictionary<DamageType, float>();

        /// <summary>
        /// Elements this level deals that nothing in the project can resist - no gear, no magic. Authored
        /// threat the player has no answer to.
        /// </summary>
        public List<DamageType> UndefendableIncoming = new List<DamageType>();

        /// <summary>Elements (excluding Normal) the modelled party can deal at this level.</summary>
        public List<DamageType> ElementsAvailable = new List<DamageType>();

        /// <summary>
        /// Whether any resistance or weakness in this level is in an element the player can actually
        /// bring yet. False means the level's resistances cannot change a single decision.
        /// </summary>
        public bool ElementChoiceMatters;

        public float ResistanceCoverage => EnemyWeight > 0f ? ResistingWeight / EnemyWeight : 0f;
        public float WeaknessCoverage => EnemyWeight > 0f ? WeakWeight / EnemyWeight : 0f;
        public bool HasCombat => EnemyWeight > 0f;
    }

    /// <summary>What one run contributes to the player's toolkit, in play order.</summary>
    public class RunProgression
    {
        public RunDefinitionSO Run;
        public string Name = "";
        public int Sequence;
        public List<LevelElementProfile> Levels = new List<LevelElementProfile>();

        /// <summary>Magic the modelled party first knows during this run — its "unlocks" list.</summary>
        public List<MagicSO> NewlyKnown = new List<MagicSO>();

        /// <summary>Combos that become possible for the first time during this run.</summary>
        public List<MagicComboSO> NewlyEnabledCombos = new List<MagicComboSO>();
    }

    /// <summary>
    /// Maps the player's magic toolkit against the run order: which spells the sphere grids teach and at
    /// what price, which of them the modelled party actually owns by each level, which combos that makes
    /// possible, and whether each level's resistances are in elements the player can bring yet.
    ///
    /// <para><b>Rebuilt on 2026-09-04.</b> This was a supply-chain model of the Draw tables: magic ×
    /// enemy × level × run, answering "is this spell placed on something the player will meet". Draw
    /// was removed and magic moved onto the sphere grid (<c>docs/plans/SPECIALIZATION.md</c> §9b), so
    /// the supply side is now <i>investment</i>: a spell exists for a player if some hero's grid
    /// teaches it and the campaign pays enough XP to reach the node. Two consequences worth knowing
    /// before reading a finding off this:</para>
    ///
    /// <list type="bullet">
    /// <item><description><b>Unreachable is worse than it used to be.</b> A magic on no grid cannot be
    /// obtained at all — there is no second route. Under Draw an "unreachable" magic was still
    /// authored content sitting one spawn-table edit away.</description></item>
    /// <item><description><b>Availability is now a function of the modelled party.</b> Which spells are
    /// in hand at level 5 depends on which heroes are fielded and how <c>GreedySpend</c> spent their
    /// XP — and <c>GreedySpend</c> is a breadth build by construction, so it will under-report a
    /// deliberately deep magic branch. Do not read a late unlock here as proof a branch is
    /// mispriced.</description></item>
    /// </list>
    /// </summary>
    public class ProgressionMap
    {
        public List<MagicAvailability> Magic = new List<MagicAvailability>();
        public List<ComboAvailability> Combos = new List<ComboAvailability>();
        public List<RunProgression> Runs = new List<RunProgression>();

        public int CatalogMagicCount;
        public int ReachableMagicCount;
        public int ReachableComboCount;

        /// <summary>
        /// Damage types the hero side can resist at all, from any source: gear
        /// (<see cref="ItemSO.Resistances"/>) or magic granting a resistance buff. Used to tell authored
        /// elemental threat apart from threat the player has no answer to.
        /// </summary>
        public List<DamageType> DefendableTypes = new List<DamageType>();

        public float MagicCoverage => CatalogMagicCount > 0
            ? (float)ReachableMagicCount / CatalogMagicCount
            : 0f;

        /// <summary>
        /// True when no run has an explicit <c>SequenceIndex</c>, so the ordering fell back to asset
        /// names. Worth surfacing rather than presenting a guessed order as fact.
        /// </summary>
        public bool RunOrderIsImplicit;

        public static ProgressionMap Build(
            IList<RunCurve> runCurves,
            IList<MagicSO> catalog,
            IList<MagicComboSO> combos,
            IList<HeroSO> heroes = null,
            IList<ItemSO> items = null)
        {
            var map = new ProgressionMap();
            map.CatalogMagicCount = catalog != null ? catalog.Count : 0;
            map.DefendableTypes = CollectDefendableTypes(catalog, items);

            var byKey = new Dictionary<string, MagicAvailability>();
            if (catalog != null)
            {
                foreach (var magic in catalog)
                {
                    if (magic == null)
                    {
                        continue;
                    }

                    var availability = new MagicAvailability
                    {
                        Magic = magic,
                        Key = string.IsNullOrEmpty(magic.Key) ? magic.name : magic.Key,
                        DisplayName = string.IsNullOrEmpty(magic.DisplayName) ? magic.name : magic.DisplayName,
                        Tags = magic.Tags != null ? new List<MagicTag>(magic.Tags) : new List<MagicTag>()
                    };

                    if (magic.Effects != null)
                    {
                        foreach (var effect in magic.Effects)
                        {
                            if (effect != null
                                && effect.EffectType == SpellEffectType.Damage
                                && !availability.DamageTypes.Contains(effect.DamageType))
                            {
                                availability.DamageTypes.Add(effect.DamageType);
                            }
                        }
                    }

                    if (!byKey.ContainsKey(availability.Key))
                    {
                        byKey[availability.Key] = availability;
                    }
                    map.Magic.Add(availability);
                }
            }

            CollectGridSources(map, byKey, heroes);

            var ordered = OrderRuns(runCurves, map);
            BuildRunProgressions(map, ordered, byKey);
            BuildComboAvailability(map, combos, ordered);

            foreach (var availability in map.Magic)
            {
                if (availability.IsReachable)
                {
                    map.ReachableMagicCount++;
                }
            }
            foreach (var combo in map.Combos)
            {
                if (combo.IsReachable)
                {
                    map.ReachableComboCount++;
                }
            }

            AssignComboUnlockRuns(map);
            return map;
        }

        // ============================================================
        //  THE GRIDS: WHO TEACHES WHAT, AND FOR HOW MUCH
        // ============================================================

        /// <summary>
        /// Every <c>MagicKnown</c> node on every hero's grid, recorded against the magic it teaches.
        /// This is the whole supply side now.
        /// </summary>
        private static void CollectGridSources(
            ProgressionMap map, Dictionary<string, MagicAvailability> byKey, IList<HeroSO> heroes)
        {
            if (heroes == null)
            {
                return;
            }

            foreach (var hero in heroes)
            {
                var grid = hero != null ? hero.SphereGrid : null;
                if (grid == null || grid.Nodes == null)
                {
                    continue;
                }

                var depths = SphereGridOps.DepthsFrom(grid);
                var pathCosts = CheapestPathCosts(grid);

                foreach (var node in grid.Nodes)
                {
                    if (node == null
                        || node.Kind != SphereNodeKind.MagicKnown
                        || string.IsNullOrEmpty(node.GrantedMagicKey))
                    {
                        continue;
                    }

                    MagicAvailability availability;
                    if (!byKey.TryGetValue(node.GrantedMagicKey, out availability))
                    {
                        // A node naming a magic the catalog does not have. BalanceAnalyzer reports
                        // that separately; here it simply is not a source of anything.
                        continue;
                    }

                    int depth;
                    if (!depths.TryGetValue(node.Key, out depth))
                    {
                        depth = 0;
                    }

                    int pathCost;
                    if (!pathCosts.TryGetValue(node.Key, out pathCost))
                    {
                        // Unreachable from the start node — no chain of activations reaches it, so
                        // it teaches nothing however it is priced.
                        continue;
                    }

                    var source = new MagicSource
                    {
                        Hero = hero,
                        HeroName = hero.DisplayName,
                        HeroKey = hero.SaveKey,
                        NodeKey = node.Key,
                        Depth = depth,
                        NodeCost = node.XpCost,
                        PathCost = pathCost,
                        Charges = Mathf.Max(1, node.GrantedCharges)
                    };

                    availability.Sources.Add(source);
                    if (availability.FirstSource == null || source.Order < availability.FirstSource.Order)
                    {
                        availability.FirstSource = source;
                    }
                }
            }
        }

        /// <summary>
        /// Cheapest total XP to own each node: a Dijkstra from the start node over undirected edges,
        /// where a node's weight is its own <c>XpCost</c>.
        ///
        /// <para>Not the same as summing costs along the shortest <i>hop</i> path. Depth pricing is
        /// superlinear but node costs are authored per node, so a longer detour through cheap nodes
        /// can genuinely be the cheaper way in — and pricing a spell at the hop-shortest route would
        /// quote a number no player would pay.</para>
        /// </summary>
        private static Dictionary<string, int> CheapestPathCosts(SphereGridSO grid)
        {
            var costs = new Dictionary<string, int>();
            string start = SphereGridOps.StartKey(grid);
            if (string.IsNullOrEmpty(start))
            {
                return costs;
            }

            var adjacency = SphereGridOps.BuildAdjacency(grid);
            var startNode = SphereGridOps.FindNode(grid, start);
            costs[start] = startNode != null ? startNode.XpCost : 0;

            var settled = new List<string>();
            while (true)
            {
                string pick = null;
                foreach (var pair in costs)
                {
                    if (settled.Contains(pair.Key))
                    {
                        continue;
                    }
                    if (pick == null || pair.Value < costs[pick])
                    {
                        pick = pair.Key;
                    }
                }

                if (pick == null)
                {
                    return costs;
                }
                settled.Add(pick);

                List<string> neighbors;
                if (!adjacency.TryGetValue(pick, out neighbors))
                {
                    continue;
                }

                foreach (var neighbor in neighbors)
                {
                    var node = SphereGridOps.FindNode(grid, neighbor);
                    if (node == null)
                    {
                        continue;
                    }

                    int candidate = costs[pick] + node.XpCost;
                    int existing;
                    if (!costs.TryGetValue(neighbor, out existing) || candidate < existing)
                    {
                        costs[neighbor] = candidate;
                    }
                }
            }
        }

        /// <summary>
        /// Every damage type the hero side has some way of resisting. Gear resistance is immediate; magic
        /// resistance buffs are counted too, but note <c>ResistanceBuffHandler.Apply</c> is still a no-op
        /// (see docs/ELEMENTAL_PLAN.md), so those are potential rather than live.
        /// </summary>
        private static List<DamageType> CollectDefendableTypes(IList<MagicSO> catalog, IList<ItemSO> items)
        {
            var types = new List<DamageType>();

            if (items != null)
            {
                foreach (var item in items)
                {
                    if (item == null || item.Resistances == null)
                    {
                        continue;
                    }
                    foreach (var resistance in item.Resistances)
                    {
                        if (resistance != null && resistance.Percent > 0f && !types.Contains(resistance.DamageType))
                        {
                            types.Add(resistance.DamageType);
                        }
                    }
                }
            }

            if (catalog != null)
            {
                foreach (var magic in catalog)
                {
                    if (magic == null || magic.Effects == null)
                    {
                        continue;
                    }
                    foreach (var effect in magic.Effects)
                    {
                        if (effect == null || effect.EffectType != SpellEffectType.Buff)
                        {
                            continue;
                        }
                        DamageType type;
                        if (TryMapResistanceBuff(effect.BuffType, out type) && !types.Contains(type))
                        {
                            types.Add(type);
                        }
                    }
                }
            }

            return types;
        }

        /// <summary>Maps the resistance <see cref="BuffType"/>s onto their damage types.</summary>
        private static bool TryMapResistanceBuff(BuffType buffType, out DamageType type)
        {
            switch (buffType)
            {
                case BuffType.FireResistance: type = DamageType.Fire; return true;
                case BuffType.IceResistance: type = DamageType.Ice; return true;
                case BuffType.LightningResistance: type = DamageType.Lightning; return true;
                case BuffType.HolyResistance: type = DamageType.Holy; return true;
                case BuffType.ShadowResistance: type = DamageType.Shadow; return true;
                default: type = DamageType.Normal; return false;
            }
        }

        private static List<RunCurve> OrderRuns(IList<RunCurve> runCurves, ProgressionMap map)
        {
            var ordered = new List<RunCurve>();
            if (runCurves == null)
            {
                return ordered;
            }

            bool anySequence = false;
            foreach (var curve in runCurves)
            {
                if (curve == null || curve.Run == null)
                {
                    continue;
                }
                if (curve.Run.SequenceIndex != 0)
                {
                    anySequence = true;
                }
                ordered.Add(curve);
            }

            map.RunOrderIsImplicit = !anySequence && ordered.Count > 1;

            ordered.Sort((a, b) =>
            {
                int bySequence = a.Run.SequenceIndex.CompareTo(b.Run.SequenceIndex);
                if (bySequence != 0)
                {
                    return bySequence;
                }
                return string.Compare(a.Name, b.Name, System.StringComparison.Ordinal);
            });

            return ordered;
        }

        private static void BuildRunProgressions(
            ProgressionMap map,
            List<RunCurve> ordered,
            Dictionary<string, MagicAvailability> byKey)
        {
            // Elements accumulate across the whole play order: a spell bought on the grid before run 1
            // is still known in run 5, and grid nodes are never un-bought.
            var seenMagic = new List<string>();
            var elements = new List<DamageType>();

            foreach (var curve in ordered)
            {
                var runProgression = new RunProgression
                {
                    Run = curve.Run,
                    Name = curve.Name,
                    Sequence = curve.Run.SequenceIndex
                };

                foreach (var level in curve.Levels)
                {
                    var profile = new LevelElementProfile
                    {
                        LevelIndex = level.Index,
                        Reference = level.Reference
                    };

                    foreach (var kvp in CollectEnemies(level))
                    {
                        var enemy = kvp.Key;
                        float weight = kvp.Value.Weight;
                        profile.EnemyWeight += weight;

                        AccumulateResistances(profile, enemy, weight);

                        if (enemy.AttackDamageType != DamageType.Normal)
                        {
                            Add(profile.IncomingWeightByType, enemy.AttackDamageType, weight);
                        }
                    }

                    RecordKnownMagic(byKey, level, seenMagic, profile, runProgression, elements);

                    profile.ElementsAvailable = new List<DamageType>(elements);
                    profile.ElementChoiceMatters = ChoiceMatters(profile, elements);

                    foreach (var incoming in profile.IncomingWeightByType)
                    {
                        if (!map.DefendableTypes.Contains(incoming.Key))
                        {
                            profile.UndefendableIncoming.Add(incoming.Key);
                        }
                    }
                    runProgression.Levels.Add(profile);
                }

                map.Runs.Add(runProgression);
            }
        }

        /// <summary>
        /// What the modelled party actually knows by this level — the activated <c>MagicKnown</c> nodes
        /// of the heroes the level curve fields, at the XP the curve says they have spent.
        ///
        /// <para>This is where the model became honest about a thing Draw let it skip. A drawable magic
        /// was available to anyone who walked into the room; a learned one is available only to the
        /// hero who paid for it, and only if that hero is fielded. So a level's element coverage now
        /// depends on party composition, which is exactly the coupling <c>docs/BALANCING.md</c> §5b
        /// wanted the model to see.</para>
        /// </summary>
        private static void RecordKnownMagic(
            Dictionary<string, MagicAvailability> byKey,
            LevelCurve level,
            List<string> seenMagic,
            LevelElementProfile profile,
            RunProgression runProgression,
            List<DamageType> elements)
        {
            if (level.Party == null || level.Party.Heroes == null)
            {
                return;
            }

            foreach (var hero in level.Party.Heroes)
            {
                var grid = hero.Definition != null ? hero.Definition.SphereGrid : null;
                if (grid == null)
                {
                    continue;
                }

                foreach (var known in SphereGridOps.KnownMagicForNodes(grid, hero.ActivatedNodes))
                {
                    MagicAvailability availability;
                    if (!byKey.TryGetValue(known.Key, out availability) || seenMagic.Contains(known.Key))
                    {
                        continue;
                    }

                    seenMagic.Add(known.Key);
                    profile.NewlyKnown.Add(availability.Magic);
                    runProgression.NewlyKnown.Add(availability.Magic);

                    foreach (var type in availability.DamageTypes)
                    {
                        if (type != DamageType.Normal && !elements.Contains(type))
                        {
                            elements.Add(type);
                        }
                    }
                }
            }
        }

        private class EnemyPresence
        {
            public float Weight;
            public bool BossOnly = true;
        }

        /// <summary>
        /// Every enemy expected in a level, with how many of them and whether they only ever appear as
        /// the level's boss (the boss room is the one with no <c>RoomSO</c> behind it).
        /// </summary>
        private static Dictionary<EnemySO, EnemyPresence> CollectEnemies(LevelCurve level)
        {
            var byEnemy = new Dictionary<EnemySO, EnemyPresence>();

            foreach (var room in level.Rooms)
            {
                bool isBossRoom = room.Room == null;
                foreach (var member in room.Expected.Members)
                {
                    if (member.Definition == null)
                    {
                        continue;
                    }

                    if (!byEnemy.TryGetValue(member.Definition, out var presence))
                    {
                        presence = new EnemyPresence();
                        byEnemy[member.Definition] = presence;
                    }

                    presence.Weight += room.Occurrences * member.Weight;
                    if (!isBossRoom)
                    {
                        presence.BossOnly = false;
                    }
                }
            }

            return byEnemy;
        }

        private static void AccumulateResistances(LevelElementProfile profile, EnemySO enemy, float weight)
        {
            if (enemy.Resistances == null)
            {
                return;
            }

            bool resists = false;
            bool weak = false;

            foreach (var resistance in enemy.Resistances)
            {
                if (resistance == null || Mathf.Approximately(resistance.Percent, 0f))
                {
                    continue;
                }

                if (resistance.Percent > 0f)
                {
                    resists = true;
                    Add(profile.ResistWeightByType, resistance.DamageType, weight);
                }
                else
                {
                    weak = true;
                    Add(profile.WeakWeightByType, resistance.DamageType, weight);
                }
            }

            if (resists)
            {
                profile.ResistingWeight += weight;
            }
            if (weak)
            {
                profile.WeakWeight += weight;
            }
        }

        private static void Add(Dictionary<DamageType, float> target, DamageType type, float weight)
        {
            if (!target.ContainsKey(type))
            {
                target[type] = 0f;
            }
            target[type] += weight;
        }

        /// <summary>
        /// A level's resistances only create a decision if at least one of them is in an element the
        /// player can already deal. Resistance in an element they cannot yet obtain is dead weight.
        /// </summary>
        private static bool ChoiceMatters(LevelElementProfile profile, List<DamageType> elements)
        {
            foreach (var kvp in profile.ResistWeightByType)
            {
                if (kvp.Key != DamageType.Normal && elements.Contains(kvp.Key))
                {
                    return true;
                }
            }
            foreach (var kvp in profile.WeakWeightByType)
            {
                if (kvp.Key != DamageType.Normal && elements.Contains(kvp.Key))
                {
                    return true;
                }
            }
            return false;
        }

        private static void BuildComboAvailability(
            ProgressionMap map,
            IList<MagicComboSO> combos,
            List<RunCurve> ordered)
        {
            if (combos == null)
            {
                return;
            }

            var tagsByKey = new Dictionary<string, List<MagicTag>>();
            foreach (var availability in map.Magic)
            {
                if (!tagsByKey.ContainsKey(availability.Key))
                {
                    tagsByKey[availability.Key] = availability.Tags;
                }
            }

            foreach (var combo in combos)
            {
                if (combo == null)
                {
                    continue;
                }

                var availability = new ComboAvailability
                {
                    Combo = combo,
                    Key = string.IsNullOrEmpty(combo.Key) ? combo.name : combo.Key,
                    Name = string.IsNullOrEmpty(combo.ComboName) ? combo.name : combo.ComboName,
                    RequiredTags = combo.RequiredTags != null
                        ? new List<MagicTag>(combo.RequiredTags)
                        : new List<MagicTag>()
                };

                bool blocked = false;

                foreach (var tag in availability.RequiredTags)
                {
                    MagicAvailability carrier = null;      // any catalog magic with the tag
                    MagicAvailability learnable = null;    // one that is also on a grid, cheapest first

                    foreach (var candidate in map.Magic)
                    {
                        if (!candidate.Tags.Contains(tag))
                        {
                            continue;
                        }

                        carrier = carrier ?? candidate;
                        if (!candidate.IsReachable)
                        {
                            continue;
                        }
                        if (learnable == null
                            || candidate.FirstSource.Order < learnable.FirstSource.Order)
                        {
                            learnable = candidate;
                        }
                    }

                    if (carrier == null)
                    {
                        availability.TagsWithNoMagic.Add(tag);
                        blocked = true;
                        continue;
                    }

                    if (learnable == null)
                    {
                        availability.TagsNotLearnable.Add(tag);
                        availability.EnablingMagic.Add($"{tag}: {carrier.DisplayName} (on no grid)");
                        blocked = true;
                        continue;
                    }

                    availability.EnablingMagic.Add(
                        $"{tag}: {learnable.DisplayName} ({learnable.FirstSource.HeroName}, {learnable.FirstSource.PathCost} xp)");
                    availability.InvestmentToEnable += learnable.FirstSource.PathCost;
                }

                if (!blocked)
                {
                    availability.UnlockedAt = FirstLevelHoldingEveryTag(
                        availability.RequiredTags, ordered, tagsByKey);
                }
                map.Combos.Add(availability);
            }
        }

        /// <summary>
        /// The first level at which the modelled party holds a magic for every one of
        /// <paramref name="tags"/> at once — a combo needs them together, not eventually.
        /// Null when no level in the campaign ever does.
        /// </summary>
        private static ProgressionPoint FirstLevelHoldingEveryTag(
            List<MagicTag> tags, List<RunCurve> ordered, Dictionary<string, List<MagicTag>> tagsByKey)
        {
            if (tags == null || tags.Count == 0 || ordered == null)
            {
                return null;
            }

            var held = new List<MagicTag>();

            foreach (var curve in ordered)
            {
                foreach (var level in curve.Levels)
                {
                    if (level.Party == null || level.Party.Heroes == null)
                    {
                        continue;
                    }

                    foreach (var hero in level.Party.Heroes)
                    {
                        var grid = hero.Definition != null ? hero.Definition.SphereGrid : null;
                        if (grid == null)
                        {
                            continue;
                        }

                        foreach (var known in SphereGridOps.KnownMagicForNodes(grid, hero.ActivatedNodes))
                        {
                            AddTagsOf(known.Key, held, tagsByKey);
                        }
                    }

                    bool all = true;
                    foreach (var tag in tags)
                    {
                        if (!held.Contains(tag))
                        {
                            all = false;
                            break;
                        }
                    }

                    if (all)
                    {
                        return new ProgressionPoint
                        {
                            Run = curve.Run,
                            RunName = curve.Name,
                            RunSequence = curve.Run != null ? curve.Run.SequenceIndex : 0,
                            LevelIndex = level.Index,
                            LevelReference = level.Reference
                        };
                    }
                }
            }

            return null;
        }

        /// <summary>Tags carried by the magic with this key, appended to <paramref name="into"/>.</summary>
        private static void AddTagsOf(
            string magicKey, List<MagicTag> into, Dictionary<string, List<MagicTag>> tagsByKey)
        {
            List<MagicTag> tags;
            if (tagsByKey == null || magicKey == null || !tagsByKey.TryGetValue(magicKey, out tags))
            {
                return;
            }
            foreach (var tag in tags)
            {
                if (!into.Contains(tag))
                {
                    into.Add(tag);
                }
            }
        }

        /// <summary>Files each reachable combo under the run where its last required piece appears.</summary>
        private static void AssignComboUnlockRuns(ProgressionMap map)
        {
            foreach (var combo in map.Combos)
            {
                if (!combo.IsReachable || combo.UnlockedAt == null)
                {
                    continue;
                }

                foreach (var run in map.Runs)
                {
                    if (run.Run == combo.UnlockedAt.Run)
                    {
                        run.NewlyEnabledCombos.Add(combo.Combo);
                        break;
                    }
                }
            }
        }
    }
}
