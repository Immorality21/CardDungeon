using System.Collections.Generic;
using Assets.Scripts.Balance;
using Assets.Scripts.Cards;
using Assets.Scripts.Combat;
using Assets.Scripts.Enemies;
using Assets.Scripts.Rooms;
using Assets.Scripts.UnitStats;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// Tests for the headless battle simulator, including the drift pin that matters most: the
    /// simulator re-implements <c>CombatManager</c>'s turn loop, so a test has to hold its arithmetic
    /// to the real thing or the whole balance model quietly stops describing the game.
    /// </summary>
    public class EncounterSimulatorTests
    {
        private static SimUnit Hero(string name, int attack, int defense, int health, int agility = 5)
        {
            return new SimUnit
            {
                DisplayName = name,
                HeroKey = name,
                IsHero = true,
                Stats = TestStats.Make(attack, defense, health, agility),
                // Effective is what SimUnit.GetEffectiveStat reads (gear and level gains are
                // folded in at build time), and AttackStat is what a Strength buff has to name to
                // reach the attacker. Neither is derived from Stats, so a factory that sets only
                // Stats produces a unit with 0 defense, 0 agility and no buffable attack stat.
                Effective = TestStats.Block(attack, defense, health, agility),
                AttackStat = StatType.Strength,
                EffectiveAttackPower = attack,
                Resistances = new List<Resistance>()
            };
        }

        private static SimUnit Enemy(string name, int attack, int defense, int health, int agility = 5,
            EnemyArchetype archetype = EnemyArchetype.Aggressor)
        {
            return new SimUnit
            {
                DisplayName = name,
                IsHero = false,
                Archetype = archetype,
                Stats = TestStats.Make(attack, defense, health, agility),
                Effective = TestStats.Block(attack, defense, health, agility),
                AttackStat = StatType.Strength,
                EffectiveAttackPower = attack,
                Resistances = new List<Resistance>()
            };
        }

        private static PartyBaseline Party(params SimUnit[] heroes)
        {
            var party = new PartyBaseline { SourceLabel = "test" };
            foreach (var hero in heroes)
            {
                party.Heroes.Add(new HeroBaseline
                {
                    Level = 1,
                    MaxDefinedLevel = 1,
                    Effective = TestStats.Block(
                        hero.EffectiveAttackPower,
                        hero.Effective[StatType.Endurance],
                        hero.Effective[StatType.MaxHealth],
                        hero.Effective[StatType.Agility]),
                    Unit = hero
                });
            }
            return party;
        }

        private static SimSettings Settings(int trials = 60, SimPolicy policy = SimPolicy.AttackOnly)
        {
            return new SimSettings
            {
                Trials = trials,
                Seed = 1234,
                MaxTurns = 300,
                Policy = policy,
                Combos = new List<MagicComboSO>()
            };
        }

        /// <summary>
        /// The drift pin. CombatManager.ExecuteAttack computes
        /// Calculate(round((attack + buff) * multiplier), defense + buff, Normal, resistances) and then
        /// rolls a crit of max(dmg + 1, round(dmg * CritMultiplier)). Every simulated hit must land on
        /// exactly one of those two values.
        /// </summary>
        [Test]
        public void ResolveAttack_AlwaysMatchesCombatManagerArithmetic()
        {
            var attacker = Enemy("attacker", 12, 0, 100);
            var buffTracker = new CombatBuffTracker();

            int expectedBase = DamageCalculator.Calculate(12, 4, DamageType.Normal, new List<Resistance>());
            int expectedCrit = Mathf.Max(expectedBase + 1, Mathf.RoundToInt(expectedBase * CombatManager.CritMultiplier));

            bool sawBase = false;
            bool sawCrit = false;

            Random.InitState(99);
            for (int i = 0; i < 600; i++)
            {
                var target = Hero("target", 0, 4, 100000);
                int damage = EncounterSimulator.ResolveAttack(attacker, target, buffTracker);

                Assert.That(damage == expectedBase || damage == expectedCrit,
                    $"Simulated hit dealt {damage}; CombatManager would deal {expectedBase} or {expectedCrit} on a crit.");

                sawBase |= damage == expectedBase;
                sawCrit |= damage == expectedCrit;
            }

            Assert.IsTrue(sawBase, "Non-crit hits should occur.");
            Assert.IsTrue(sawCrit, "Crits should occur at the configured crit chance.");
        }

        [Test]
        public void ResolveAttack_AppliesMultiplierBeforeDefense()
        {
            var attacker = Enemy("bruiser", 10, 0, 100);
            var target = Hero("target", 0, 20, 100000);
            var buffTracker = new CombatBuffTracker();

            int expected = DamageCalculator.Calculate(
                Mathf.RoundToInt(10 * 2.5f), 20, DamageType.Normal, target.Resistances);

            Random.InitState(7);
            int minimum = int.MaxValue;
            for (int i = 0; i < 200; i++)
            {
                var fresh = Hero("target", 0, 20, 100000);
                minimum = Mathf.Min(minimum, EncounterSimulator.ResolveAttack(attacker, fresh, buffTracker, 2.5f));
            }

            Assert.AreEqual(expected, minimum, "The non-crit floor must equal the multiplier applied pre-defense.");
        }

        [Test]
        public void ResolveAttack_BuffsStackOnEffectiveStats()
        {
            var attacker = Enemy("attacker", 10, 0, 100);
            var buffTracker = new CombatBuffTracker();
            buffTracker.ApplyBuff(attacker, Assets.Scripts.UnitStats.StatType.Strength, 10, 5);

            int expected = DamageCalculator.Calculate(20, 0, DamageType.Normal, new List<Resistance>());

            Random.InitState(3);
            int minimum = int.MaxValue;
            for (int i = 0; i < 200; i++)
            {
                var target = Hero("target", 0, 0, 100000);
                minimum = Mathf.Min(minimum, EncounterSimulator.ResolveAttack(attacker, target, buffTracker));
            }

            Assert.AreEqual(expected, minimum);
        }

        [Test]
        public void Run_IsDeterministicForAGivenSeed()
        {
            var party = Party(Hero("hero", 8, 4, 60));
            var enemies = new List<SimUnit> { Enemy("goblin", 5, 2, 30) };

            var first = EncounterSimulator.Run(party, enemies, Settings());
            var second = EncounterSimulator.Run(party, enemies, Settings());

            Assert.AreEqual(first.Wins, second.Wins);
            Assert.AreEqual(first.AverageTurns, second.AverageTurns, 0.0001f);
            Assert.AreEqual(first.AverageEndHealthFraction, second.AverageEndHealthFraction, 0.0001f);
        }

        [Test]
        public void Run_DoesNotDisturbTheCallersRandomStream()
        {
            var party = Party(Hero("hero", 8, 4, 60));
            var enemies = new List<SimUnit> { Enemy("goblin", 5, 2, 30) };

            Random.InitState(4242);
            float before = Random.Range(0f, 1f);

            Random.InitState(4242);
            EncounterSimulator.Run(party, enemies, Settings());
            float after = Random.Range(0f, 1f);

            Assert.AreEqual(before, after, 0.0001f,
                "The simulator must save and restore Random.state so it cannot perturb anything else.");
        }

        [Test]
        public void Run_OverwhelmingParty_AlwaysWins()
        {
            var party = Party(Hero("champion", 60, 20, 500, 10));
            var enemies = new List<SimUnit> { Enemy("rat", 1, 0, 5, 3) };

            var outcome = EncounterSimulator.Run(party, enemies, Settings());

            Assert.AreEqual(1f, outcome.WinRate, 0.0001f);
            Assert.AreEqual(0, outcome.Stalemates);
        }

        [Test]
        public void Run_HopelessParty_NeverWins()
        {
            var party = Party(Hero("victim", 1, 0, 4, 4));
            var enemies = new List<SimUnit> { Enemy("titan", 40, 15, 400, 9) };

            var outcome = EncounterSimulator.Run(party, enemies, Settings());

            Assert.AreEqual(0f, outcome.WinRate, 0.0001f);
            Assert.Greater(outcome.AverageHeroDeaths, 0f);
        }

        [Test]
        public void Run_TracksStalemates_WhenNeitherSideCanFinish()
        {
            // Minimum damage is 1, so a huge health pool against a tiny one still resolves eventually
            // — the turn cap is what surfaces a fight that would never end in practice.
            var party = Party(Hero("wall", 1, 100, 100000, 5));
            var enemies = new List<SimUnit> { Enemy("wall", 1, 100, 100000, 5) };

            var settings = Settings(4);
            settings.MaxTurns = 40;

            var outcome = EncounterSimulator.Run(party, enemies, settings);

            Assert.AreEqual(4, outcome.Stalemates);
            Assert.AreEqual(0, outcome.Wins);
        }

        [Test]
        public void Run_BruiserSpendsEveryOtherTurnCharging()
        {
            var party = Party(Hero("hero", 1, 0, 100000, 5));
            var bruiser = Enemy("bruiser", 10, 0, 100000, 5, EnemyArchetype.Bruiser);
            var aggressor = Enemy("aggressor", 10, 0, 100000, 5);

            var settings = Settings(1);
            settings.MaxTurns = 60;

            var withBruiser = EncounterSimulator.Run(party, new List<SimUnit> { bruiser }, settings);
            var withAggressor = EncounterSimulator.Run(party, new List<SimUnit> { aggressor }, settings);

            // Both stalemate against an unkillable hero; the point is that they resolve identically in
            // structure, which keeps the archetype comparison meaningful.
            Assert.AreEqual(withAggressor.Trials, withBruiser.Trials);
            Assert.AreEqual(withAggressor.Stalemates, withBruiser.Stalemates);
        }

        [Test]
        public void RunAllPolicies_ReturnsEveryPolicy()
        {
            var party = Party(Hero("hero", 8, 4, 60));
            var enemies = new List<SimUnit> { Enemy("goblin", 5, 2, 30) };

            var outcomes = EncounterSimulator.RunAllPolicies(party, enemies, Settings());

            Assert.AreEqual(3, outcomes.Count);
            Assert.IsTrue(outcomes.ContainsKey(SimPolicy.AttackOnly));
            Assert.IsTrue(outcomes.ContainsKey(SimPolicy.MagicFirst));
            Assert.IsTrue(outcomes.ContainsKey(SimPolicy.Adaptive));
        }

        [Test]
        public void Adaptive_UsesPotionsWhenWounded()
        {
            var party = Party(Hero("hero", 6, 2, 40));
            party.PotionCount = 3;
            party.PotionHealAmount = 15;

            var enemies = new List<SimUnit> { Enemy("brawler", 6, 2, 60) };

            var settings = Settings(40, SimPolicy.Adaptive);
            settings.PotionCount = 3;
            settings.PotionHealAmount = 15;

            var outcome = EncounterSimulator.Run(party, enemies, settings);

            Assert.Greater(outcome.AveragePotionsUsed, 0f,
                "A fight that drops a hero below the heal threshold should see potions spent.");
        }

        [Test]
        public void AttackOnly_NeverSpendsPotionsOrCasts()
        {
            var party = Party(Hero("hero", 6, 2, 40));
            var enemies = new List<SimUnit> { Enemy("brawler", 6, 2, 60) };

            var settings = Settings(40, SimPolicy.AttackOnly);
            settings.PotionCount = 3;
            settings.PotionHealAmount = 15;

            var outcome = EncounterSimulator.Run(party, enemies, settings);

            Assert.AreEqual(0f, outcome.AveragePotionsUsed, 0.0001f);
            Assert.AreEqual(0f, outcome.AverageCastsUsed, 0.0001f);
        }
    }
}
