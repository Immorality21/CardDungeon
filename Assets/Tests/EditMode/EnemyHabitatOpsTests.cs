using System.Collections.Generic;
using Assets.Scripts.Dungeon;
using Assets.Scripts.Enemies;
using Assets.Scripts.Rooms;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// Where each monster is found (<see cref="EnemyHabitatOps"/>, backlog 16a): its levels and floors,
    /// the boss floor marked, and a level the player has not reached counted but never named.
    /// </summary>
    public class EnemyHabitatOpsTests
    {
        private EnemySO _lich;
        private RunDefinitionSO _catacombs;
        private RunDefinitionSO _barrow;

        private static LevelDefinitionSO TemplateWith(EnemySO enemy)
        {
            var room = ScriptableObject.CreateInstance<RoomSO>();
            room.EnemySpawnTable = new List<EnemySpawnEntry> { new EnemySpawnEntry { Enemy = enemy, SpawnChance = 1f } };
            var template = ScriptableObject.CreateInstance<LevelDefinitionSO>();
            template.RoomPool = new List<RoomSO> { room };
            return template;
        }

        [SetUp]
        public void SetUp()
        {
            _lich = ScriptableObject.CreateInstance<EnemySO>();
            _lich.name = "Lich";
            var empty = ScriptableObject.CreateInstance<LevelDefinitionSO>();
            empty.RoomPool = new List<RoomSO>();

            _catacombs = ScriptableObject.CreateInstance<RunDefinitionSO>();
            _catacombs.DisplayName = "The Catacombs";
            _catacombs.Levels = new List<RunLevelEntry>
            {
                new RunLevelEntry { LevelTemplate = TemplateWith(_lich), LevelName = "The Ossuary Stair" },
                new RunLevelEntry { LevelTemplate = empty, LevelName = "The Bone Galleries" },
                new RunLevelEntry { LevelTemplate = empty, LevelName = "The Lich's Sepulchre", BossEnemy = _lich }
            };

            _barrow = ScriptableObject.CreateInstance<RunDefinitionSO>();
            _barrow.DisplayName = "The Barrow Throne";
            _barrow.Levels = new List<RunLevelEntry> { new RunLevelEntry { LevelTemplate = TemplateWith(_lich), LevelName = "The Throne" } };
        }

        [Test]
        public void Find_ListsEachLevelWithItsFloors()
        {
            var found = EnemyHabitatOps.Find(_lich, new[] { _catacombs, _barrow });

            Assert.AreEqual(2, found.Count);
            Assert.AreSame(_catacombs, found[0].Run);
            Assert.AreEqual(2, found[0].Floors.Count, "The stair spawns it and the sepulchre is its throne; the galleries have none.");
            Assert.AreSame(_barrow, found[1].Run);
        }

        [Test]
        public void Lines_ReachedLevel_NamesFloors_AndMarksTheBoss()
        {
            var lines = EnemyHabitatOps.Lines(_lich, EnemyHabitatOps.Find(_lich, new[] { _catacombs }), r => true);

            Assert.AreEqual(1, lines.Count);
            Assert.AreEqual("The Catacombs", lines[0].Label);
            StringAssert.Contains("The Ossuary Stair", lines[0].Value);
            StringAssert.Contains(ImmoralityGaming.Localization.Loc.Format("bestiary.habitat.boss-floor", ("floor", "The Lich's Sepulchre")), lines[0].Value);
        }

        [Test]
        public void Lines_UnreachedLevel_IsCountedNotNamed()
        {
            var lines = EnemyHabitatOps.Lines(_lich, EnemyHabitatOps.Find(_lich, new[] { _catacombs, _barrow }), r => r == _catacombs);

            Assert.AreEqual(2, lines.Count);
            Assert.AreEqual(BestiaryTone.Unknown, lines[1].Tone);
            StringAssert.DoesNotContain("Barrow", lines[1].Label + lines[1].Value);
        }

        [Test]
        public void Lines_EnemyMetNowhere_IsEmpty()
        {
            var stranger = ScriptableObject.CreateInstance<EnemySO>();

            Assert.IsEmpty(EnemyHabitatOps.Lines(stranger, EnemyHabitatOps.Find(stranger, new[] { _catacombs }), r => true));
        }
    }
}
