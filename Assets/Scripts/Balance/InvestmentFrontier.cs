using System.Collections.Generic;
using Assets.Scripts.Dungeon;
using Assets.Scripts.Heroes;
using Assets.Scripts.Items;
using UnityEngine;

namespace Assets.Scripts.Balance
{
    /// <summary>
    /// One investment mix, measured: a party width and an XP budget per hero, with what that mix
    /// does to a floor.
    ///
    /// <para><see cref="Cost"/> is the two axes reduced to one number so tiers can be compared, in
    /// the exchange rate the design is stated in — <c>BalanceRulesSO.HeroXpEquivalent</c> XP per
    /// hero bought past <see cref="PartySlots.BaseCap"/>. Heroes below the base cap are free (a
    /// fresh save can already field two), so a narrow party is never *cheaper* than the full base
    /// party — only different, which is why the frontier keeps both axes rather than only the
    /// cost.</para>
    /// </summary>
    public class InvestmentPoint
    {
        public int PartySize;
        public int XpPerHero;
        public int Cost;

        public float WipeRate;

        /// <summary>Over surviving trials only, matching <c>FloorOutcome</c>.</summary>
        public float EndHealthFraction;

        public float HeroDeaths;

        public string Mix => PartySize == 1
            ? $"1 hero, {XpPerHero} XP"
            : $"{PartySize} heroes, {XpPerHero} XP";

        /// <summary>True when <paramref name="other"/> costs no more on either axis, and less on one.</summary>
        public bool IsDominatedBy(InvestmentPoint other)
        {
            if (other == null || other == this)
            {
                return false;
            }
            bool noWorse = other.PartySize <= PartySize && other.XpPerHero <= XpPerHero;
            bool strictlyBetter = other.PartySize < PartySize || other.XpPerHero < XpPerHero;
            return noWorse && strictlyBetter;
        }
    }

    /// <summary>
    /// What one floor asks of the player, expressed as a <b>frontier</b> rather than a verdict
    /// against a single reference party.
    ///
    /// <para>A floor's difficulty cannot be one number and it cannot be one party either: the two
    /// investment axes the game offers — party width and sphere-grid XP — trade against each other,
    /// so "can this be beaten" only has an answer of the form <i>by whom, having paid what</i>. The
    /// frontier is the set of <i>minimal</i> mixes that bring the floor inside the wipe band; every
    /// useful statement about a tier is a statement about this set's shape and position:</para>
    /// <list type="bullet">
    /// <item><b>Is there a choice at all?</b> Two or more affordable mixes means the player picks how
    /// to pay. One means a checklist.</item>
    /// <item><b>Does it move outward with depth?</b> That is "depth means danger", stated in the only
    /// currency that survives a content edit.</item>
    /// </list>
    ///
    /// <para>Read the numbers as <b>optimistic</b>: <c>SphereGridOps.GreedySpend</c> approximates an
    /// optimal build, so a player taking a flavourful route through a wide grid is weaker at the
    /// same XP. See <c>docs/BALANCING.md</c> §5j.</para>
    /// </summary>
    public class FloorFrontier
    {
        public string Label = "";
        public Object Asset;
        public RunDefinitionSO Run;
        public int LevelIndex = -1;

        /// <summary>Campaign depth from <c>CampaignOps.ComputeTiers</c>; -1 when no campaign is authored.</summary>
        public int Tier = -1;

        public int Rooms;

        /// <summary>Every mix actually simulated. Sparse by design — the sweep prunes dominated mixes.</summary>
        public List<InvestmentPoint> Measured = new List<InvestmentPoint>();

        /// <summary>The minimal mixes that bring the floor inside the wipe band.</summary>
        public List<InvestmentPoint> Frontier = new List<InvestmentPoint>();

        /// <summary>The minimal mixes past which the floor stops being a threat at all.</summary>
        public List<InvestmentPoint> SafeFrontier = new List<InvestmentPoint>();

        /// <summary>The tier budget this floor is measured against; -1 when none is authored.</summary>
        public int Budget = -1;

        /// <summary>Mixes within <c>EquivalentInvestmentTolerance</c> of the cheapest — the real choice.</summary>
        public List<InvestmentPoint> AffordableChoices = new List<InvestmentPoint>();

        /// <summary>What the floor asks: the cheapest mix on the frontier. <see cref="int.MaxValue"/> when nothing clears it.</summary>
        public int AskedInvestment
        {
            get
            {
                int best = int.MaxValue;
                foreach (var point in Frontier)
                {
                    if (point.Cost < best)
                    {
                        best = point.Cost;
                    }
                }
                return best;
            }
        }

