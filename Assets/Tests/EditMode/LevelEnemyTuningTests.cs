using System.Collections.Generic;
using Assets.Scripts.Enemies;
using Assets.Scripts.UnitStats;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// An <see cref="EnemySO"/> is a template; the level it appears in owns its numbers. These pin the
    /// resolution order - template, then Difficulty, then StatScales, then Overrides - and the two
    /// rounding rules that stop a multiplier erasing an enemy.
    /// </summary>
    public class LevelEnemyTuningTests
    {
        private readonly List<ScriptableObject> _created = new List<ScriptableObject>();

        private EnemySO Enemy(int strength = 5, int health = 20, int endurance = 2, int agility = 5)
        {
            var enemy = ScriptableObject.CreateInstance<EnemySO>();
            _created.Add(enemy);
            enemy.DisplayName = "Test Enemy";
            enemy.BaseStats[StatType.Strength] = strength;
            enemy.BaseStats[StatType.Endurance] = endurance;
            enemy.BaseStats[StatType.MaxHealth] = health;
            enemy.BaseStats[StatType.Agility] = agility;
            enemy.XpReward = 10;
            enemy.GoldReward = 5;
            return enemy;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var asset in _created)
            {
                if (asset != null)
                {
                    Object.DestroyImmediate(asset);
                }
            }
            _created.Clear();
        }

        // ------------------------------------------------------------------ the layers

        [Test]
        public void Difficulty_ScalesHealthAndStrength_AndLeavesTheRestAlone()
        {
            var enemy = Enemy(strength: 5, health: 20, endurance: 2, agility: 5);
            var tuning = new LevelEnemyTuning { Difficulty = 2f };

            var stats = tuning.StatsFor(enemy);

            Assert.AreEqual(10, stats[StatType.Strength]);
            Assert.AreEqual(40, stats[StatType.MaxHealth]);
            Assert.AreEqual(2, stats[StatType.Endurance], "Difficulty is the durability/damage dial, not a blanket buff.");
            Assert.AreEqual(5, stats[StatType.Agility]);
        }

        [Test]
        public void StatScales_ReachStatsDifficultyDoesNot()
        {
            var enemy = Enemy(strength: 5, health: 20, agility: 4);
            var tuning = new LevelEnemyTuning { Difficulty = 1f };
            tuning.StatScales.Add(new StatScale { Stat = StatType.Agility, Multiplier = 2f });

            var stats = tuning.StatsFor(enemy);

            Assert.AreEqual(8, stats[StatType.Agility]);
            Assert.AreEqual(5, stats[StatType.Strength]);
            Assert.AreEqual(20, stats[StatType.MaxHealth]);
        }

        [Test]
        public void StatScales_ApplyOnTopOfDifficulty()
        {
            // The lever the tuning pass actually needs: tanky without hitting harder. Rounding happens
            // at each step, so this is 20 -> 28 -> 56 rather than 20 x 1.4 x 2.
            var enemy = Enemy(strength: 5, health: 20);
            var tuning = new LevelEnemyTuning { Difficulty = 1.4f };
            tuning.StatScales.Add(new StatScale { Stat = StatType.MaxHealth, Multiplier = 2f });

            var stats = tuning.StatsFor(enemy);

            Assert.AreEqual(56, stats[StatType.MaxHealth]);
            Assert.AreEqual(7, stats[StatType.Strength]);
        }

        [Test]
        public void Override_WinsOverTemplateAndScaling()
        {
            var enemy = Enemy(strength: 5, health: 20);
            var tuning = new LevelEnemyTuning { Difficulty = 3f };
            var over = new EnemyStatOverride { Enemy = enemy };
            over.Stats[StatType.MaxHealth] = 100;
            tuning.Overrides.Add(over);

            var stats = tuning.StatsFor(enemy);

            Assert.AreEqual(100, stats[StatType.MaxHealth], "An absolute override is absolute.");
            Assert.AreEqual(15, stats[StatType.Strength], "Stats the override does not list still scale.");
        }

        [Test]
        public void Override_OnlyTouchesTheStatsItLists()
        {
            var enemy = Enemy(strength: 5, health: 20, endurance: 2, agility: 5);
            var tuning = new LevelEnemyTuning();
            var over = new EnemyStatOverride { Enemy = enemy };
            over.Stats[StatType.Endurance] = 9;
            tuning.Overrides.Add(over);

            var stats = tuning.StatsFor(enemy);

            Assert.AreEqual(9, stats[StatType.Endurance]);
            Assert.AreEqual(5, stats[StatType.Strength]);
            Assert.AreEqual(20, stats[StatType.MaxHealth]);
            Assert.AreEqual(5, stats[StatType.Agility]);
        }

        [Test]
        public void Override_AppliesOnlyToItsOwnEnemy()
        {
            var boss = Enemy(strength: 6, health: 80);
            var trash = Enemy(strength: 5, health: 20);
            var tuning = new LevelEnemyTuning { Difficulty = 2f };
            var over = new EnemyStatOverride { Enemy = boss };
            over.Stats[StatType.MaxHealth] = 90;
            tuning.Overrides.Add(over);

            Assert.AreEqual(90, tuning.StatsFor(boss)[StatType.MaxHealth],
                "This is how a boss escapes the trash dial.");
            Assert.AreEqual(40, tuning.StatsFor(trash)[StatType.MaxHealth]);
        }

        // ------------------------------------------------------------------ rounding rules

        [Test]
        public void APositiveStat_NeverScalesToZero()
        {
            // A 0.5 multiplier on a Strength of 1 must not produce a harmless enemy.
            var enemy = Enemy(strength: 1, health: 3);
            var tuning = new LevelEnemyTuning { Difficulty = 0.1f };

            var stats = tuning.StatsFor(enemy);

            Assert.AreEqual(1, stats[StatType.Strength]);
            Assert.AreEqual(1, stats[StatType.MaxHealth]);
        }

        [Test]
        public void AStatTheTemplateLeavesAtZero_StaysZero()
        {
            var enemy = Enemy();
            enemy.BaseStats[StatType.Intelligence] = 0;
            var tuning = new LevelEnemyTuning { Difficulty = 5f };
            tuning.StatScales.Add(new StatScale { Stat = StatType.Intelligence, Multiplier = 5f });

            Assert.AreEqual(0, tuning.StatsFor(enemy)[StatType.Intelligence],
                "Multiplying nothing is still nothing.");
        }

        [Test]
        public void StatScaleRowsAtNone_AreIgnored()
        {
            // The state a freshly added inspector row is in.
            var enemy = Enemy(health: 20);
            var tuning = new LevelEnemyTuning();
            tuning.StatScales.Add(new StatScale { Stat = StatType.None, Multiplier = 10f });

            Assert.AreEqual(20, tuning.StatsFor(enemy)[StatType.MaxHealth]);
        }

        [Test]
        public void StatsFor_DoesNotMutateTheTemplate()
        {
            // The template is shared by every level that places the enemy; scaling one must not
            // quietly rewrite the asset for the others.
            var enemy = Enemy(strength: 5, health: 20);
            var tuning = new LevelEnemyTuning { Difficulty = 3f };

            tuning.StatsFor(enemy);

            Assert.AreEqual(5, enemy.BaseStats[StatType.Strength]);
            Assert.AreEqual(20, enemy.BaseStats[StatType.MaxHealth]);
        }

        // ------------------------------------------------------------------ rewards

        [Test]
        public void Rewards_FollowTheirMultipliers()
        {
            var enemy = Enemy();
            var tuning = new LevelEnemyTuning { XpMultiplier = 2.5f, GoldMultiplier = 2f };

            Assert.AreEqual(25, tuning.XpFor(enemy));
            Assert.AreEqual(10, tuning.GoldFor(enemy));
        }

        [Test]
        public void Rewards_CanBeOverriddenPerEnemy()
        {
            var enemy = Enemy();
            var tuning = new LevelEnemyTuning { XpMultiplier = 2f };
            tuning.Overrides.Add(new EnemyStatOverride { Enemy = enemy, XpReward = 99 });

            Assert.AreEqual(99, tuning.XpFor(enemy));
            Assert.AreEqual(5, tuning.GoldFor(enemy), "A zero override leaves the multiplied value alone.");
        }

        // ------------------------------------------------------------------ the null path

        [Test]
        public void NoTuning_IsTheTemplateExactly()
        {
            // Free-play in the scene, and any level nobody has tuned, must behave as before.
            var enemy = Enemy(strength: 5, health: 20);

            var stats = LevelEnemyTuning.StatsFor(enemy, null);

            Assert.AreEqual(5, stats[StatType.Strength]);
            Assert.AreEqual(20, stats[StatType.MaxHealth]);
            Assert.AreEqual(10, LevelEnemyTuning.XpFor(enemy, null));
            Assert.AreEqual(5, LevelEnemyTuning.GoldFor(enemy, null));
        }

        [Test]
        public void AFreshTuning_IsIdentity()
        {
            var enemy = Enemy(strength: 5, health: 20);
            var tuning = new LevelEnemyTuning();

            Assert.IsTrue(tuning.IsIdentity, "Difficulty 1 with nothing authored must change nothing.");
            Assert.AreEqual(5, tuning.StatsFor(enemy)[StatType.Strength]);
            Assert.AreEqual(20, tuning.StatsFor(enemy)[StatType.MaxHealth]);
        }

        [Test]
        public void IsIdentity_IsFalseOnceAnythingIsAuthored()
        {
            Assert.IsFalse(new LevelEnemyTuning { Difficulty = 1.5f }.IsIdentity);
            Assert.IsFalse(new LevelEnemyTuning { XpMultiplier = 2f }.IsIdentity);

            var scaled = new LevelEnemyTuning();
            scaled.StatScales.Add(new StatScale { Stat = StatType.Agility, Multiplier = 2f });
            Assert.IsFalse(scaled.IsIdentity);
        }
    }
}
