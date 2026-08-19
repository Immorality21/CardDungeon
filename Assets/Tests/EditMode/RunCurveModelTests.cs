using System.Collections.Generic;
using Assets.Scripts.Balance;
using Assets.Scripts.Dungeon;
using Assets.Scripts.Enemies;
using Assets.Scripts.Heroes;
using Assets.Scripts.Rooms;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// Tests for the encounter and run-curve models on synthetic assets, so the expectation maths is
    /// pinned independently of whatever the project's real content happens to be.
    /// </summary>
    public class RunCurveModelTests
    {
        private readonly List<ScriptableObject> _created = new List<ScriptableObject>();

        private T Make<T>() where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            _created.Add(asset);
            return asset;
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

        private BalanceRulesSO Rules()
        {
            var rules = Make<BalanceRulesSO>();
            rules.ReferenceHeroLevel = 1;
            return rules;
        }

        private PartyBaseline SturdyParty()
        {
            var hero = Make<HeroSO>();
            hero.Label = "Tester";
            hero.BaseAttack = 12;
            hero.BaseDefense = 5;
            hero.BaseHealth = 120;
            hero.BaseAgility = 5;
            hero.LevelProgression = new List<LevelConfiguration>();

            return PartyBaseline.Build(new List<HeroSO> { hero }, 1);
        }

        private EnemySO Goblin(int attack = 4, int health = 20, int xp = 10, int gold = 5)
        {
            var enemy = Make<EnemySO>();
            enemy.DisplayName = "Goblin";
            enemy.Attack = attack;
            enemy.Defense = 1;
            enemy.Health = health;
            enemy.Agility = 5;
            enemy.XpReward = xp;
            enemy.GoldReward = gold;
            enemy.Archetype = EnemyArchetype.Aggressor;
            enemy.DrawableMagics = new List<DrawableMagicEntry>();
            return enemy;
        }

        private RoomSO Room(EnemySO enemy, float chance, int rolls)
        {
            var room = Make<RoomSO>();
            room.Name = "Test Room";
            room.Width = 3;
            room.Height = 3;
            room.EnemySpawnTable = new List<EnemySpawnEntry>();
            if (enemy != null)
            {
                room.EnemySpawnTable.Add(new EnemySpawnEntry
                {
                    Enemy = enemy,
                    SpawnChance = chance,
                    EvaluationCount = rolls
                });
            }
            return room;
        }

        [Test]
        public void RoomEncounter_ExpectedCount_IsChanceTimesRolls()
        {
            var encounter = RoomEncounter.Build(Room(Goblin(), 0.5f, 3), null, false, SturdyParty(), Rules());

            Assert.AreEqual(1.5f, encounter.Expected.TotalCount, 0.0001f);
            Assert.AreEqual(3f, encounter.WorstCase.TotalCount, 0.0001f);
        }

        [Test]
        public void RoomEncounter_GuaranteeAllSpawns_MakesExpectedEqualWorstCase()
        {
            var encounter = RoomEncounter.Build(Room(Goblin(), 0.2f, 4), null, true, SturdyParty(), Rules());

            Assert.AreEqual(4f, encounter.Expected.TotalCount, 0.0001f);
            Assert.AreEqual(encounter.WorstCaseDanger, encounter.ExpectedDanger, 0.0001f);
        }

        [Test]
        public void RoomEncounter_ConnectorRoom_HasNoCombat()
        {
            var room = Room(Goblin(), 1f, 1);
            room.IsConnectorRoom = true;

            var encounter = RoomEncounter.Build(room, null, false, SturdyParty(), Rules());

            Assert.IsFalse(encounter.IsCombatRoom);
        }

        [Test]
        public void RoomEncounter_SpawnOverride_TakesPrecedenceOverTheRoomTable()
        {
            var tableEnemy = Goblin(attack: 4, health: 20);
            var overrideEnemy = Goblin(attack: 9, health: 90);
            var room = Room(tableEnemy, 1f, 1);

            var overrideTable = new List<EnemySpawnEntry>
            {
                new EnemySpawnEntry { Enemy = overrideEnemy, SpawnChance = 1f, EvaluationCount = 1 }
            };

            var encounter = RoomEncounter.Build(room, overrideTable, false, SturdyParty(), Rules());

            Assert.AreEqual(90f, encounter.Expected.HealthPool, 0.0001f);
        }

        [Test]
        public void RoomEncounter_WorstCase_IsAtLeastAsDangerousAsExpected()
        {
            var encounter = RoomEncounter.Build(Room(Goblin(), 0.4f, 3), null, false, SturdyParty(), Rules());

            Assert.GreaterOrEqual(encounter.WorstCaseDanger, encounter.ExpectedDanger);
        }

        [Test]
        public void RunCurve_GeneratedLevel_SpreadsRoomsAcrossThePoolUniformly()
        {
            var goblin = Goblin();
            var combat = Room(goblin, 1f, 1);
            var empty = Room(null, 0f, 1);

            var template = Make<LevelDefinitionSO>();
            template.Key = "T";
            template.RoomsToGenerate = 10;
            template.RoomPool = new List<RoomSO> { combat, empty };

            var run = Make<RunDefinitionSO>();
            run.Levels = new List<RunLevelEntry>
            {
                new RunLevelEntry { LevelTemplate = template, LevelName = "L1" }
            };

            var curve = RunCurve.Build(run, SturdyParty(), Rules());

            // Ten rooms drawn uniformly from a two-room pool: five of each, and only one has enemies.
            Assert.AreEqual(5f, curve.Levels[0].ExpectedCombatRooms, 0.0001f);
            Assert.AreEqual(5f, curve.Levels[0].ExpectedEnemyCount, 0.0001f);
        }

        [Test]
        public void RunCurve_AttritionLoad_RisesWithRoomCount()
        {
            var goblin = Goblin();
            var combat = Room(goblin, 1f, 1);

            var small = Make<LevelDefinitionSO>();
            small.RoomsToGenerate = 4;
            small.RoomPool = new List<RoomSO> { combat };

            var large = Make<LevelDefinitionSO>();
            large.RoomsToGenerate = 16;
            large.RoomPool = new List<RoomSO> { combat };

            var run = Make<RunDefinitionSO>();
            run.Levels = new List<RunLevelEntry>
            {
                new RunLevelEntry { LevelTemplate = small, LevelName = "Small" },
                new RunLevelEntry { LevelTemplate = large, LevelName = "Large" }
            };

            var curve = RunCurve.Build(run, SturdyParty(), Rules());

            Assert.Greater(curve.Levels[1].AttritionLoad, curve.Levels[0].AttritionLoad);
            Assert.AreEqual(1, curve.DifficultyJumps.Count);
            Assert.Greater(curve.DifficultyJumps[0], 0f, "A harder second level should register a positive jump.");
        }

        [Test]
        public void RunCurve_FlatCurve_RegistersNoGrowth()
        {
            var combat = Room(Goblin(), 1f, 1);

            var template = Make<LevelDefinitionSO>();
            template.RoomsToGenerate = 6;
            template.RoomPool = new List<RoomSO> { combat };

            var run = Make<RunDefinitionSO>();
            run.Levels = new List<RunLevelEntry>
            {
                new RunLevelEntry { LevelTemplate = template, LevelName = "A" },
                new RunLevelEntry { LevelTemplate = template, LevelName = "B" }
            };

            var curve = RunCurve.Build(run, SturdyParty(), Rules());

            Assert.AreEqual(0f, curve.DifficultyJumps[0], 0.0001f,
                "Two identical levels must read as a flat curve, not as escalation.");
        }

        [Test]
        public void RunCurve_Boss_ReplacesTheExitRoomRatherThanAddingToTheLevel()
        {
            var combat = Room(Goblin(), 1f, 1);

            var template = Make<LevelDefinitionSO>();
            template.RoomsToGenerate = 4;
            template.RoomPool = new List<RoomSO> { combat };

            var boss = Goblin(attack: 9, health: 120, xp: 60, gold: 50);
            boss.DisplayName = "Warden";
            boss.IsBoss = true;
            boss.Archetype = EnemyArchetype.Boss;

            var withoutBoss = Make<RunDefinitionSO>();
            withoutBoss.Levels = new List<RunLevelEntry>
            {
                new RunLevelEntry { LevelTemplate = template, LevelName = "L" }
            };

            var withBoss = Make<RunDefinitionSO>();
            withBoss.Levels = new List<RunLevelEntry>
            {
                new RunLevelEntry { LevelTemplate = template, LevelName = "L", BossEnemy = boss }
            };

            var party = SturdyParty();
            var rules = Rules();

            var plain = RunCurve.Build(withoutBoss, party, rules);
            var bossed = RunCurve.Build(withBoss, party, rules);

            Assert.AreEqual(4f, plain.Levels[0].ExpectedCombatRooms, 0.0001f);
            Assert.AreEqual(4f, bossed.Levels[0].ExpectedCombatRooms, 0.0001f,
                "EnemyManager wipes the exit room before placing the boss, so the boss replaces a room.");
            Assert.Greater(bossed.Levels[0].BossDanger, 0f);
            Assert.Greater(bossed.Levels[0].BossToTrashRatio, 1f, "A boss should outweigh the level's trash.");
        }

        [Test]
        public void RunCurve_ManualLayout_UsesTheHandPlacedRooms()
        {
            var combat = Room(Goblin(), 1f, 1);

            var layout = Make<ManualLevelLayoutSO>();
            layout.Key = "Manual";
            layout.Rooms = new List<ManualRoomEntry>
            {
                new ManualRoomEntry { RoomTemplate = combat },
                new ManualRoomEntry { RoomTemplate = combat },
                new ManualRoomEntry { RoomTemplate = combat }
            };

            var run = Make<RunDefinitionSO>();
            run.Levels = new List<RunLevelEntry>
            {
                new RunLevelEntry { ManualLayout = layout, LevelName = "Manual" }
            };

            var curve = RunCurve.Build(run, SturdyParty(), Rules());

            Assert.AreEqual("Manual", curve.Levels[0].LayoutKind);
            Assert.AreEqual(3f, curve.Levels[0].ExpectedCombatRooms, 0.0001f);
        }

        [Test]
        public void RunCurve_RewardTotals_AccumulateAcrossLevels()
        {
            var combat = Room(Goblin(xp: 10, gold: 5), 1f, 1);

            var template = Make<LevelDefinitionSO>();
            template.RoomsToGenerate = 3;
            template.RoomPool = new List<RoomSO> { combat };

            var run = Make<RunDefinitionSO>();
            run.Levels = new List<RunLevelEntry>
            {
                new RunLevelEntry { LevelTemplate = template, LevelName = "A" },
                new RunLevelEntry { LevelTemplate = template, LevelName = "B" }
            };

            var curve = RunCurve.Build(run, SturdyParty(), Rules());

            Assert.AreEqual(60f, curve.TotalExpectedXp, 0.0001f);
            Assert.AreEqual(30f, curve.TotalExpectedGold, 0.0001f);
        }

        [Test]
        public void VarietyReport_AllAggressorsWithNoResistances_IsFlaggedAsOneDimensional()
        {
            var members = new List<WeightedEnemy>();
            for (int i = 0; i < 3; i++)
            {
                var enemy = Goblin();
                members.Add(new WeightedEnemy
                {
                    Definition = enemy,
                    Unit = SimUnit.FromEnemy(enemy),
                    Weight = 1f
                });
            }

            var report = VarietyReport.Build(members, new List<Assets.Scripts.Cards.MagicSO>(), Rules());

            Assert.AreEqual(1f, report.DominantArchetypeShare, 0.0001f);
            Assert.AreEqual(EnemyArchetype.Aggressor, report.DominantArchetype);
            Assert.AreEqual(0f, report.ResistanceCoverage, 0.0001f);
        }

        [Test]
        public void VarietyReport_MixedArchetypes_LowersTheDominantShare()
        {
            var aggressor = Goblin();
            var healer = Goblin();
            healer.Archetype = EnemyArchetype.Healer;

            var members = new List<WeightedEnemy>
            {
                new WeightedEnemy { Definition = aggressor, Unit = SimUnit.FromEnemy(aggressor), Weight = 1f },
                new WeightedEnemy { Definition = healer, Unit = SimUnit.FromEnemy(healer), Weight = 1f }
            };

            var report = VarietyReport.Build(members, new List<Assets.Scripts.Cards.MagicSO>(), Rules());

            Assert.AreEqual(0.5f, report.DominantArchetypeShare, 0.0001f);
        }
    }
}
