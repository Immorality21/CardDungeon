using System.Collections.Generic;
using Assets.Scripts.Balance;
using Assets.Scripts.Dungeon;
using Assets.Scripts.Enemies;
using Assets.Scripts.Heroes;
using Assets.Scripts.Rooms;
using Assets.Scripts.UnitStats;
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
            rules.ReferenceHeroXp = 0;
            return rules;
        }

        private PartyBaseline SturdyParty()
        {
            var hero = Make<HeroSO>();
            hero.Label = "Tester";
            hero.BaseStats[StatType.Strength] = 12;
            hero.BaseStats[StatType.Endurance] = 5;
            hero.BaseStats[StatType.MaxHealth] = 120;
            hero.BaseStats[StatType.Agility] = 5;
            return PartyBaseline.Build(new List<HeroSO> { hero }, 0);
        }

        private EnemySO Goblin(int strength = 4, int health = 20, int xp = 10, int gold = 5)
        {
            var enemy = Make<EnemySO>();
            enemy.DisplayName = "Goblin";
            enemy.BaseStats[StatType.Strength] = strength;
            enemy.BaseStats[StatType.Endurance] = 1;
            enemy.BaseStats[StatType.MaxHealth] = health;
            enemy.BaseStats[StatType.Agility] = 5;
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
            room.Kind = RoomKind.Connector;

            var encounter = RoomEncounter.Build(room, null, false, SturdyParty(), Rules());

            Assert.IsFalse(encounter.IsCombatRoom);
        }

        [Test]
        public void RoomEncounter_SpawnOverride_TakesPrecedenceOverTheRoomTable()
        {
            var tableEnemy = Goblin(strength: 4, health: 20);
            var overrideEnemy = Goblin(strength: 9, health: 90);
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

            // Ten rooms drawn uniformly from a two-room pool, minus the party's starting room
            // (EnemyManager.SpawnEnemies skips it): 9 populated, 4.5 of each, and only one of the
            // two pool entries has enemies.
            Assert.AreEqual(4.5f, curve.Levels[0].ExpectedCombatRooms, 0.0001f);
            Assert.AreEqual(4.5f, curve.Levels[0].ExpectedEnemyCount, 0.0001f);
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

            var boss = Goblin(strength: 9, health: 120, xp: 60, gold: 50);
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

            // Four generated rooms less the unpopulated starting room.
            Assert.AreEqual(3f, plain.Levels[0].ExpectedCombatRooms, 0.0001f);
            Assert.AreEqual(3f, bossed.Levels[0].ExpectedCombatRooms, 0.0001f,
                "EnemyManager wipes the exit room before placing the boss, so the boss replaces a room.");
            Assert.Greater(bossed.Levels[0].BossDanger, 0f);
            Assert.Greater(bossed.Levels[0].BossToTrashRatio, 1f, "A boss should outweigh the level's trash.");

            // The sealed exit room is a synthetic entry, not a room from the pool. Anything walking
            // a level's rooms to build a *floor* has to be able to tell the two apart and skip it,
            // because it appends the boss itself — the floor simulation could not, and fought every
            // boss in the campaign twice until 2026-08-28.
            int bossRooms = 0;
            foreach (var room in bossed.Levels[0].Rooms)
            {
                if (room.IsBossRoom)
                {
                    bossRooms++;
                }
            }
            Assert.AreEqual(1, bossRooms, "Exactly one room of a boss level is the sealed exit room.");
            foreach (var room in plain.Levels[0].Rooms)
            {
                Assert.IsFalse(room.IsBossRoom, "A level with no boss has no boss room.");
            }
        }

        /// <summary>
        /// Rounding a spawn table member by member deletes rooms. <c>Bog Shaman 0.4 +
        /// Hex Weaver 0.5</c> describes a room holding about one enemy; rounded independently it holds
        /// none, and the floor simulation drops it entirely. That is how The Mire Throne — a
        /// four-combat-room floor — came to be simulated as its boss standing alone.
        /// </summary>
        [Test]
        public void ToDiscreteUnits_FractionsThatRoundToNothingIndividuallyStillFillTheRoom()
        {
            var group = new WeightedEnemyGroup();
            group.Add(Goblin(strength: 4, health: 20), 0.4f);
            var weaver = Goblin(strength: 9, health: 30);
            weaver.DisplayName = "Weaver";
            group.Add(weaver, 0.5f);

            var units = group.ToDiscreteUnits();

            Assert.AreEqual(1, units.Count, "0.4 + 0.5 is one enemy's worth of room, not none.");
            Assert.AreEqual("Weaver", units[0].DisplayName,
                "The leftover seat goes to the likeliest occupant, not to whoever was authored first.");
        }

        [Test]
        public void ToDiscreteUnits_KeepsTheGroupsTotalSize()
        {
            var group = new WeightedEnemyGroup();
            group.Add(Goblin(), 1.4f);
            var other = Goblin(strength: 6, health: 25);
            other.DisplayName = "Other";
            group.Add(other, 1.4f);

            // 2.8 expected enemies rounds to three, and each member's whole part is honoured first.
            Assert.AreEqual(3, group.ToDiscreteUnits().Count);
        }

        /// <summary>
        /// A room the level barely populates is still allowed to be empty — apportionment moves the
        /// rounding to the group total, it does not put a floor under it.
        /// </summary>
        [Test]
        public void ToDiscreteUnits_ATrulyEmptyExpectationStaysEmpty()
        {
            var group = new WeightedEnemyGroup();
            group.Add(Goblin(), 0.2f);

            Assert.IsEmpty(group.ToDiscreteUnits());
        }

        /// <summary>
        /// The result must not depend on the order the spawn table was authored in, or two rooms with
        /// identical content would simulate differently.
        /// </summary>
        [Test]
        public void ToDiscreteUnits_IsIndependentOfSpawnTableOrder()
        {
            var heavy = Goblin(strength: 9, health: 40);
            heavy.DisplayName = "Heavy";
            var light = Goblin(strength: 3, health: 15);
            light.DisplayName = "Light";

            var forwards = new WeightedEnemyGroup();
            forwards.Add(light, 0.3f);
            forwards.Add(heavy, 0.6f);

            var backwards = new WeightedEnemyGroup();
            backwards.Add(heavy, 0.6f);
            backwards.Add(light, 0.3f);

            Assert.AreEqual(1, forwards.ToDiscreteUnits().Count);
            Assert.AreEqual(forwards.ToDiscreteUnits()[0].DisplayName,
                backwards.ToDiscreteUnits()[0].DisplayName);
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
            // Three hand-placed rooms, one of which is the start room (StartRoomIndex 0) and so
            // never spawns anything.
            Assert.AreEqual(2f, curve.Levels[0].ExpectedCombatRooms, 0.0001f);
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

            // Two levels x (3 generated rooms - the empty start room) x one 10xp/5g goblin.
            Assert.AreEqual(40f, curve.TotalExpectedXp, 0.0001f);
            Assert.AreEqual(20f, curve.TotalExpectedGold, 0.0001f);
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
