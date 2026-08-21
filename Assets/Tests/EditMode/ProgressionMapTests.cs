using System.Collections.Generic;
using Assets.Scripts.Balance;
using Assets.Scripts.Cards;
using Assets.Scripts.Combat;
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
    /// Tests for the unlock/elemental supply model. These pin the questions the Elements &amp; Unlocks tab
    /// answers: can this magic be drawn at all, when does a combo become possible, and is a level's
    /// resistance in an element the player can bring yet.
    /// </summary>
    public class ProgressionMapTests
    {
        private readonly List<ScriptableObject> _created = new List<ScriptableObject>();

        private T Make<T>(string name = null) where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            if (!string.IsNullOrEmpty(name))
            {
                asset.name = name;
            }
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

        // ---------------------------------------------------------------- fixtures

        private MagicSO Magic(string key, DamageType? damage, params MagicTag[] tags)
        {
            var magic = Make<MagicSO>(key);
            magic.Key = key;
            magic.DisplayName = key;
            magic.Tags = new List<MagicTag>(tags);
            magic.Effects = new List<SpellEffect>();
            if (damage.HasValue)
            {
                magic.Effects.Add(new SpellEffect
                {
                    EffectType = SpellEffectType.Damage,
                    Power = 4,
                    DamageType = damage.Value
                });
            }
            return magic;
        }

        private EnemySO Enemy(string name, MagicSO[] draws, DamageType[] resistTypes = null, float[] percents = null)
        {
            var enemy = Make<EnemySO>(name);
            enemy.DisplayName = name;
            enemy.BaseStats[StatType.Strength] = 4;
            enemy.BaseStats[StatType.Endurance] = 1;
            enemy.BaseStats[StatType.MaxHealth] = 20;
            enemy.BaseStats[StatType.Agility] = 5;
            enemy.XpReward = 10;
            enemy.GoldReward = 5;
            enemy.DrawableMagics = new List<DrawableMagicEntry>();
            foreach (var magic in draws)
            {
                enemy.DrawableMagics.Add(new DrawableMagicEntry { Magic = magic, Charges = 2 });
            }

            enemy.Resistances = new List<Resistance>();
            if (resistTypes != null)
            {
                for (int i = 0; i < resistTypes.Length; i++)
                {
                    enemy.Resistances.Add(new Resistance { DamageType = resistTypes[i], Percent = percents[i] });
                }
            }
            return enemy;
        }

        private RoomSO Room(EnemySO enemy)
        {
            var room = Make<RoomSO>();
            room.Name = "Room " + (enemy != null ? enemy.name : "empty");
            room.Width = 3;
            room.Height = 3;
            room.EnemySpawnTable = new List<EnemySpawnEntry>();
            if (enemy != null)
            {
                room.EnemySpawnTable.Add(new EnemySpawnEntry { Enemy = enemy, SpawnChance = 1f, EvaluationCount = 1 });
            }
            return room;
        }

        private LevelDefinitionSO Template(params RoomSO[] rooms)
        {
            var template = Make<LevelDefinitionSO>();
            template.RoomsToGenerate = rooms.Length;
            template.RoomPool = new List<RoomSO>(rooms);
            return template;
        }

        private PartyBaseline Party()
        {
            var hero = Make<HeroSO>();
            hero.Label = "Tester";
            hero.BaseStats[StatType.Strength] = 12;
            hero.BaseStats[StatType.Endurance] = 5;
            hero.BaseStats[StatType.MaxHealth] = 200;
            hero.BaseStats[StatType.Agility] = 5;
            hero.LevelProgression = new List<LevelConfiguration>();
            return PartyBaseline.Build(new List<HeroSO> { hero }, 1);
        }

        private ProgressionMap Build(
            List<RunDefinitionSO> runs,
            List<MagicSO> catalog,
            List<MagicComboSO> combos)
        {
            var rules = Make<BalanceRulesSO>();
            var party = Party();

            var curves = new List<RunCurve>();
            foreach (var run in runs)
            {
                curves.Add(RunCurve.Build(run, party, rules));
            }

            return ProgressionMap.Build(curves, catalog, combos);
        }

        // ---------------------------------------------------------------- magic reachability

        [Test]
        public void Magic_OfferedByAnEnemyInTheRun_IsReachable()
        {
            var fireball = Magic("Fireball", DamageType.Fire, MagicTag.Fire);
            var enemy = Enemy("Imp", new[] { fireball });

            var run = Make<RunDefinitionSO>("Run");
            run.Levels = new List<RunLevelEntry>
            {
                new RunLevelEntry { LevelTemplate = Template(Room(enemy)), LevelName = "L1" }
            };

            var map = Build(new List<RunDefinitionSO> { run }, new List<MagicSO> { fireball }, new List<MagicComboSO>());

            Assert.AreEqual(1, map.ReachableMagicCount);
            Assert.IsTrue(map.Magic[0].IsReachable);
            Assert.AreEqual("Imp", map.Magic[0].FirstSource.EnemyName);
            Assert.AreEqual(0, map.Magic[0].FirstSource.LevelIndex);
        }

        [Test]
        public void Magic_NoEnemyOffers_IsUnreachable()
        {
            var fireball = Magic("Fireball", DamageType.Fire, MagicTag.Fire);
            var orphan = Magic("Meteor", DamageType.Fire, MagicTag.Fire);
            var enemy = Enemy("Imp", new[] { fireball });

            var run = Make<RunDefinitionSO>("Run");
            run.Levels = new List<RunLevelEntry>
            {
                new RunLevelEntry { LevelTemplate = Template(Room(enemy)), LevelName = "L1" }
            };

            var map = Build(new List<RunDefinitionSO> { run },
                new List<MagicSO> { fireball, orphan }, new List<MagicComboSO>());

            Assert.AreEqual(1, map.ReachableMagicCount);
            Assert.AreEqual(2, map.CatalogMagicCount);
            Assert.AreEqual(0.5f, map.MagicCoverage, 0.0001f);

            var meteor = map.Magic.Find(m => m.Key == "Meteor");
            Assert.IsFalse(meteor.IsReachable);
            Assert.IsNull(meteor.FirstSource);
        }

        [Test]
        public void Magic_OnlyOnABoss_IsFlaggedBossGated()
        {
            var trashMagic = Magic("Slash", DamageType.Normal, MagicTag.Physical);
            var bossMagic = Magic("Doom", DamageType.Shadow, MagicTag.Dark);

            var trash = Enemy("Rat", new[] { trashMagic });
            var boss = Enemy("Warden", new[] { bossMagic });
            boss.IsBoss = true;
            boss.Archetype = EnemyArchetype.Boss;

            var run = Make<RunDefinitionSO>("Run");
            run.Levels = new List<RunLevelEntry>
            {
                new RunLevelEntry { LevelTemplate = Template(Room(trash)), LevelName = "L1", BossEnemy = boss }
            };

            var map = Build(new List<RunDefinitionSO> { run },
                new List<MagicSO> { trashMagic, bossMagic }, new List<MagicComboSO>());

            Assert.IsTrue(map.Magic.Find(m => m.Key == "Doom").BossGatedOnly);
            Assert.IsFalse(map.Magic.Find(m => m.Key == "Slash").BossGatedOnly);
        }

        // ---------------------------------------------------------------- run ordering

        [Test]
        public void Runs_AreOrderedBySequenceIndex_AndUnlocksCountedOnce()
        {
            var early = Magic("IceShard", DamageType.Ice, MagicTag.Ice);
            var late = Magic("Bolt", DamageType.Lightning, MagicTag.Lightning);

            var earlyEnemy = Enemy("Eye", new[] { early });
            var lateEnemy = Enemy("Golem", new[] { early, late });

            var second = Make<RunDefinitionSO>("Alpha");   // alphabetically first, deliberately
            second.SequenceIndex = 1;
            second.Levels = new List<RunLevelEntry>
            {
                new RunLevelEntry { LevelTemplate = Template(Room(lateEnemy)), LevelName = "L1" }
            };

            var first = Make<RunDefinitionSO>("Zeta");
            first.SequenceIndex = 0;
            first.Levels = new List<RunLevelEntry>
            {
                new RunLevelEntry { LevelTemplate = Template(Room(earlyEnemy)), LevelName = "L1" }
            };

            var map = Build(new List<RunDefinitionSO> { second, first },
                new List<MagicSO> { early, late }, new List<MagicComboSO>());

            Assert.AreEqual("Zeta", map.Runs[0].Name, "SequenceIndex must win over asset name.");
            Assert.AreEqual("Alpha", map.Runs[1].Name);
            Assert.IsFalse(map.RunOrderIsImplicit);

            // IceShard is offered in both runs but only counts as an unlock the first time.
            Assert.AreEqual(1, map.Runs[0].NewlyDrawable.Count);
            Assert.AreEqual("IceShard", map.Runs[0].NewlyDrawable[0].Key);
            Assert.AreEqual(1, map.Runs[1].NewlyDrawable.Count);
            Assert.AreEqual("Bolt", map.Runs[1].NewlyDrawable[0].Key);
        }

        [Test]
        public void Runs_WithNoSequenceIndex_ReportImplicitOrder()
        {
            var magic = Magic("Slash", DamageType.Normal, MagicTag.Physical);
            var enemy = Enemy("Rat", new[] { magic });

            var runA = Make<RunDefinitionSO>("A");
            runA.Levels = new List<RunLevelEntry>
            {
                new RunLevelEntry { LevelTemplate = Template(Room(enemy)), LevelName = "L1" }
            };
            var runB = Make<RunDefinitionSO>("B");
            runB.Levels = new List<RunLevelEntry>
            {
                new RunLevelEntry { LevelTemplate = Template(Room(enemy)), LevelName = "L1" }
            };

            var map = Build(new List<RunDefinitionSO> { runA, runB },
                new List<MagicSO> { magic }, new List<MagicComboSO>());

            Assert.IsTrue(map.RunOrderIsImplicit,
                "With every SequenceIndex at 0 and more than one run, the order is a guess and must say so.");
        }

        // ---------------------------------------------------------------- combos

        private MagicComboSO Combo(string name, params MagicTag[] required)
        {
            var combo = Make<MagicComboSO>(name);
            combo.Key = name;
            combo.ComboName = name;
            combo.RequiredTags = new List<MagicTag>(required);
            combo.BonusEffects = new List<SpellEffect>();
            return combo;
        }

        [Test]
        public void Combo_WithATagNoMagicCarries_IsUnreachable()
        {
            var fireball = Magic("Fireball", DamageType.Fire, MagicTag.Fire);
            var enemy = Enemy("Imp", new[] { fireball });
            var combo = Combo("Ignite", MagicTag.Fire, MagicTag.Oil);   // nothing carries Oil

            var run = Make<RunDefinitionSO>("Run");
            run.Levels = new List<RunLevelEntry>
            {
                new RunLevelEntry { LevelTemplate = Template(Room(enemy)), LevelName = "L1" }
            };

            var map = Build(new List<RunDefinitionSO> { run },
                new List<MagicSO> { fireball }, new List<MagicComboSO> { combo });

            var availability = map.Combos[0];
            Assert.IsFalse(availability.IsReachable);
            Assert.Contains(MagicTag.Oil, availability.TagsWithNoMagic);
            Assert.IsNull(availability.UnlockedAt);
            Assert.AreEqual(0, map.ReachableComboCount);
        }

        [Test]
        public void Combo_WithATagOnlyOnUndrawableMagic_IsUnreachable()
        {
            var fireball = Magic("Fireball", DamageType.Fire, MagicTag.Fire);
            var oilSlick = Magic("OilSlick", null, MagicTag.Oil);       // in the catalog, offered by nobody
            var enemy = Enemy("Imp", new[] { fireball });
            var combo = Combo("Ignite", MagicTag.Fire, MagicTag.Oil);

            var run = Make<RunDefinitionSO>("Run");
            run.Levels = new List<RunLevelEntry>
            {
                new RunLevelEntry { LevelTemplate = Template(Room(enemy)), LevelName = "L1" }
            };

            var map = Build(new List<RunDefinitionSO> { run },
                new List<MagicSO> { fireball, oilSlick }, new List<MagicComboSO> { combo });

            var availability = map.Combos[0];
            Assert.IsFalse(availability.IsReachable);
            Assert.IsEmpty(availability.TagsWithNoMagic);
            Assert.Contains(MagicTag.Oil, availability.TagsNotDrawable);
        }

        [Test]
        public void Combo_UnlocksWhereItsLastPieceBecomesDrawable()
        {
            var fireball = Magic("Fireball", DamageType.Fire, MagicTag.Fire);
            var oilSlick = Magic("OilSlick", null, MagicTag.Oil);

            var early = Enemy("Imp", new[] { fireball });
            var later = Enemy("Sludge", new[] { oilSlick });
            var combo = Combo("Ignite", MagicTag.Fire, MagicTag.Oil);

            var run = Make<RunDefinitionSO>("Run");
            run.Levels = new List<RunLevelEntry>
            {
                new RunLevelEntry { LevelTemplate = Template(Room(early)), LevelName = "L1" },
                new RunLevelEntry { LevelTemplate = Template(Room(later)), LevelName = "L2" }
            };

            var map = Build(new List<RunDefinitionSO> { run },
                new List<MagicSO> { fireball, oilSlick }, new List<MagicComboSO> { combo });

            var availability = map.Combos[0];
            Assert.IsTrue(availability.IsReachable);
            Assert.AreEqual(1, availability.UnlockedAt.LevelIndex,
                "The combo needs both tags at once, so it unlocks with the later of the two.");
            Assert.AreEqual(1, map.Runs[0].NewlyEnabledCombos.Count);
        }

        // ---------------------------------------------------------------- element relevance

        [Test]
        public void Level_ResistingAnElementThePlayerHas_MattersForChoice()
        {
            var iceShard = Magic("IceShard", DamageType.Ice, MagicTag.Ice);
            var enemy = Enemy("Eye", new[] { iceShard }, new[] { DamageType.Ice }, new[] { 50f });

            var run = Make<RunDefinitionSO>("Run");
            run.Levels = new List<RunLevelEntry>
            {
                new RunLevelEntry { LevelTemplate = Template(Room(enemy)), LevelName = "L1" }
            };

            var map = Build(new List<RunDefinitionSO> { run },
                new List<MagicSO> { iceShard }, new List<MagicComboSO>());

            var level = map.Runs[0].Levels[0];
            Assert.IsTrue(level.ElementChoiceMatters);
            Assert.AreEqual(1f, level.ResistanceCoverage, 0.0001f);
            Assert.Contains(DamageType.Ice, level.ElementsAvailable);
        }

        [Test]
        public void Level_ResistingAnElementThePlayerCannotBring_DoesNotMatter()
        {
            var iceShard = Magic("IceShard", DamageType.Ice, MagicTag.Ice);

            // Offers Ice, but resists Holy — which no reachable magic deals.
            var enemy = Enemy("Eye", new[] { iceShard }, new[] { DamageType.Holy }, new[] { 50f });

            var run = Make<RunDefinitionSO>("Run");
            run.Levels = new List<RunLevelEntry>
            {
                new RunLevelEntry { LevelTemplate = Template(Room(enemy)), LevelName = "L1" }
            };

            var map = Build(new List<RunDefinitionSO> { run },
                new List<MagicSO> { iceShard }, new List<MagicComboSO>());

            var level = map.Runs[0].Levels[0];
            Assert.IsFalse(level.ElementChoiceMatters,
                "A resistance in an element the player cannot deal cannot change any decision.");
            Assert.AreEqual(1f, level.ResistanceCoverage, 0.0001f);
        }

        [Test]
        public void Level_TracksWeaknessesSeparatelyFromResistances()
        {
            var fireball = Magic("Fireball", DamageType.Fire, MagicTag.Fire);
            var enemy = Enemy("Straw Man", new[] { fireball },
                new[] { DamageType.Fire, DamageType.Ice }, new[] { -50f, 50f });

            var run = Make<RunDefinitionSO>("Run");
            run.Levels = new List<RunLevelEntry>
            {
                new RunLevelEntry { LevelTemplate = Template(Room(enemy)), LevelName = "L1" }
            };

            var map = Build(new List<RunDefinitionSO> { run },
                new List<MagicSO> { fireball }, new List<MagicComboSO>());

            var level = map.Runs[0].Levels[0];
            Assert.AreEqual(1f, level.WeaknessCoverage, 0.0001f);
            Assert.AreEqual(1f, level.ResistanceCoverage, 0.0001f);
            Assert.IsTrue(level.WeakWeightByType.ContainsKey(DamageType.Fire));
            Assert.IsTrue(level.ResistWeightByType.ContainsKey(DamageType.Ice));
        }

        [Test]
        public void Level_ZeroPercentResistance_IsNotCountedAsCoverage()
        {
            var fireball = Magic("Fireball", DamageType.Fire, MagicTag.Fire);
            var enemy = Enemy("Imp", new[] { fireball }, new[] { DamageType.Fire }, new[] { 0f });

            var run = Make<RunDefinitionSO>("Run");
            run.Levels = new List<RunLevelEntry>
            {
                new RunLevelEntry { LevelTemplate = Template(Room(enemy)), LevelName = "L1" }
            };

            var map = Build(new List<RunDefinitionSO> { run },
                new List<MagicSO> { fireball }, new List<MagicComboSO>());

            var level = map.Runs[0].Levels[0];
            Assert.AreEqual(0f, level.ResistanceCoverage, 0.0001f,
                "A 0% entry is a placeholder, not a resistance.");
            Assert.IsFalse(level.ElementChoiceMatters);
        }
    }
}
