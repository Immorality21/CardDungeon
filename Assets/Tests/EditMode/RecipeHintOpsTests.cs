using System.Collections.Generic;
using Assets.Scripts.Dungeon;
using Assets.Scripts.Enemies;
using Assets.Scripts.Items;
using Assets.Scripts.Rooms;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// The Hall of Knowledge's hint on an unfound recipe (<see cref="RecipeHintOps"/>): where it comes
    /// from, worded so it never names an enemy the player has not met or a level they have not reached.
    /// </summary>
    public class RecipeHintOpsTests
    {
        private ItemSO _recipe;
        private EnemySO _lich;
        private RunDefinitionSO _catacombs;
        private RunDefinitionSO _cistern;

        [SetUp]
        public void SetUp()
        {
            _recipe = ScriptableObject.CreateInstance<ItemSO>();
            _recipe.Category = ItemCategory.Recipe;

            _lich = ScriptableObject.CreateInstance<EnemySO>();
            _lich.name = "Lich";
            _lich.DisplayName = "Lich";
            _lich.LootTable = new List<LootDrop> { new LootDrop { Item = _recipe, Chance = 0.25f } };

            var room = ScriptableObject.CreateInstance<RoomSO>();
            room.EnemySpawnTable = new List<EnemySpawnEntry> { new EnemySpawnEntry { Enemy = _lich, SpawnChance = 1f } };
            var crypt = ScriptableObject.CreateInstance<LevelDefinitionSO>();
            crypt.RoomPool = new List<RoomSO> { room };
            _catacombs = ScriptableObject.CreateInstance<RunDefinitionSO>();
            _catacombs.Key = "Catacombs";
            _catacombs.DisplayName = "The Catacombs";
            _catacombs.Levels = new List<RunLevelEntry> { new RunLevelEntry { LevelTemplate = crypt, LevelName = "The Ossuary" } };

            var sump = ScriptableObject.CreateInstance<LevelDefinitionSO>();
            sump.RoomPool = new List<RoomSO>();
            sump.RecipeTable = new List<LootDrop> { new LootDrop { Item = _recipe, Chance = 0.4f } };
            _cistern = ScriptableObject.CreateInstance<RunDefinitionSO>();
            _cistern.Key = "SeepingCistern";
            _cistern.DisplayName = "The Seeping Cistern";
            _cistern.Levels = new List<RunLevelEntry>
            {
                new RunLevelEntry { LevelTemplate = ScriptableObject.CreateInstance<LevelDefinitionSO>(), LevelName = "The Inflow" },
                new RunLevelEntry { LevelTemplate = sump, LevelName = "The Sump" }
            };
        }

        private List<RecipeSource> Sources()
        {
            return RecipeHintOps.Find(_recipe, new[] { _catacombs, _cistern }, new[] { _lich });
        }

        [Test]
        public void Find_ListsTheEnemyWhereItFights_ThenTheFloorClear()
        {
            var sources = Sources();

            Assert.AreEqual(2, sources.Count);
            Assert.AreEqual(RecipeSourceKind.EnemyDrop, sources[0].Kind);
            Assert.AreSame(_lich, sources[0].Enemy);
            Assert.AreSame(_catacombs, sources[0].Run);
            Assert.AreEqual(RecipeSourceKind.FloorClear, sources[1].Kind);
            Assert.AreSame(_cistern, sources[1].Run);
            Assert.AreSame(_cistern.Levels[1], sources[1].Floor);
        }

        [Test]
        public void Find_EnemyNoFloorSpawns_HasNoLevel()
        {
            var sources = RecipeHintOps.Find(_recipe, new RunDefinitionSO[0], new[] { _lich });

            Assert.AreEqual(1, sources.Count);
            Assert.IsNull(sources[0].Run);
        }

        [Test]
        public void Lines_KnownEnemyAndLevel_AreNamed()
        {
            var lines = RecipeHintOps.Lines(Sources(), e => true, r => true);

            Assert.AreEqual(2, lines.Count);
            StringAssert.Contains("Lich", lines[0]);
            StringAssert.Contains("The Sump", lines[1]);
        }

        [Test]
        public void Lines_UnmetEnemyAndUnreachedLevel_AreNotNamed()
        {
            var lines = RecipeHintOps.Lines(Sources(), e => false, r => false);

            Assert.AreEqual(2, lines.Count);
            StringAssert.DoesNotContain("Lich", lines[0]);
            StringAssert.DoesNotContain("The Sump", lines[1]);
            StringAssert.Contains(ImmoralityGaming.Localization.Loc.Format("bestiary.recipe.hint.floor-number", ("number", 2)), lines[1],
                "An unreached floor is named by its number.");
        }

        [Test]
        public void Lines_OneEnemyInManyLevels_IsOneLineNamingTwo()
        {
            var sources = new List<RecipeSource>();
            for (int i = 0; i < 6; i++)
            {
                var run = ScriptableObject.CreateInstance<RunDefinitionSO>();
                run.Key = "Run" + i;
                run.DisplayName = "Level " + i;
                sources.Add(new RecipeSource { Kind = RecipeSourceKind.EnemyDrop, Enemy = _lich, Run = run });
            }

            var lines = RecipeHintOps.Lines(sources, e => true, r => true, maxLines: 4);

            Assert.AreEqual(1, lines.Count, "One enemy in six levels is one line.");
            StringAssert.Contains("Level 0", lines[0]);
            StringAssert.Contains("Level 1", lines[0]);
            StringAssert.DoesNotContain("Level 2", lines[0]);
        }

        [Test]
        public void Find_QuestReward_IsASource()
        {
            var quest = ScriptableObject.CreateInstance<Assets.Scripts.Hub.Quests.QuestSO>();
            quest.RewardItem = _recipe;

            var sources = RecipeHintOps.Find(_recipe, new RunDefinitionSO[0], new EnemySO[0], new[] { quest });

            Assert.AreEqual(1, sources.Count);
            Assert.AreEqual(RecipeSourceKind.Quest, sources[0].Kind);
            Assert.AreEqual(1, RecipeHintOps.Lines(sources, e => true, r => true).Count);
        }

        [Test]
        public void Lines_UnreachedLevel_ReadsAfterTheKnownOnes()
        {
            var lines = RecipeHintOps.Lines(new List<RecipeSource>
            {
                new RecipeSource { Kind = RecipeSourceKind.EnemyDrop, Enemy = _lich, Run = _cistern },
                new RecipeSource { Kind = RecipeSourceKind.EnemyDrop, Enemy = _lich, Run = _catacombs }
            }, e => true, r => r == _catacombs);

            Assert.AreEqual(1, lines.Count);
            Assert.Less(lines[0].IndexOf("The Catacombs"), lines[0].IndexOf("not yet reached"));
        }

        [Test]
        public void Find_FloorWithoutRoomEvents_PaysNoEventRecipe()
        {
            var template = ScriptableObject.CreateInstance<LevelDefinitionSO>();
            template.EventRecipes = new List<ItemSO> { _recipe };
            var run = ScriptableObject.CreateInstance<RunDefinitionSO>();
            run.Levels = new List<RunLevelEntry> { new RunLevelEntry { LevelTemplate = template, AllowRoomEvents = false } };

            Assert.IsEmpty(RecipeHintOps.Find(_recipe, new[] { run }, new EnemySO[0]));

            run.Levels[0].AllowRoomEvents = true;
            Assert.AreEqual(RecipeSourceKind.RoomEvent, RecipeHintOps.Find(_recipe, new[] { run }, new EnemySO[0])[0].Kind);
        }

        [Test]
        public void Lines_NoSources_IsEmpty()
        {
            Assert.IsEmpty(RecipeHintOps.Lines(new List<RecipeSource>(), e => true, r => true));
        }
    }
}
