using System.Collections.Generic;
using Assets.Scripts.Balance;
using Assets.Scripts.Combat;
using Assets.Scripts.Enemies;
using Assets.Scripts.Enemies.Behaviors;
using Assets.Scripts.Heroes;
using Assets.Scripts.Items;
using Assets.Scripts.Rooms;
using Assets.Scripts.UnitStats;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// Unit tests for the closed-form balance metrics. These pin the metrics to the game's own
    /// primitives so a change to <see cref="DamageCalculator"/> or a behaviour constant cannot silently
    /// change what the balance window reports.
    /// </summary>
    public class BalanceMathTests
    {
        private static SimUnit Unit(string name, int attack, int defense, int health, int agility, bool isHero)
        {
            return new SimUnit
            {
                DisplayName = name,
                IsHero = isHero,
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

        [Test]
        public void ExpectedCritMultiplier_MatchesCombatManagerConstants()
        {
            float expected = 1f + CombatManager.CritChance * (CombatManager.CritMultiplier - 1f);
            Assert.AreEqual(expected, BalanceMath.ExpectedCritMultiplier(), 0.0001f);
        }

        [Test]
        public void AverageDamage_IsFlatDamageTimesCritExpectation()
        {
            var target = Unit("target", 0, 5, 100, 5, true);
            int flat = DamageCalculator.Calculate(10, 5, DamageType.Normal, target.Resistances);

            float average = BalanceMath.AverageDamage(10, target);

            Assert.AreEqual(flat * BalanceMath.ExpectedCritMultiplier(), average, 0.0001f);
        }

        [Test]
        public void AverageDamage_RespectsResistance()
        {
            var resistant = Unit("resistant", 0, 0, 100, 5, false);
            resistant.Resistances.Add(new Resistance { DamageType = DamageType.Fire, Percent = 50f });

            float normal = BalanceMath.AverageDamage(20, resistant, DamageType.Normal);
            float fire = BalanceMath.AverageDamage(20, resistant, DamageType.Fire);

            Assert.Less(fire, normal, "A 50% fire resistance must reduce fire damage below normal damage.");
        }

        [Test]
        public void DefenseReduction_AtDefenseConstant_IsHalf()
        {
            float reduction = BalanceMath.EnduranceReduction((int)DamageCalculator.EnduranceConstant);
            Assert.AreEqual(0.5f, reduction, 0.0001f);
        }

        [Test]
        public void HitsToKill_RoundsUp()
        {
            Assert.AreEqual(4, BalanceMath.HitsToKill(3f, 10));
            Assert.AreEqual(1, BalanceMath.HitsToKill(10f, 10));
        }

        [Test]
        public void HitsToKill_ZeroDamage_NeverKills()
        {
            Assert.AreEqual(int.MaxValue, BalanceMath.HitsToKill(0f, 10));
        }

        [Test]
        public void TurnsPerTick_ScalesWithAgility()
        {
            var slow = Unit("slow", 1, 0, 10, 5, true);
            var fast = Unit("fast", 1, 0, 10, 10, true);

            Assert.AreEqual(2f, BalanceMath.TurnsPerTick(fast) / BalanceMath.TurnsPerTick(slow), 0.0001f);
        }

        [Test]
        public void AverageOffenseMultiplier_Bruiser_AveragesChargeAndHeavy()
        {
            // A telegraphed heavy is one dead wind-up turn and one 2.5x payload, so the long-run
            // average is half the payload. The constant used to live on BruiserBehavior; it is
            // authored on the Bruiser preset now, and this pins that the arithmetic survived.
            float expected = 2.5f / 2f;
            Assert.AreEqual(expected, BalanceMath.AverageOffenseMultiplier(EnemyArchetype.Bruiser, 2), 0.0001f);
        }

        [Test]
        public void AverageOffenseMultiplier_Boss_GrowsWithPartySize()
        {
            float solo = BalanceMath.AverageOffenseMultiplier(EnemyArchetype.Boss, 1);
            float four = BalanceMath.AverageOffenseMultiplier(EnemyArchetype.Boss, 4);

            Assert.Greater(four, solo,
                "The boss signature hits every hero, so a wider party makes its average turn worth more.");
        }

        [Test]
        public void DangerIndex_BelowOne_WhenPartyOutgunsEnemy()
        {
            var party = new List<SimUnit> { Unit("hero", 20, 10, 200, 8, true) };
            var enemies = new List<SimUnit> { Unit("rat", 2, 0, 10, 4, false) };

            Assert.Less(BalanceMath.DangerIndex(party, enemies), 1f);
        }

        [Test]
        public void DangerIndex_AboveOne_WhenEnemyOutgunsParty()
        {
            var party = new List<SimUnit> { Unit("hero", 2, 0, 5, 5, true) };
            var enemies = new List<SimUnit> { Unit("dragon", 30, 8, 300, 6, false) };

            Assert.Greater(BalanceMath.DangerIndex(party, enemies), 1f);
        }

        [Test]
        public void DangerIndex_HarmlessEnemy_IsZero()
        {
            var party = new List<SimUnit> { Unit("hero", 10, 0, 100, 5, true) };
            var enemies = new List<SimUnit> { Unit("pacifist", 0, 0, 10, 5, false) };

            Assert.AreEqual(0f, BalanceMath.DangerIndex(party, enemies), 0.0001f);
        }

        [Test]
        public void PartyTurnsToKill_ScalesWithEnemyHealth()
        {
            var party = new List<SimUnit> { Unit("hero", 10, 0, 100, 5, true) };
            var weak = Unit("weak", 1, 0, 20, 5, false);
            var tough = Unit("tough", 1, 0, 40, 5, false);

            float weakTurns = BalanceMath.PartyTurnsToKill(party, weak);
            float toughTurns = BalanceMath.PartyTurnsToKill(party, tough);

            Assert.AreEqual(2f, toughTurns / weakTurns, 0.01f);
        }

        [Test]
        public void Grade_ClassifiesAgainstBand()
        {
            Assert.AreEqual(BalanceSeverity.Ok, BalanceMath.Grade(0.5f, 0.2f, 0.8f, 0f, 1f));
            Assert.AreEqual(BalanceSeverity.Warning, BalanceMath.Grade(0.9f, 0.2f, 0.8f, 0f, 1f));
            Assert.AreEqual(BalanceSeverity.Critical, BalanceMath.Grade(1.2f, 0.2f, 0.8f, 0f, 1f));
        }

        [Test]
        public void HeroStatCalculator_WithGear_AppliesRawThenPercentage()
        {
            var sword = ScriptableObject.CreateInstance<ItemSO>();
            sword.Bonuses = new List<ItemBonus>
            {
                new ItemBonus { StatType = StatType.Strength, BonusType = BonusType.Raw, Value = 5f }
            };

            var amulet = ScriptableObject.CreateInstance<ItemSO>();
            amulet.Bonuses = new List<ItemBonus>
            {
                new ItemBonus { StatType = StatType.Strength, BonusType = BonusType.Percentage, Value = 50f }
            };

            var stats = TestStats.Make(10, 0, 100, 5);
            var effective = HeroStatCalculator.WithGear(stats, new List<ItemSO> { sword, amulet });

            // (10 + 5) * 1.5 = 22.5, rounded to 22 by Mathf.RoundToInt's banker's rounding.
            Assert.AreEqual(Mathf.RoundToInt(15f * 1.5f), effective[StatType.Strength]);

            Object.DestroyImmediate(sword);
            Object.DestroyImmediate(amulet);
        }
    }
}
