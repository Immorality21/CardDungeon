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
    /// The balance model's side of summons (§4b): the simulated summoner holds its charge for the
    /// floor's hardest room, summons there, refills at a refuge, and a "summon build" beelines to the
    /// summon node that a greedy spend never reaches.
    /// </summary>
    public class SummonSimulationTests
    {
        private static SimUnit Hero(string name, int attack, int health, int agility = 5)
        {
            return new SimUnit
            {
                DisplayName = name,
                HeroKey = name,
                IsHero = true,
                Stats = TestStats.Make(attack, 0, health, agility),
                Effective = TestStats.Block(attack, 0, health, agility),
                AttackStat = StatType.Strength,
                EffectiveAttackPower = attack,
                Resistances = new List<Resistance>()
            };
        }

        private static SimUnit Enemy(string name, int attack, int health, bool boss = false)
        {
            EnemySO definition = null;
            if (boss)
            {
                definition = ScriptableObject.CreateInstance<EnemySO>();
                definition.IsBoss = true;
            }
            return new SimUnit
            {
                DisplayName = name,
                IsHero = false,
                Archetype = EnemyArchetype.Aggressor,
                Definition = definition,
                Stats = TestStats.Make(attack, 0, health, 5),
                Effective = TestStats.Block(attack, 0, health, 5),
                AttackStat = StatType.Strength,
                EffectiveAttackPower = attack,
                Resistances = new List<Resistance>()
            };
        }

        private static SimUnit Summoner(int charges = 1)
        {
            var hero = Hero("warrior", 6, 200);
            var summon = ScriptableObject.CreateInstance<SummonSO>();
            summon.Key = "TestBoar";
            summon.TargetType = MagicTargetType.AllAllies;
            summon.BaseCharges = charges;
            summon.Effects = new List<SpellEffect>
            {
                new SpellEffect { EffectType = SpellEffectType.Buff, Power = 50, PowerMode = PowerMode.PercentOfTargetStat, BuffType = BuffType.Strength, Duration = 3 }
            };
            var grant = new SummonGrant { Key = "TestBoar" };
            hero.Summons.Add(new SimSummonSlot
            {
                Summon = summon,
                Grant = grant,
                Castable = SummonOps.BuildCastable(summon, grant),
                Charges = charges,
                MaxCharges = charges
            });
            return hero;
        }

        private static PartyBaseline Party(params SimUnit[] heroes)
        {
            var party = new PartyBaseline { SourceLabel = "test" };
            foreach (var hero in heroes)
            {
                party.Heroes.Add(new HeroBaseline { Effective = hero.Effective.Clone(), Unit = hero });
            }
            return party;
        }

        private static List<IList<SimUnit>> Rooms(params SimUnit[][] rooms)
        {
            var list = new List<IList<SimUnit>>();
            foreach (var room in rooms)
            {
                list.Add(new List<SimUnit>(room));
            }
            return list;
        }

        private static EncounterSimulator.FloorSimSettings Settings(int restRooms = 0)
        {
            return new EncounterSimulator.FloorSimSettings
            {
                Trials = 20,
                Seed = 777,
                MaxTurns = 300,
                Policy = SimPolicy.Adaptive,
                Combos = new List<MagicComboSO>(),
                RestRooms = restRooms,
                RestHealFraction = 0.35f
            };
        }

        private static List<IList<SimUnit>> ThreeRoomsBossLast()
        {
            return Rooms(
                new[] { Enemy("rat", 2, 12) },
                new[] { Enemy("rat", 2, 12) },
                new[] { Enemy("boss", 3, 60, boss: true) });
        }

        // --- which room the charge is held for ------------------------------------

        [Test]
        public void PickSummonRoom_PrefersTheBossRoom_EvenIfAnotherIsHeavier()
        {
            var rooms = Rooms(
                new[] { Enemy("brute", 20, 200) },
                new[] { Enemy("boss", 3, 30, boss: true) },
                new[] { Enemy("rat", 2, 12) });

            Assert.AreEqual(1, EncounterSimulator.PickSummonRoom(rooms));
        }

        [Test]
        public void PickSummonRoom_WithoutABoss_PicksTheHeaviestRoom()
        {
            var rooms = Rooms(
                new[] { Enemy("rat", 2, 12) },
                new[] { Enemy("brute", 6, 50), Enemy("brute", 6, 50) },
                new[] { Enemy("rat", 2, 12) });

            Assert.AreEqual(1, EncounterSimulator.PickSummonRoom(rooms));
        }

        // --- the floor sim spends it there -------------------------------------------

        [Test]
        public void RunFloor_TheSummonerSummonsOnce_InTheHeldRoom()
        {
            var outcome = EncounterSimulator.RunFloor(Party(Summoner()), ThreeRoomsBossLast(), Settings());

            Assert.AreEqual(1f, outcome.AverageSummonsUsed, 1e-4f,
                "One charge, held past the two trash rooms and spent in the boss room.");
        }

        [Test]
        public void RunFloor_WithSummonsOff_NeverSummons()
        {
            var settings = Settings();
            settings.UseSummons = false;

            var outcome = EncounterSimulator.RunFloor(Party(Summoner()), ThreeRoomsBossLast(), settings);

            Assert.AreEqual(0f, outcome.AverageSummonsUsed);
        }

        [Test]
        public void RunFloor_AttackOnlyPolicy_NeverSummons()
        {
            var settings = Settings();
            settings.Policy = SimPolicy.AttackOnly;

            var outcome = EncounterSimulator.RunFloor(Party(Summoner()), ThreeRoomsBossLast(), settings);

            Assert.AreEqual(0f, outcome.AverageSummonsUsed);
        }

        [Test]
        public void RunFloor_ALaterFloor_HasNoSummonUntilARefugeRefillsIt()
        {
            var drained = Settings();
            drained.StartsWithFullCharges = false;
            var noRest = EncounterSimulator.RunFloor(Party(Summoner()), ThreeRoomsBossLast(), drained);
            Assert.AreEqual(0f, noRest.AverageSummonsUsed, "A floor after the first starts with summons spent.");

            var withRest = Settings(restRooms: 1);
            withRest.StartsWithFullCharges = false;
            var rested = EncounterSimulator.RunFloor(Party(Summoner()), ThreeRoomsBossLast(), withRest);
            Assert.AreEqual(1f, rested.AverageSummonsUsed, 1e-4f, "A refuge before the boss room restores it.");
        }

        // --- the summon build ------------------------------------------------------------

        private static SphereGridSO DeepSummonGrid()
        {
            var grid = ScriptableObject.CreateInstance<SphereGridSO>();
            grid.StartNodeKey = "start";
            grid.Nodes = new List<SphereGridNode>
            {
                new SphereGridNode { Key = "start", XpCost = 10, Neighbors = new List<string> { "cheap", "deep-1" } },
                // The breadth trap: cheap nodes a greedy spend buys before the deep branch.
                new SphereGridNode { Key = "cheap", XpCost = 5, Neighbors = new List<string> { "cheap-2" } },
                new SphereGridNode { Key = "cheap-2", XpCost = 35 },
                new SphereGridNode { Key = "deep-1", XpCost = 40, Neighbors = new List<string> { "summon" } },
                new SphereGridNode { Key = "summon", Kind = SphereNodeKind.Summon, GrantedSummonKey = "X", XpCost = 60 },
            };
            return grid;
        }

        [Test]
        public void BeelineThenGreedy_Affordable_ReachesTheTarget_ThenSpendsTheRest()
        {
            var nodes = SphereGridOps.BeelineThenGreedy(DeepSummonGrid(), "summon", 115);

            CollectionAssert.IsSubsetOf(new[] { "start", "deep-1", "summon" }, nodes);
            Assert.Contains("cheap", nodes, "The 5 XP left over buys the cheap node.");
        }

        [Test]
        public void BeelineThenGreedy_Unaffordable_IsTheGreedySpend()
        {
            var grid = DeepSummonGrid();

            var nodes = SphereGridOps.BeelineThenGreedy(grid, "summon", 100);

            CollectionAssert.AreEqual(SphereGridOps.GreedySpend(grid, null, 100, out _), nodes);
            CollectionAssert.DoesNotContain(nodes, "summon");
        }

        [Test]
        public void GreedySpend_NeverWalksToTheDeepSummon_WhichIsWhyTheSummonBuildExists()
        {
            var grid = DeepSummonGrid();
            var greedy = SphereGridOps.GreedySpend(grid, null, 115, out _);
            CollectionAssert.DoesNotContain(greedy, "summon",
                "At the same budget the breadth build buys the cheap node and stalls short of the summon.");

            var hero = ScriptableObject.CreateInstance<HeroSO>();
            hero.SphereGrid = grid;
            Assert.Contains("summon", InvestmentFrontier.SummonBuildNodes(hero, 115));
        }
    }
}
