using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Dungeon;
using Assets.Scripts.Heroes;
using Assets.Scripts.Items;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// Class items (<see cref="ItemSO.UsableBy"/>, 2026-11-14): gear only some heroes can wear, and the
    /// class weapon every hero can win as a rare boss drop (<see cref="RunLevelEntry.BossDrops"/>).
    /// </summary>
    public class ClassItemTests
    {
        private static HeroSO Hero(string key)
        {
            var hero = ScriptableObject.CreateInstance<HeroSO>();
            hero.name = key;
            hero.Key = key;
            return hero;
        }

        private static List<T> LoadAll<T>() where T : Object
        {
            return AssetDatabase.FindAssets("t:" + typeof(T).Name)
                .Select(g => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(a => a != null)
                .ToList();
        }

        [Test]
        public void CanBeWornBy_EmptyList_IsAnyone()
        {
            var item = ScriptableObject.CreateInstance<ItemSO>();

            Assert.IsTrue(item.UsableByAnyone);
            Assert.IsTrue(item.CanBeWornBy("Warrior"));
        }

        [Test]
        public void CanBeWornBy_NamedHero_OnlyThem()
        {
            var mage = Hero("Mage");
            var item = ScriptableObject.CreateInstance<ItemSO>();
            item.UsableBy = new List<HeroSO> { mage };

            Assert.IsFalse(item.UsableByAnyone);
            Assert.IsTrue(item.CanBeWornBy(mage.SaveKey));
            Assert.IsFalse(item.CanBeWornBy("Warrior"));
        }

        [Test]
        public void CanBeWornBy_OnlyNullEntries_IsAnyone()
        {
            var item = ScriptableObject.CreateInstance<ItemSO>();
            item.UsableBy = new List<HeroSO> { null };

            Assert.IsTrue(item.CanBeWornBy("Warrior"), "An empty row is an authoring slip, not a lock on everyone.");
        }

        [Test]
        public void Craft_ResultTheWearerCannotUse_GoesToTheBags()
        {
            var mage = Hero("Mage");
            var stick = ScriptableObject.CreateInstance<ItemSO>();
            stick.Key = "Stick";
            stick.SlotType = SlotType.MainHand;
            var staff = ScriptableObject.CreateInstance<ItemSO>();
            staff.Key = "MagesStaff";
            staff.SlotType = SlotType.MainHand;
            staff.UsableBy = new List<HeroSO> { mage };
            var recipe = ScriptableObject.CreateInstance<ItemSO>();
            recipe.Key = "RecipeMagesStaff";
            recipe.Category = ItemCategory.Recipe;
            recipe.CraftBase = stick;
            recipe.CraftResult = staff;
            var lookup = new Dictionary<string, ItemSO> { { "Stick", stick }, { "MagesStaff", staff }, { "RecipeMagesStaff", recipe } };

            var items = new List<ItemSaveData>
            {
                new ItemSaveData { ItemKey = "RecipeMagesStaff", Quantity = 1 },
                new ItemSaveData { ItemKey = "Stick", Quantity = 1, EquippedSlot = "MainHand", EquippedHeroKey = "Warrior" }
            };

            Assert.IsTrue(CraftingOps.Craft(items, recipe, k => lookup.TryGetValue(k, out var so) ? so : null, out _, out var made));
            Assert.IsTrue(string.IsNullOrEmpty(made.EquippedHeroKey), "The Warrior cannot hold the Mage's staff.");
        }

        private static List<RunLevelEntry> AllFloors()
        {
            return LoadAll<RunDefinitionSO>().SelectMany(r => r.Levels).Where(f => f != null).ToList();
        }

        /// <summary>Every hero on the roster has a main-hand weapon of their own off some floor's boss.</summary>
        [Test]
        public void EveryHero_HasAClassWeapon_OffAFloorBoss()
        {
            var dropped = new HashSet<ItemSO>(AllFloors()
                .SelectMany(f => f.BossDrops ?? new List<LootDrop>())
                .Where(d => d != null && d.Item != null)
                .Select(d => d.Item));
            var missing = new List<string>();
            foreach (var hero in LoadAll<HeroSO>())
            {
                bool has = dropped.Any(i => i.Category == ItemCategory.Equipment && i.SlotType == SlotType.MainHand
                    && !i.UsableByAnyone && i.CanBeWornBy(hero.SaveKey));
                if (!has)
                {
                    missing.Add(hero.SaveKey);
                }
            }
            CollectionAssert.IsEmpty(missing, "Hero(es) with no class weapon off a floor boss: " + string.Join(", ", missing));
        }

        /// <summary>A rare boss drop on a level that cannot be replayed is one roll, ever: the boss kill
        /// ends the level. So boss drops live on repeatable levels only.</summary>
        [Test]
        public void BossDrops_AreOnRepeatableLevels()
        {
            foreach (var run in LoadAll<RunDefinitionSO>())
            {
                bool drops = run.Levels.Any(f => f?.BossDrops != null && f.BossDrops.Count > 0);
                Assert.IsTrue(!drops || run.Repeatable, run.name + " has boss drops but cannot be replayed.");
            }
        }

        /// <summary>A floor's boss drop needs a boss, is drop-only gear (or the shop sells it too), and is
        /// rare: never a sure thing (owner, 2026-11-14).</summary>
        [Test]
        public void BossDrops_AreRareDropOnlyGear_OnABossFloor()
        {
            foreach (var floor in AllFloors())
            {
                foreach (var drop in floor.BossDrops ?? new List<LootDrop>())
                {
                    string where = floor.LevelTemplate != null ? floor.LevelTemplate.name : "a floor";
                    Assert.IsNotNull(floor.BossEnemy, where + " has boss drops but no boss.");
                    Assert.IsNotNull(drop?.Item, where + " has an empty boss drop row.");
                    Assert.AreEqual(ItemCategory.Equipment, drop.Item.Category, where + ": " + drop.Item.name);
                    Assert.IsTrue(drop.Item.IsExclusive, where + ": " + drop.Item.name + " should be drop-only.");
                    Assert.That(drop.Chance, Is.GreaterThan(0f).And.LessThanOrEqualTo(0.25f), where + ": " + drop.Item.name + " should be a rare drop.");
                }
            }
        }
    }
}