        /// <summary>Where the floor stops threatening anyone. <see cref="int.MaxValue"/> when it never does.</summary>
        public int SafeInvestment
        {
            get
            {
                int best = int.MaxValue;
                foreach (var point in SafeFrontier)
                {
                    if (point.Cost < best)
                    {
                        best = point.Cost;
                    }
                }
                return best;
            }
        }

        /// <summary>No mix on the swept surface brings this floor inside the band.</summary>
        public bool Unclearable => Frontier.Count == 0;

        /// <summary>At least two affordable mixes, i.e. the player picks how to pay rather than being told.</summary>
        public bool OffersChoice => AffordableChoices.Count >= 2;

        public string FrontierText
        {
            get
            {
                if (Frontier.Count == 0)
                {
                    return "(nothing on the sweep clears it)";
                }
                var parts = new List<string>();
                foreach (var point in Frontier)
                {
                    parts.Add($"({point.Mix})");
                }
                return string.Join(" · ", parts);
            }
        }
    }

    /// <summary>Everything one frontier sweep needs. Pure inputs — nothing here reads an asset database.</summary>
    public class FrontierSweepSettings
    {
        /// <summary>The floor's roster in party order; a width of <c>k</c> fields the first <c>k</c>.</summary>
        public IList<HeroSO> Roster;

        /// <summary>The floor's rooms in play order, boss last. Party-independent, so one set serves every mix.</summary>
        public IList<IList<SimUnit>> Rooms;

        public IList<int> Widths;
        public IList<int> XpSteps;

        public int HeroXpEquivalent = 250;
        public int BaseWidth = PartySlots.BaseCap;

        /// <summary>At or below this wipe rate the floor counts as cleared by the mix.</summary>
        public float ClearWipeRate = 0.35f;

        /// <summary>Below this wipe rate the floor is no longer a threat to the mix.</summary>
        public float SafeWipeRate = 0.05f;

        /// <summary>Mixes costing within this much of the cheapest count as a real alternative to it.</summary>
        public int EquivalentInvestmentTolerance = 150;

        public ItemSO PotionItem;
        public int PotionCount = -1;

        /// <summary>The floor sim template — trials, seed, refuges, charges. Policy is forced to Adaptive.</summary>
        public EncounterSimulator.FloorSimSettings Sim;

        /// <summary>
        /// Called on each freshly built party before it fights. The analyzer passes its Draw-loadout
        /// assignment here, so a swept party holds the same magic a measured one does.
        /// </summary>
        public System.Action<PartyBaseline> PrepareParty;
    }

    /// <summary>
    /// Sweeps party width against sphere-grid XP and reports where a floor's difficulty frontier
    /// actually sits.
    ///
    /// <para><b>Why a sweep and not a party.</b> The first attempt at this measured the XP axis at a
    /// three-hero party, concluded "XP does not matter", and was wrong: three heroes is the corner of
    /// the surface where nothing matters, so nothing showed. Both axes bite, and they substitute for
    /// each other at roughly one hero per <c>HeroXpEquivalent</c> XP. Only the surface shows that;
    /// any single line through it can be made to say anything. (<c>docs/BALANCING.md</c> §5i → §5j.)</para>
    ///
    /// <para><b>The sweep is pruned, not exhaustive.</b> Widening a party or spending more XP only
    /// ever helps, so once a width clears at some XP every wider mix at that XP or above is dominated
    /// and is never simulated. That is what keeps a frontier to roughly a dozen floor batches instead
    /// of the whole grid.</para>
    /// </summary>
    public static class InvestmentFrontier
    {
        /// <summary>The investment a mix costs, in XP-per-hero units. Heroes inside the base cap are free.</summary>
        public static int CostOf(int partySize, int xpPerHero, int heroXpEquivalent, int baseWidth)
        {
            int boughtHeroes = Mathf.Max(0, partySize - baseWidth);
            return boughtHeroes * Mathf.Max(0, heroXpEquivalent) + Mathf.Max(0, xpPerHero);
        }

        public static FloorFrontier Measure(FrontierSweepSettings settings)
        {
            var frontier = new FloorFrontier();
            if (settings == null || settings.Roster == null || settings.Roster.Count == 0
                || settings.Rooms == null || settings.Rooms.Count == 0 || settings.Sim == null)
            {
                return frontier;
            }

            frontier.Rooms = settings.Rooms.Count;

            var widths = Ordered(settings.Widths);
            var xpSteps = Ordered(settings.XpSteps);
            if (widths.Count == 0 || xpSteps.Count == 0)
            {
                return frontier;
            }

            // One cache across both passes: the "stops being a threat" frontier re-reads most of the
            // mixes the "can clear it" frontier already paid for.
            var cache = new Dictionary<long, InvestmentPoint>();

            frontier.Frontier = MinimalMixes(settings, widths, xpSteps, cache,
                point => point.WipeRate <= settings.ClearWipeRate);
            frontier.SafeFrontier = MinimalMixes(settings, widths, xpSteps, cache,
                point => point.WipeRate < settings.SafeWipeRate);

            foreach (var point in cache.Values)
            {
                frontier.Measured.Add(point);
            }
            frontier.Measured.Sort((a, b) =>
            {
                int byWidth = a.PartySize.CompareTo(b.PartySize);
                return byWidth != 0 ? byWidth : a.XpPerHero.CompareTo(b.XpPerHero);
            });

            int cheapest = frontier.AskedInvestment;
            if (cheapest != int.MaxValue)
            {
                foreach (var point in frontier.Frontier)
                {
                    if (point.Cost <= cheapest + Mathf.Max(0, settings.EquivalentInvestmentTolerance))
                    {
                        frontier.AffordableChoices.Add(point);
                    }
                }
            }

            return frontier;
        }

