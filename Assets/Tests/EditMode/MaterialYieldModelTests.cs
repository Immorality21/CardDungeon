using System.Collections.Generic;
using Assets.Scripts.Balance;
using Assets.Scripts.Dungeon;
using Assets.Scripts.Enemies;
using Assets.Scripts.Items;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// Covers <see cref="MaterialYieldModel"/> — how much raw stuff a modelled floor hands over, and
    /// the two ways authored material content can fail to reach a player.
    ///
    /// <para>The point of this model is that it is measured <i>before</i> anything spends materials
    /// (<c>docs/plans/HUB.md</c> §7 phase 1): a building's price is only tunable against a counted
    /// tap, so the arithmetic has to be trustworthy first.</para>
    /// </summary>
    public class MaterialYieldModelTests
    {
        private readonly List<ScriptableObject> _created = new List<ScriptableObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var so in _created)
            {
                Object.DestroyImmediate(so);
            }
            _created.Clear();
        }

        private ItemSO Material(string key)
        {
            var so = ScriptableObject.CreateInstance<ItemSO>();
            so.Key = key;
            so.DisplayName = key;
            so.Category = ItemCategory.Material;
            so.MaxStack = 999;
            _created.Add(so);
            return so;
        }

        private EnemySO Enemy(string key, params LootDrop[] drops)
        {
            var so = ScriptableObject.CreateInstance<EnemySO>();
            so.Key = key;
            so.DisplayName = key;
            so.LootTable = new List<LootDrop>(drops);
            _created.Add(so);
            return so;
        }

        private LevelDefinitionSO Level(string key, int treasureRooms, params LootDrop[] materials)
        {
            var so = ScriptableObject.CreateInstance<LevelDefinitionSO>();
            so.Key = key;
            so.TreasureRooms = treasureRooms;
            so.MaterialTable = new List<LootDrop>(materials);
            _created.Add(so);
            return so;
        }

        private static LootDrop Drop(ItemSO item, float chance, int min, int max)
        {
            return new LootDrop { Item = item, Chance = chance, MinQuantity = min, MaxQuantity = max };
        }

        /// <summary>A one-room floor holding <paramref name="weight"/> of one enemy.</summary>
        private static LevelCurve Floor(EnemySO enemy, float weight, float occurrences,
            LevelDefinitionSO template = null)
        {
            var room = new RoomEncounter { Occurrences = occurrences };
            room.Expected.Add(enemy, weight);

            return new LevelCurve
            {
                Index = 0,
                Name = "Test",
                Template = template,
                TreasureRooms = template != null ? template.TreasureRooms : 0,
                Rooms = new List<RoomEncounter> { room }
            };
        }

        [Test]
        public void ForLevel_KillYield_IsRoomsTimesEnemiesTimesChanceTimesMeanQuantity()
        {
            var iron = Material("ScrapIron");
            var enemy = Enemy("Grunt", Drop(iron, 0.5f, 2, 4));

            // 3 rooms x 2 enemies x 0.5 chance x mean 3 = 9 units.
            var yields = MaterialYieldModel.ForLevel(Floor(enemy, weight: 2f, occurrences: 3f));

            Assert.AreEqual(1, yields.Count);
            Assert.AreEqual(9f, yields[0].FromKills, 0.001f);
            Assert.AreEqual(0f, yields[0].FromCaches);
        }

        [Test]
        public void ForLevel_CacheYield_IsCountedOncePerTreasureRoom()
        {
            var iron = Material("ScrapIron");
            var timber = Material("RottedTimber");
            var template = Level("Halls", treasureRooms: 2, Drop(timber, 0.5f, 1, 3));
            var enemy = Enemy("Grunt", Drop(iron, 1f, 1, 1));

            var yields = MaterialYieldModel.ForLevel(Floor(enemy, 1f, 1f, template));

            var wood = yields.Find(y => y.Key == "RottedTimber");
            Assert.IsNotNull(wood);
            Assert.AreEqual(2f, wood.FromCaches, 0.001f, "2 caches x 0.5 chance x mean 2");
            Assert.AreEqual(0f, wood.FromKills);
        }

        [Test]
        public void ForLevel_IgnoresNonMaterialDrops()
        {
            // Gear is priced by ShopPricing and modelled by GearLoadout; counting a sword as raw
            // stuff would inflate the tap a building is about to be priced against.
            var sword = ScriptableObject.CreateInstance<ItemSO>();
            sword.Key = "Sword";
            sword.Category = ItemCategory.Equipment;
            _created.Add(sword);

            var enemy = Enemy("Grunt", Drop(sword, 1f, 1, 1));

            CollectionAssert.IsEmpty(MaterialYieldModel.ForLevel(Floor(enemy, 1f, 1f)));
        }

        [Test]
        public void ForLevel_ATableOnALevelWithNoCache_YieldsNothing()
        {
            var timber = Material("RottedTimber");
            var template = Level("Halls", treasureRooms: 0, Drop(timber, 1f, 5, 5));
            var enemy = Enemy("Grunt");

            CollectionAssert.IsEmpty(MaterialYieldModel.ForLevel(Floor(enemy, 1f, 1f, template)),
                "a MaterialTable is only rolled when a cache is opened");
        }

        [Test]
        public void LevelsWithUnreachableMaterialTable_FindsTheSilentlyDeadTable()
        {
            var timber = Material("RottedTimber");
            var enemy = Enemy("Grunt");

            var live = Floor(enemy, 1f, 1f, Level("Live", 1, Drop(timber, 1f, 1, 1)));
            var dead = Floor(enemy, 1f, 1f, Level("Dead", 0, Drop(timber, 1f, 1, 1)));
            dead.Name = "Dead";

            var run = new RunCurve { Levels = new List<LevelCurve> { live, dead } };

            var found = MaterialYieldModel.LevelsWithUnreachableMaterialTable(run);
            Assert.AreEqual(1, found.Count);
            Assert.AreEqual("Dead", found[0].Name);
        }

        [Test]
        public void Unobtainable_NamesMaterialsNothingYields()
        {
            var dropped = Material("ScrapIron");
            var orphan = Material("Unobtainium");
            var enemy = Enemy("Grunt", Drop(dropped, 1f, 1, 1));
            var run = new RunCurve { Levels = new List<LevelCurve> { Floor(enemy, 1f, 1f) } };

            var missing = MaterialYieldModel.Unobtainable(
                new List<ItemSO> { dropped, orphan }, new List<RunCurve> { run });

            Assert.AreEqual(1, missing.Count);
            Assert.AreEqual("Unobtainium", missing[0].Key);
        }

        [Test]
        public void ForRun_SumsEveryFloor()
        {
            var iron = Material("ScrapIron");
            var enemy = Enemy("Grunt", Drop(iron, 1f, 1, 1));
            var run = new RunCurve
            {
                Levels = new List<LevelCurve> { Floor(enemy, 2f, 1f), Floor(enemy, 3f, 1f) }
            };

            var yields = MaterialYieldModel.ForRun(run);

            Assert.AreEqual(1, yields.Count);
            Assert.AreEqual(5f, yields[0].Total, 0.001f);
        }
    }
}
