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
    /// Tests for the unlock/elemental supply model. These pin the questions the Elements &amp; Unlocks
    /// tab answers: can this magic be obtained at all, what does the cheapest route to it cost, when
    /// does a combo become possible, and is a level's resistance in an element the party can bring yet.
    ///
    /// <para><b>The supply side changed on 2026-09-04.</b> Magic used to be drawn from enemies, so a
    /// source was an enemy in a level and reachability was a spawn-table question. Draw is gone: a
    /// source is a <c>MagicKnown</c> node on a hero's sphere grid, and reachability is an investment
    /// question. Every fixture below builds heroes with grids rather than enemies with draw lists.</para>
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

        private EnemySO Enemy(string name, DamageType[] resistTypes = null, float[] percents = null)
        {
            var enemy = Make<EnemySO>(name);
            enemy.DisplayName = name;
            enemy.BaseStats[StatType.Strength] = 4;
            enemy.BaseStats[StatType.Endurance] = 1;
            enemy.BaseStats[StatType.MaxHealth] = 20;
            enemy.BaseStats[StatType.Agility] = 5;
            enemy.XpReward = 10;
            enemy.GoldReward = 5;
            enemy.Spells = new List<EnemySpellEntry>();

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

        /// <summary>
        /// A level built from exactly these rooms. Generates <c>rooms.Length + 1</c>: the model takes
        /// the party's starting room off the total before spreading the rest across the pool (
        /// <c>EnemyManager.SpawnEnemies</c> skips the room the party spawns in), so asking for one
        /// room per pool entry would leave every entry expected zero times and every weight at 0.
        /// </summary>
        private LevelDefinitionSO Template(params RoomSO[] rooms)
        {
            var template = Make<LevelDefinitionSO>();
            template.RoomsToGenerate = rooms.Length + 1;
            template.RoomPool = new List<RoomSO>(rooms);
            return template;
        }

        /// <summary>
        /// A hero whose grid is a straight chain from the start node, with a <c>MagicKnown</c> node
        /// for each named magic in order. The chain is what makes <c>PathCost</c> interesting: the
        /// second spell can only be bought through the first.
        /// </summary>
        private HeroSO Hero(string label, params string[] magicKeys)
        {
            var grid = Make<SphereGridSO>(label + "Grid");
            grid.Nodes = new List<SphereGridNode>
            {
                new SphereGridNode
                {
                    Key = label + "-start",
                    Kind = SphereNodeKind.Stat,
                    XpCost = 15,
                    Gains = new StatBlock()
                }
            };
            grid.StartNodeKey = label + "-start";

            string previous = grid.StartNodeKey;
            for (int i = 0; i < magicKeys.Length; i++)
            {
                string key = $"{label}-magic-{i}";
                grid.Nodes.Add(new SphereGridNode
                {
                    Key = key,
                    Kind = SphereNodeKind.MagicKnown,
                    XpCost = 20 * (i + 1),
                    GrantedMagicKey = magicKeys[i],
                    GrantedCharges = 2,
                    Gains = new StatBlock(),
                    Neighbors = new List<string> { previous }
                });
                previous = key;
            }

            var hero = Make<HeroSO>(label);
            hero.Key = label;
            hero.Label = label;
            hero.BaseStats[StatType.Strength] = 12;
            hero.BaseStats[StatType.Endurance] = 5;
            hero.BaseStats[StatType.MaxHealth] = 200;
            hero.BaseStats[StatType.Agility] = 5;
            hero.SphereGrid = grid;
            return hero;
        }

        /// <summary>
        /// The party the run curves are measured against. <paramref name="xpPerHero"/> is greedy-spent
        /// on each grid, which is what decides which spells the modelled party actually holds.
        /// </summary>
        private PartyBaseline Party(List<HeroSO> heroes, int xpPerHero)
        {
            return PartyBaseline.Build(heroes, xpPerHero);
        }

        private ProgressionMap Build(
            List<RunDefinitionSO> runs,
            List<MagicSO> catalog,
            List<MagicComboSO> combos,
            List<HeroSO> heroes,
            int xpPerHero = 0)
        {
            var rules = Make<BalanceRulesSO>();
            var party = Party(heroes, xpPerHero);

            var curves = new List<RunCurve>();
            foreach (var run in runs)
            {
                curves.Add(RunCurve.Build(run, party, rules));
            }

            return ProgressionMap.Build(curves, catalog, combos, heroes);
        }

        private RunDefinitionSO OneLevelRun(string name, EnemySO enemy, int sequence = 0)
        {
            var run = Make<RunDefinitionSO>(name);
            run.SequenceIndex = sequence;
            run.Levels = new List<RunLevelEntry>
            {
                new RunLevelEntry { LevelTemplate = Template(Room(enemy)), LevelName = "L1" }
            };
            return run;
        }

        // ---------------------------------------------------------------- magic reachability

        [Test]
        public void Magic_OnAHerosGrid_IsReachable()
        {
            var fireball = Magic("Fireball", DamageType.Fire, MagicTag.Fire);
            var acolyte = Hero("Acolyte", "Fireball");

            var map = Build(
                new List<RunDefinitionSO> { OneLevelRun("Run", Enemy("Imp")) },
                new List<MagicSO> { fireball },
                new List<MagicComboSO>(),
                new List<HeroSO> { acolyte });

            Assert.AreEqual(1, map.ReachableMagicCount);
            Assert.IsTrue(map.Magic[0].IsReachable);
            Assert.AreEqual("Acolyte", map.Magic[0].FirstSource.HeroName);
            Assert.AreEqual("Acolyte-magic-0", map.Magic[0].FirstSource.NodeKey);
        }

        [Test]
        public void Magic_OnNoGrid_IsUnreachable()
        {
            // Stronger than it used to be: with Draw gone there is no second route, so this is dead
            // content rather than content one spawn-table edit away.
            var fireball = Magic("Fireball", DamageType.Fire, MagicTag.Fire);
            var orphan = Magic("Meteor", DamageType.Fire, MagicTag.Fire);
            var acolyte = Hero("Acolyte", "Fireball");

            var map = Build(
                new List<RunDefinitionSO> { OneLevelRun("Run", Enemy("Imp")) },
                new List<MagicSO> { fireball, orphan },
                new List<MagicComboSO>(),
                new List<HeroSO> { acolyte });

            Assert.AreEqual(1, map.ReachableMagicCount);
            Assert.AreEqual(2, map.CatalogMagicCount);
            Assert.AreEqual(0.5f, map.MagicCoverage, 0.0001f);

            var meteor = map.Magic.Find(m => m.Key == "Meteor");
            Assert.IsFalse(meteor.IsReachable);
            Assert.IsNull(meteor.FirstSource);
        }

        [Test]
        public void Magic_PathCostIsTheWholeChain_NotJustTheNode()
        {
            // What a spell costs is what a player pays to own the node, and nodes are priced by
            // depth - a deep spell's real price is dominated by the branch leading to it.
            var heal = Magic("Heal", null);
            var renew = Magic("Renew", null);
            var acolyte = Hero("Acolyte", "Heal", "Renew");   // start 15, Heal 20, Renew 40

            var map = Build(
                new List<RunDefinitionSO> { OneLevelRun("Run", Enemy("Imp")) },
                new List<MagicSO> { heal, renew },
                new List<MagicComboSO>(),
                new List<HeroSO> { acolyte });

            Assert.AreEqual(35, map.Magic.Find(m => m.Key == "Heal").FirstSource.PathCost);
            Assert.AreEqual(75, map.Magic.Find(m => m.Key == "Renew").FirstSource.PathCost,
                "Renew can only be bought through Heal, so its price includes it.");
        }

        [Test]
        public void Magic_TaughtByTwoHeroes_ReportsTheCheaperRouteAndIsNotSingleHero()
        {
            var ward = Magic("Ward", null);
            var warrior = Hero("Warrior", "Ward");             // start 15 + 20 = 35
            var tank = Hero("Tank", "Filler", "Ward");         // start 15 + 20 + 40 = 75

            var map = Build(
                new List<RunDefinitionSO> { OneLevelRun("Run", Enemy("Imp")) },
                new List<MagicSO> { ward, Magic("Filler", null) },
                new List<MagicComboSO>(),
                new List<HeroSO> { warrior, tank });

            var availability = map.Magic.Find(m => m.Key == "Ward");
            Assert.AreEqual(2, availability.Sources.Count);
            Assert.AreEqual("Warrior", availability.FirstSource.HeroName);
            Assert.AreEqual(35, availability.FirstSource.PathCost);
            Assert.IsFalse(availability.SingleHeroOnly);
        }

        [Test]
        public void Magic_TaughtByOneHeroOnly_IsFlaggedAsAPrecondition()
        {
            // Fielding that hero stops being a preference and becomes a requirement, which is a
            // different kind of gate from "it costs a lot".
            var doom = Magic("Doom", DamageType.Shadow, MagicTag.Dark);
            var cultist = Hero("Cultist", "Doom");

            var map = Build(
                new List<RunDefinitionSO> { OneLevelRun("Run", Enemy("Imp")) },
                new List<MagicSO> { doom },
                new List<MagicComboSO>(),
                new List<HeroSO> { cultist });

            Assert.IsTrue(map.Magic.Find(m => m.Key == "Doom").SingleHeroOnly);
        }

        [Test]
        public void Magic_NamingAKeyTheCatalogDoesNotHave_IsNotASourceOfAnything()
        {
            var fireball = Magic("Fireball", DamageType.Fire, MagicTag.Fire);
            var acolyte = Hero("Acolyte", "TypoedKey");

            var map = Build(
                new List<RunDefinitionSO> { OneLevelRun("Run", Enemy("Imp")) },
                new List<MagicSO> { fireball },
                new List<MagicComboSO>(),
                new List<HeroSO> { acolyte });

            Assert.IsFalse(map.Magic[0].IsReachable);
            Assert.AreEqual(0, map.ReachableMagicCount);
        }

        // ---------------------------------------------------------------- run ordering

        [Test]
        public void Runs_AreOrderedBySequenceIndex_AndUnlocksCountedOnce()
        {
            var early = Magic("IceShard", DamageType.Ice, MagicTag.Ice);
            var late = Magic("Bolt", DamageType.Lightning, MagicTag.Lightning);
            var acolyte = Hero("Acolyte", "IceShard", "Bolt");

            var second = OneLevelRun("Alpha", Enemy("Golem"), sequence: 1);   // alphabetically first
            var first = OneLevelRun("Zeta", Enemy("Eye"), sequence: 0);

            // Enough XP for the whole chain, so the modelled party ends up holding both.
            var map = Build(new List<RunDefinitionSO> { second, first },
                new List<MagicSO> { early, late }, new List<MagicComboSO>(),
                new List<HeroSO> { acolyte }, xpPerHero: 200);

            Assert.AreEqual("Zeta", map.Runs[0].Name, "SequenceIndex must win over asset name.");
            Assert.AreEqual("Alpha", map.Runs[1].Name);
            Assert.IsFalse(map.RunOrderIsImplicit);

            // Both are already owned when the first run starts, and a magic counts as new once.
            Assert.AreEqual(2, map.Runs[0].NewlyKnown.Count);
            Assert.AreEqual(0, map.Runs[1].NewlyKnown.Count);
        }

        [Test]
        public void Runs_WithNoSequenceIndex_ReportImplicitOrder()
        {
            var magic = Magic("Slash", DamageType.Normal, MagicTag.Physical);
            var warrior = Hero("Warrior", "Slash");

            var map = Build(
                new List<RunDefinitionSO> { OneLevelRun("A", Enemy("Rat")), OneLevelRun("B", Enemy("Rat")) },
                new List<MagicSO> { magic }, new List<MagicComboSO>(),
                new List<HeroSO> { warrior });

            Assert.IsTrue(map.RunOrderIsImplicit,
                "With every SequenceIndex at 0 and more than one run, the order is a guess and must say so.");
        }

        [Test]
        public void Level_NewlyKnown_FollowsWhatTheModelledPartyCanAfford()
        {
            // The honest half of the new model: a spell being on a grid is not the same as the party
            // holding it. A party with no XP holds nothing, however well authored the grid is.
            var heal = Magic("Heal", null);
            var acolyte = Hero("Acolyte", "Heal");

            var broke = Build(
                new List<RunDefinitionSO> { OneLevelRun("Run", Enemy("Imp")) },
                new List<MagicSO> { heal }, new List<MagicComboSO>(),
                new List<HeroSO> { acolyte }, xpPerHero: 0);

            Assert.IsTrue(broke.Magic[0].IsReachable, "the grid still teaches it");
            Assert.AreEqual(0, broke.Runs[0].NewlyKnown.Count, "but nobody has bought it");

            var funded = Build(
                new List<RunDefinitionSO> { OneLevelRun("Run", Enemy("Imp")) },
                new List<MagicSO> { heal }, new List<MagicComboSO>(),
                new List<HeroSO> { acolyte }, xpPerHero: 200);

            Assert.AreEqual(1, funded.Runs[0].NewlyKnown.Count);
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
            var acolyte = Hero("Acolyte", "Fireball");
            var combo = Combo("Ignite", MagicTag.Fire, MagicTag.Oil);   // nothing carries Oil

            var map = Build(
                new List<RunDefinitionSO> { OneLevelRun("Run", Enemy("Imp")) },
                new List<MagicSO> { fireball }, new List<MagicComboSO> { combo },
                new List<HeroSO> { acolyte }, xpPerHero: 200);

            var availability = map.Combos[0];
            Assert.IsFalse(availability.IsReachable);
            Assert.Contains(MagicTag.Oil, availability.TagsWithNoMagic);
            Assert.IsNull(availability.UnlockedAt);
            Assert.AreEqual(0, map.ReachableComboCount);
        }

        [Test]
        public void Combo_WithATagOnlyOnMagicNoGridTeaches_IsUnreachable()
        {
            var fireball = Magic("Fireball", DamageType.Fire, MagicTag.Fire);
            var oilSlick = Magic("OilSlick", null, MagicTag.Oil);   // in the catalog, on no grid
            var acolyte = Hero("Acolyte", "Fireball");
            var combo = Combo("Ignite", MagicTag.Fire, MagicTag.Oil);

            var map = Build(
                new List<RunDefinitionSO> { OneLevelRun("Run", Enemy("Imp")) },
                new List<MagicSO> { fireball, oilSlick }, new List<MagicComboSO> { combo },
                new List<HeroSO> { acolyte }, xpPerHero: 200);

            var availability = map.Combos[0];
            Assert.IsFalse(availability.IsReachable);
            Assert.IsEmpty(availability.TagsWithNoMagic);
            Assert.Contains(MagicTag.Oil, availability.TagsNotLearnable);
        }

        [Test]
        public void Combo_IsHeldFromTheLevelWhereTheModelledPartyOwnsEveryPiece()
        {
            var fireball = Magic("Fireball", DamageType.Fire, MagicTag.Fire);
            var oilSlick = Magic("OilSlick", null, MagicTag.Oil);
            var acolyte = Hero("Acolyte", "Fireball", "OilSlick");
            var combo = Combo("Ignite", MagicTag.Fire, MagicTag.Oil);

            var map = Build(
                new List<RunDefinitionSO> { OneLevelRun("Run", Enemy("Imp")) },
                new List<MagicSO> { fireball, oilSlick }, new List<MagicComboSO> { combo },
                new List<HeroSO> { acolyte }, xpPerHero: 200);

            var availability = map.Combos[0];
            Assert.IsTrue(availability.IsReachable);
            Assert.IsNotNull(availability.UnlockedAt);
            Assert.AreEqual(0, availability.UnlockedAt.LevelIndex);
            Assert.AreEqual(1, map.Runs[0].NewlyEnabledCombos.Count);
            Assert.Greater(availability.InvestmentToEnable, 0);
        }

        [Test]
        public void Combo_AffordableButNeverBoughtByTheModel_IsReachableWithNoUnlockPoint()
        {
            // Distinct from unreachable, and reported differently: every piece is on a grid, but the
            // campaign's XP never reaches them. GreedySpend is a breadth build, so this is a signal
            // about pricing rather than proof the combo is dead.
            var fireball = Magic("Fireball", DamageType.Fire, MagicTag.Fire);
            var oilSlick = Magic("OilSlick", null, MagicTag.Oil);
            var acolyte = Hero("Acolyte", "Fireball", "OilSlick");
            var combo = Combo("Ignite", MagicTag.Fire, MagicTag.Oil);

            var map = Build(
                new List<RunDefinitionSO> { OneLevelRun("Run", Enemy("Imp")) },
                new List<MagicSO> { fireball, oilSlick }, new List<MagicComboSO> { combo },
                new List<HeroSO> { acolyte }, xpPerHero: 0);

            var availability = map.Combos[0];
            Assert.IsTrue(availability.IsReachable, "both pieces are authored on a grid");
            Assert.IsNull(availability.UnlockedAt, "but no modelled party ever holds them");
        }

        // ---------------------------------------------------------------- element relevance

        [Test]
        public void Level_ResistingAnElementThePartyHas_MattersForChoice()
        {
            var iceShard = Magic("IceShard", DamageType.Ice, MagicTag.Ice);
            var acolyte = Hero("Acolyte", "IceShard");
            var enemy = Enemy("Eye", new[] { DamageType.Ice }, new[] { 50f });

            var map = Build(
                new List<RunDefinitionSO> { OneLevelRun("Run", enemy) },
                new List<MagicSO> { iceShard }, new List<MagicComboSO>(),
                new List<HeroSO> { acolyte }, xpPerHero: 200);

            var level = map.Runs[0].Levels[0];
            Assert.IsTrue(level.ElementChoiceMatters);
            Assert.AreEqual(1f, level.ResistanceCoverage, 0.0001f);
            Assert.Contains(DamageType.Ice, level.ElementsAvailable);
        }

        [Test]
        public void Level_ResistingAnElementThePartyCannotBring_DoesNotMatter()
        {
            var iceShard = Magic("IceShard", DamageType.Ice, MagicTag.Ice);
            var acolyte = Hero("Acolyte", "IceShard");

            // The party can deal Ice, but the level resists Holy, which nothing they own touches.
            var enemy = Enemy("Eye", new[] { DamageType.Holy }, new[] { 50f });

            var map = Build(
                new List<RunDefinitionSO> { OneLevelRun("Run", enemy) },
                new List<MagicSO> { iceShard }, new List<MagicComboSO>(),
                new List<HeroSO> { acolyte }, xpPerHero: 200);

            var level = map.Runs[0].Levels[0];
            Assert.IsFalse(level.ElementChoiceMatters,
                "A resistance in an element the player cannot deal cannot change any decision.");
            Assert.AreEqual(1f, level.ResistanceCoverage, 0.0001f);
        }

        [Test]
        public void Level_TracksWeaknessesSeparatelyFromResistances()
        {
            var fireball = Magic("Fireball", DamageType.Fire, MagicTag.Fire);
            var acolyte = Hero("Acolyte", "Fireball");
            var enemy = Enemy("Straw Man",
                new[] { DamageType.Fire, DamageType.Ice }, new[] { -50f, 50f });

            var map = Build(
                new List<RunDefinitionSO> { OneLevelRun("Run", enemy) },
                new List<MagicSO> { fireball }, new List<MagicComboSO>(),
                new List<HeroSO> { acolyte }, xpPerHero: 200);

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
            var acolyte = Hero("Acolyte", "Fireball");
            var enemy = Enemy("Imp", new[] { DamageType.Fire }, new[] { 0f });

            var map = Build(
                new List<RunDefinitionSO> { OneLevelRun("Run", enemy) },
                new List<MagicSO> { fireball }, new List<MagicComboSO>(),
                new List<HeroSO> { acolyte }, xpPerHero: 200);

            var level = map.Runs[0].Levels[0];
            Assert.AreEqual(0f, level.ResistanceCoverage, 0.0001f,
                "A 0% entry is a placeholder, not a resistance.");
            Assert.IsFalse(level.ElementChoiceMatters);
        }
    }
}