        /// <summary>
        /// The minimal mixes satisfying <paramref name="accept"/>, widths ascending.
        ///
        /// <para>For each width, the cheapest XP that satisfies the predicate. A width is skipped
        /// entirely once a narrower one already succeeds at that XP or lower, because the narrower
        /// mix dominates it on both axes — which is both the pruning and the definition of the
        /// frontier.</para>
        /// </summary>
        private static List<InvestmentPoint> MinimalMixes(
            FrontierSweepSettings settings,
            List<int> widths,
            List<int> xpSteps,
            Dictionary<long, InvestmentPoint> cache,
            System.Func<InvestmentPoint, bool> accept)
        {
            var minimal = new List<InvestmentPoint>();
            int bestXpSoFar = int.MaxValue;

            foreach (int width in widths)
            {
                if (width <= 0 || width > settings.Roster.Count)
                {
                    continue;
                }

                foreach (int xp in xpSteps)
                {
                    if (xp >= bestXpSoFar)
                    {
                        // A narrower party already manages this at no more XP, so everything from
                        // here up is dominated. Nothing left to learn at this width.
                        break;
                    }

                    var point = MeasureMix(settings, width, xp, cache);
                    if (accept(point))
                    {
                        minimal.Add(point);
                        bestXpSoFar = xp;
                        break;
                    }
                }
            }

            return minimal;
        }

        private static InvestmentPoint MeasureMix(
            FrontierSweepSettings settings, int width, int xp, Dictionary<long, InvestmentPoint> cache)
        {
            long key = ((long)width << 32) | (uint)xp;
            if (cache.TryGetValue(key, out var cached))
            {
                return cached;
            }

            var roster = new List<HeroSO>();
            for (int i = 0; i < settings.Roster.Count && roster.Count < width; i++)
            {
                if (settings.Roster[i] != null)
                {
                    roster.Add(settings.Roster[i]);
                }
            }

            var point = new InvestmentPoint
            {
                PartySize = width,
                XpPerHero = xp,
                Cost = CostOf(width, xp, settings.HeroXpEquivalent, settings.BaseWidth),
                // A mix that cannot be fielded is a total loss, not a free pass.
                WipeRate = 1f
            };

            if (roster.Count == width)
            {
                var party = PartyBaseline.Build(roster, xp, null, settings.PotionItem, settings.PotionCount);
                if (settings.PrepareParty != null)
                {
                    settings.PrepareParty(party);
                }

                var sim = CloneSettings(settings.Sim);
                sim.Policy = SimPolicy.Adaptive;
                sim.PotionCount = party.PotionCount;
                sim.PotionHealAmount = party.PotionHealAmount;

                var outcome = EncounterSimulator.RunFloor(party, settings.Rooms, sim);
                point.WipeRate = outcome.WipeRate;
                point.EndHealthFraction = outcome.AverageEndHealthFraction;
                point.HeroDeaths = outcome.AverageHeroDeaths;
            }

            cache[key] = point;
            return point;
        }

        private static EncounterSimulator.FloorSimSettings CloneSettings(
            EncounterSimulator.FloorSimSettings source)
        {
            return new EncounterSimulator.FloorSimSettings
            {
                Trials = source.Trials,
                Seed = source.Seed,
                MaxTurns = source.MaxTurns,
                Policy = source.Policy,
                PotionCount = source.PotionCount,
                PotionHealAmount = source.PotionHealAmount,
                Combos = source.Combos,
                HealThreshold = source.HealThreshold,
                RestRooms = source.RestRooms,
                RestHealFraction = source.RestHealFraction,
                StartsWithFullCharges = source.StartsWithFullCharges
            };
        }

        private static List<int> Ordered(IList<int> values)
        {
            var ordered = new List<int>();
            if (values == null)
            {
                return ordered;
            }
            foreach (int value in values)
            {
                if (value >= 0 && !ordered.Contains(value))
                {
                    ordered.Add(value);
                }
            }
            ordered.Sort();
            return ordered;
        }
    }
}
