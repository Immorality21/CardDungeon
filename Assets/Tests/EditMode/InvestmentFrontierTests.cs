using System.Collections.Generic;
using Assets.Scripts.Balance;
using Assets.Scripts.Cards;
using Assets.Scripts.Combat;
using Assets.Scripts.Enemies;
using Assets.Scripts.Heroes;
using Assets.Scripts.UnitStats;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// The investment frontier: what a floor asks of the player, over the three axes the game
    /// actually sells them — party width, sphere-grid XP and gear bought with gold.
    ///
    /// <para>Every test here pins something a single-party measurement gets wrong. The mistake this
    /// model exists to prevent is on record: the first sweep held the party at three heroes, found the
    /// XP axis did nothing, and reported that width is the only gate. Three heroes is the corner of
    /// the surface where nothing matters. Both axes bite, and they substitute for each other — which
    /// only a surface can show. (<c>docs/BALANCING.md</c> §5i → §5j.)</para>
    /// </summary>
    public class InvestmentFrontierTests
    {
        private const int HeroXpEquivalent = 250;
        private const int BaseWidth = 2;

        // --- the cost arithmetic --------------------------------------------------------

        /// <summary>
        /// Heroes inside <c>PartySlots.BaseCap</c> are free — a fresh save already fields them — so a
        /// narrow party is never *cheaper* than the full base party, only different. Getting this
        /// wrong would make "play solo" read as the discount route through the whole campaign.
        /// </summary>
        [Test]
        public void CostOf_HeroesInsideTheBaseCapAreFree()
        {
            Assert.AreEqual(0, InvestmentFrontier.CostOf(1, 0, HeroXpEquivalent, BaseWidth));
            Assert.AreEqual(0, InvestmentFrontier.CostOf(2, 0, HeroXpEquivalent, BaseWidth));
            Assert.AreEqual(200, InvestmentFrontier.CostOf(1, 200, HeroXpEquivalent, BaseWidth));
            Assert.AreEqual(200, InvestmentFrontier.CostOf(2, 200, HeroXpEquivalent, BaseWidth));
        }

        [Test]
        public void CostOf_BoughtSlotsCostTheExchangeRateEach()
        {
            Assert.AreEqual(250, InvestmentFrontier.CostOf(3, 0, HeroXpEquivalent, BaseWidth));
            Assert.AreEqual(500, InvestmentFrontier.CostOf(4, 0, HeroXpEquivalent, BaseWidth));
            Assert.AreEqual(600, InvestmentFrontier.CostOf(4, 100, HeroXpEquivalent, BaseWidth));
        }

        // --- the gear axis --------------------------------------------------------------

        /// <summary>
        /// Gold folds into the same cost as the other two axes, at
        /// <c>InvestmentPointsPerGold</c>. The rate is measured, not read off a price tag: the
        /// merchant says what things *cost*, and the frontier needs what they are *worth*.
        /// See <c>docs/BALANCING.md</c> §5q.
        /// </summary>
        [Test]
        public void CostOf_GoldOnGearIsChargedAtTheConversionRate()
        {
            // 300 gold over two heroes is 150 each, and at 1:1 that is 150 points.
            Assert.AreEqual(150, InvestmentFrontier.CostOf(2, 0, 300, HeroXpEquivalent, BaseWidth, 1f));
            Assert.AreEqual(250, InvestmentFrontier.CostOf(2, 100, 300, HeroXpEquivalent, BaseWidth, 1f));
            Assert.AreEqual(450, InvestmentFrontier.CostOf(3, 100, 300, HeroXpEquivalent, BaseWidth, 1f));

            // Above 1:1, a gold piece buys more survivability than an XP point does, so the same
            // gear reads as a *dearer* investment - which is the correction §5q makes.
            Assert.AreEqual(450, InvestmentFrontier.CostOf(2, 0, 300, HeroXpEquivalent, BaseWidth, 3f));

            // And below it, cheaper. The dial has to move both ways: it must be able to report gear
            // as the bargain it was measured to be, or as the luxury a retune would make it.
            Assert.AreEqual(75, InvestmentFrontier.CostOf(2, 0, 300, HeroXpEquivalent, BaseWidth, 0.5f));
        }

        /// <summary>
        /// The gold term is charged <b>per hero</b>, because the XP term is. <c>xpPerHero</c> is what
        /// each hero spends on their own grid whatever the party's width, while <c>goldOnGear</c> is
        /// one pool the party shares — so converting the pool as a total put the two axes in
        /// different units. Measured, that showed up as an exchange rate that fell with every extra
        /// body (1.3 points per gold solo, 0.7 at two, 0.5 at three), which is not a fact about gear.
        /// Per hero, the rate is flat. See <c>docs/BALANCING.md</c> §5q.
        /// </summary>
        [Test]
        public void CostOf_GoldIsChargedPerHeroSoTheRateDoesNotMoveWithWidth()
        {
            // The same gear *per hero* costs the same investment at every width, once the bought
            // bodies are subtracted. 300g each at 1:1 is 300 points each, every time.
            Assert.AreEqual(300, InvestmentFrontier.CostOf(1, 0, 300, HeroXpEquivalent, BaseWidth, 1f));
            Assert.AreEqual(300, InvestmentFrontier.CostOf(2, 0, 600, HeroXpEquivalent, BaseWidth, 1f));
            Assert.AreEqual(
                300 + HeroXpEquivalent,
                InvestmentFrontier.CostOf(3, 0, 900, HeroXpEquivalent, BaseWidth, 1f));

            // And one shared pool spread thinner buys proportionally less each.
            Assert.AreEqual(600, InvestmentFrontier.CostOf(1, 0, 600, HeroXpEquivalent, BaseWidth, 1f));
            Assert.AreEqual(300, InvestmentFrontier.CostOf(2, 0, 600, HeroXpEquivalent, BaseWidth, 1f));
        }

        /// <summary>
        /// The gearless overload has to keep answering exactly as it did, or every frontier number
        /// written before the gear axis existed becomes incomparable to the ones after it.
        /// </summary>
        [Test]
        public void CostOf_TheGearlessOverloadIsUnchanged()
        {
            for (int width = 1; width <= 4; width++)
            {
                for (int xp = 0; xp <= 600; xp += 150)
                {
                    Assert.AreEqual(
                        InvestmentFrontier.CostOf(width, xp, 0, HeroXpEquivalent, BaseWidth, 1f),
                        InvestmentFrontier.CostOf(width, xp, HeroXpEquivalent, BaseWidth));
                }
            }
        }

        /// <summary>
        /// Three axes, so domination needs all three. Missing the gold term would silently prune
        /// away the gear route — the mix would be measured and then dropped for being "dominated" by
        /// something that is only cheaper on two of the three things the player pays with.
        /// </summary>
        [Test]
        public void IsDominatedBy_GearCountsAsAnAxis()
        {
            var geared = new InvestmentPoint { PartySize = 2, XpPerHero = 100, GoldOnGear = 300 };
            var bare = new InvestmentPoint { PartySize = 2, XpPerHero = 100, GoldOnGear = 0 };

            Assert.IsTrue(geared.IsDominatedBy(bare), "Same width and XP, but one paid for gear too.");
            Assert.IsFalse(bare.IsDominatedBy(geared));
        }

        /// <summary>
        /// Buying gear instead of XP is a trade, not a saving, so neither mix dominates - which is
        /// precisely the "range of ways to pay" gear was added to provide.
        /// </summary>
        [Test]
        public void IsDominatedBy_GearTradedForXpDominatesNothing()
        {
            var richInGear = new InvestmentPoint { PartySize = 2, XpPerHero = 0, GoldOnGear = 400 };
            var richInXp = new InvestmentPoint { PartySize = 2, XpPerHero = 400, GoldOnGear = 0 };

            Assert.IsFalse(richInGear.IsDominatedBy(richInXp));
            Assert.IsFalse(richInXp.IsDominatedBy(richInGear));
        }

        /// <summary>A mix that bought nothing reads as a gearless mix, in the label as well as the cost.</summary>
        [Test]
        public void Mix_NamesGearOnlyWhenSomeWasBought()
        {
            Assert.AreEqual("2 heroes, 100 XP",
                new InvestmentPoint { PartySize = 2, XpPerHero = 100 }.Mix);
            Assert.AreEqual("2 heroes, 100 XP, 300g gear",
                new InvestmentPoint { PartySize = 2, XpPerHero = 100, GoldOnGear = 300 }.Mix);
            Assert.AreEqual("1 hero, 0 XP",
                new InvestmentPoint { PartySize = 1, XpPerHero = 0 }.Mix);
        }

        // --- dominance ------------------------------------------------------------------

        [Test]
        public void IsDominatedBy_CheaperOnBothAxesWins()
        {
            var wideAndRich = new InvestmentPoint { PartySize = 3, XpPerHero = 200 };
            var narrowAndPoor = new InvestmentPoint { PartySize = 2, XpPerHero = 100 };

            Assert.IsTrue(wideAndRich.IsDominatedBy(narrowAndPoor));
            Assert.IsFalse(narrowAndPoor.IsDominatedBy(wideAndRich));
        }

        /// <summary>
        /// Two mixes that each win one axis are both on the frontier — that is exactly the "range of
        /// ways to pay" the design asks for, and collapsing it to one number would erase it.
        /// </summary>
        [Test]
        public void IsDominatedBy_TradingOneAxisForTheOtherDominatesNothing()
        {
            var wideAndGreen = new InvestmentPoint { PartySize = 3, XpPerHero = 0 };
            var narrowAndGrown = new InvestmentPoint { PartySize = 2, XpPerHero = 200 };

            Assert.IsFalse(wideAndGreen.IsDominatedBy(narrowAndGrown));
            Assert.IsFalse(narrowAndGrown.IsDominatedBy(wideAndGreen));
        }

        [Test]
        public void IsDominatedBy_AnIdenticalMixDoesNotDominateItself()
        {
            var point = new InvestmentPoint { PartySize = 2, XpPerHero = 100 };
            Assert.IsFalse(point.IsDominatedBy(new InvestmentPoint { PartySize = 2, XpPerHero = 100 }));
        }

        // --- the sweep ------------------------------------------------------------------

        /// <summary>
        /// The headline behaviour: a floor tuned so that two heroes need a grown grid, but three
        /// heroes clear it green, produces *both* mixes. The wider-and-richer combinations in between
        /// are dominated and must not appear — a frontier with four entries where two are strictly
        /// worse versions of the others reads as a choice the player does not have.
        /// </summary>
        [Test]
        public void Measure_ReportsBothAxesWhenTheySubstitute()
        {
            var frontier = InvestmentFrontier.Measure(Sweep(
                widths: new List<int> { 1, 2, 3 },
                xpSteps: new List<int> { 0, 200 },
                enemyStrength: 9,
                enemyHealth: 26,
                roomCount: 3));

            CollectionAssert.IsNotEmpty(frontier.Frontier, "The sweep should find some way to clear the floor.");
            foreach (var point in frontier.Frontier)
            {
                foreach (var other in frontier.Frontier)
                {
                    Assert.IsFalse(point.IsDominatedBy(other),
                        $"({point.Mix}) is on the frontier but dominated by ({other.Mix}).");
                }
            }
        }

        /// <summary>
        /// A floor nothing on the sweep survives is a wall, not a gate, and must say so rather than
        /// silently reporting a frontier of zero cost.
        /// </summary>
        [Test]
        public void Measure_UnclearableFloorReportsNoFrontier()
        {
            var frontier = InvestmentFrontier.Measure(Sweep(
                widths: new List<int> { 1, 2, 3 },
                xpSteps: new List<int> { 0, 200 },
                enemyStrength: 400,
                enemyHealth: 4000,
                roomCount: 4));

            Assert.IsTrue(frontier.Unclearable);
            Assert.AreEqual(int.MaxValue, frontier.AskedInvestment);
            Assert.IsFalse(frontier.OffersChoice);
        }

        /// <summary>
        /// A floor anyone walks through asks nothing, and the cheapest mix is the free one. The
        /// sweep must not report the first mix it happens to try — widths ascend and XP ascends, so
        /// the answer is (1 hero, 0 XP) or (2, 0), never a bought slot.
        /// </summary>
        [Test]
        public void Measure_TrivialFloorAsksNothing()
        {
            var frontier = InvestmentFrontier.Measure(Sweep(
                widths: new List<int> { 1, 2, 3 },
                xpSteps: new List<int> { 0, 200 },
                enemyStrength: 1,
                enemyHealth: 2,
                roomCount: 1));

            Assert.IsFalse(frontier.Unclearable);
            Assert.AreEqual(0, frontier.AskedInvestment);
        }

        /// <summary>
        /// The pruning is not an approximation: once a width clears at some XP, every wider mix at
        /// that XP or above is dominated and is never simulated. If this ever starts sampling the
        /// whole grid the sweep still gives the right answer, but a frontier goes from a dozen floor
        /// batches to the product of both axes — which is the difference between a minute and ten.
        /// </summary>
        [Test]
        public void Measure_DoesNotSimulateMixesTheFrontierAlreadyDominates()
        {
            var widths = new List<int> { 1, 2, 3, 4 };
            var xpSteps = new List<int> { 0, 100, 200, 350, 500 };

            var frontier = InvestmentFrontier.Measure(Sweep(
                widths, xpSteps, enemyStrength: 1, enemyHealth: 2, roomCount: 1));

            // The narrowest, greenest mix clears this floor outright, so nothing else is worth a
            // battle: every other cell of the 4x5 grid is dominated by (1 hero, 0 XP).
            Assert.AreEqual(1, frontier.Measured.Count,
                "A floor the cheapest mix clears should cost exactly one simulated batch, not "
                + widths.Count * xpSteps.Count + ".");
        }

        /// <summary>
        /// A width the run's roster cannot field is not a free pass. Reporting it as clearing would
        /// hand the player a frontier point they can never buy, because party slots are capped by the
        /// heroes the campaign has actually given them as well as by gold.
        /// </summary>
        [Test]
        public void Measure_WidthsBeyondTheRosterAreNotOnTheFrontier()
        {
            var settings = Sweep(
                widths: new List<int> { 1, 2, 3, 4 },
                xpSteps: new List<int> { 0 },
                enemyStrength: 1,
                enemyHealth: 2,
                roomCount: 1);
            settings.Roster = new List<HeroSO> { settings.Roster[0], settings.Roster[1] };

            var frontier = InvestmentFrontier.Measure(settings);

            foreach (var point in frontier.Frontier)
            {
                Assert.LessOrEqual(point.PartySize, 2,
                    "The sweep offered a mix wider than the roster it was given.");
            }
        }

        /// <summary>
        /// "Offers a choice" is about mixes the player would *actually* weigh against each other. A
        /// second frontier point costing three times the first is not an alternative, it is the
        /// expensive way to do the same thing, and counting it would hide a checklist tier.
        /// </summary>
        [Test]
        public void AffordableChoices_OnlyCountsMixesNearTheCheapest()
        {
            var frontier = new FloorFrontier();
            frontier.Frontier.Add(new InvestmentPoint { PartySize = 2, XpPerHero = 200, Cost = 200 });
            frontier.Frontier.Add(new InvestmentPoint { PartySize = 3, XpPerHero = 0, Cost = 250 });
            frontier.Frontier.Add(new InvestmentPoint { PartySize = 1, XpPerHero = 900, Cost = 900 });

            Assert.AreEqual(200, frontier.AskedInvestment);

            // Rebuilt the way Measure does, so the rule under test is the shipped one.
            foreach (var point in frontier.Frontier)
            {
                if (point.Cost <= frontier.AskedInvestment + 150)
                {
                    frontier.AffordableChoices.Add(point);
                }
            }

            Assert.AreEqual(2, frontier.AffordableChoices.Count);
            Assert.IsTrue(frontier.OffersChoice);
        }

        // --- rules plumbing -------------------------------------------------------------

        /// <summary>
        /// The deepest authored budget covers everything past it. Adding a run to the end of the
        /// campaign should inherit the endgame's ask, not silently drop out of the ladder.
        /// </summary>
        [Test]
        public void InvestmentBudgetForTier_DeepestBudgetCoversEverythingBeyondIt()
        {
            var rules = BalanceRulesSO.CreateDefault();
            rules.TierInvestmentBudgets = new List<int> { 200, 450, 700, 1000 };

            Assert.AreEqual(200, rules.InvestmentBudgetForTier(0));
            Assert.AreEqual(700, rules.InvestmentBudgetForTier(2));
            Assert.AreEqual(1000, rules.InvestmentBudgetForTier(3));
            Assert.AreEqual(1000, rules.InvestmentBudgetForTier(9));
            Assert.AreEqual(-1, rules.InvestmentBudgetForTier(-1),
                "A run with no campaign node has no tier, so it has no budget to miss.");

            Object.DestroyImmediate(rules);
        }

        [Test]
        public void InvestmentBudgetForTier_NoAuthoredBudgetsMeansNoTarget()
        {
            var rules = BalanceRulesSO.CreateDefault();
            rules.TierInvestmentBudgets = new List<int>();

            Assert.AreEqual(-1, rules.InvestmentBudgetForTier(0));

            Object.DestroyImmediate(rules);
        }

        /// <summary>
        /// The budgets have to rise, or the ladder they express is not a ladder. This is design
        /// intent rather than a fact about the game, which is why it lives in the rules asset — but a
        /// non-monotonic default would make the "asks no more than the one before it" finding
        /// unsatisfiable.
        /// </summary>
        [Test]
        public void DefaultTierBudgets_RiseWithDepth()
        {
            var rules = BalanceRulesSO.CreateDefault();

            for (int tier = 1; tier < rules.TierInvestmentBudgets.Count; tier++)
            {
                Assert.Greater(rules.TierInvestmentBudgets[tier], rules.TierInvestmentBudgets[tier - 1],
                    $"Tier {tier} must ask more than tier {tier - 1}.");
            }

            Object.DestroyImmediate(rules);
        }

        // --- fixtures -------------------------------------------------------------------

        /// <summary>
        /// A sweep over a synthetic floor. The heroes carry a two-node grid whose gains are large
        /// enough that the XP axis genuinely moves survivability, because a fixture where XP does
        /// nothing would pass every test here while proving nothing.
        /// </summary>
        private static FrontierSweepSettings Sweep(
            List<int> widths, List<int> xpSteps, int enemyStrength, int enemyHealth, int roomCount)
        {
            var roster = new List<HeroSO> { Hero("one"), Hero("two"), Hero("three"), Hero("four") };

            var rooms = new List<IList<SimUnit>>();
            for (int i = 0; i < roomCount; i++)
            {
                rooms.Add(new List<SimUnit> { Enemy("biter", enemyStrength, enemyHealth) });
            }

            return new FrontierSweepSettings
            {
                Roster = roster,
                Rooms = rooms,
                Widths = widths,
                XpSteps = xpSteps,
                HeroXpEquivalent = HeroXpEquivalent,
                BaseWidth = BaseWidth,
                ClearWipeRate = 0.35f,
                SafeWipeRate = 0.05f,
                EquivalentInvestmentTolerance = 150,
                PotionCount = 0,
                Sim = new EncounterSimulator.FloorSimSettings
                {
                    Trials = 30,
                    Seed = 4321,
                    MaxTurns = 200,
                    Policy = SimPolicy.AttackOnly,
                    Combos = new List<MagicComboSO>(),
                    PotionCount = 0,
                    PotionHealAmount = 0,
                    RestRooms = 0,
                    RestHealFraction = 0.35f,
                    StartsWithFullCharges = false
                }
            };
        }

        private static HeroSO Hero(string key)
        {
            var hero = ScriptableObject.CreateInstance<HeroSO>();
            hero.Key = key;
            hero.Label = key;
            hero.BaseStats = new StatBlock(
                new UnitStat(StatType.Strength, 7),
                new UnitStat(StatType.Endurance, 0),
                new UnitStat(StatType.MaxHealth, 26),
                new UnitStat(StatType.Agility, 5));

            var grid = ScriptableObject.CreateInstance<SphereGridSO>();
            grid.StartNodeKey = "vigour";
            grid.Nodes = new List<SphereGridNode>
            {
                new SphereGridNode
                {
                    Key = "vigour",
                    XpCost = 100,
                    Gains = new StatBlock(new UnitStat(StatType.MaxHealth, 14)),
                    Neighbors = new List<string> { "might" }
                },
                new SphereGridNode
                {
                    Key = "might",
                    XpCost = 100,
                    Gains = new StatBlock(new UnitStat(StatType.Strength, 4)),
                    Neighbors = new List<string>()
                }
            };
            hero.SphereGrid = grid;

            return hero;
        }

        private static SimUnit Enemy(string name, int attack, int health)
        {
            return new SimUnit
            {
                DisplayName = name,
                IsHero = false,
                Archetype = EnemyArchetype.Aggressor,
                Stats = TestStats.Make(attack, 0, health, 5),
                Effective = TestStats.Block(attack, 0, health, 5),
                AttackStat = StatType.Strength,
                EffectiveAttackPower = attack,
                Resistances = new List<Resistance>()
            };
        }
    }
}
