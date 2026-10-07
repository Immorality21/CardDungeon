using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Events;
using Assets.Scripts.Items;
using Assets.Scripts.UnitStats;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// Items that grow with use (<see cref="ItemGrowth"/>): generic key/value counters on the item's
    /// save entry, milestones that add bonuses once reached, and the game-wide event stream that feeds
    /// them without combat knowing.
    /// </summary>
    public class ItemGrowthTests
    {
        private ItemSO _sword;

        [SetUp]
        public void SetUp()
        {
            _sword = ScriptableObject.CreateInstance<ItemSO>();
            _sword.Key = "soul-sword";
            _sword.DisplayName = "Soul Sword";
            _sword.Category = ItemCategory.Equipment;
            _sword.Bonuses = new List<ItemBonus> { new ItemBonus { StatType = StatType.Strength, Value = 4 } };
            _sword.Milestones = new List<ItemMilestone>
            {
                new ItemMilestone
                {
                    Counter = ItemCounterKind.Kills,
                    Threshold = 50,
                    Bonuses = new List<ItemBonus> { new ItemBonus { StatType = StatType.Strength, Value = 3 } }
                }
            };
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_sword);
        }

        [Test]
        public void Counters_AreGenericKeyValuePairs_OnTheEntry()
        {
            var entry = new ItemSaveData { ItemKey = _sword.Key };

            ItemGrowth.Add(entry, "kills", 3);
            ItemGrowth.Add(entry, "kills", 2);
            ItemGrowth.Add(entry, "anything-else", 1);

            Assert.AreEqual(5, ItemGrowth.Get(entry, "kills"));
            Assert.AreEqual(5, ItemGrowth.Get(entry, ItemCounterKind.Kills));
            Assert.AreEqual(1, ItemGrowth.Get(entry, "anything-else"));
            Assert.AreEqual(0, ItemGrowth.Get(entry, "never-counted"));
        }

        [Test]
        public void Counters_SurviveTheSaveRoundTrip()
        {
            var entry = new ItemSaveData { ItemKey = _sword.Key };
            ItemGrowth.Add(entry, ItemGrowth.KeyOf(ItemCounterKind.Kills), 49);

            var back = JsonUtility.FromJson<ItemSaveData>(JsonUtility.ToJson(entry));

            Assert.AreEqual(49, ItemGrowth.Get(back, ItemCounterKind.Kills));
        }

        [Test]
        public void AMilestone_AddsItsBonuses_OnlyOnceReached()
        {
            var entry = new ItemSaveData { ItemKey = _sword.Key };
            ItemGrowth.Add(entry, "kills", 49);

            float Strength() => InventoryOperations.SumBonuses(
                new[] { ItemGrowth.BonusesOf(_sword, entry) }, BonusType.Raw)[StatType.Strength];

            Assert.AreEqual(4f, Strength(), "49 kills: the sword's own bonus only");
            ItemGrowth.Add(entry, "kills", 1);
            Assert.AreEqual(7f, Strength(), "the 50th kill adds the milestone");
        }

        [Test]
        public void FreshGear_TheBalanceModelsView_HasNoMilestones()
        {
            var raw = InventoryOperations.ComputeBonuses(new[] { _sword }, BonusType.Raw);

            Assert.AreEqual(4f, raw[StatType.Strength]);
        }

        [Test]
        public void Counts_OnlyWhatAMilestoneReads()
        {
            Assert.IsTrue(ItemGrowth.Counts(_sword, ItemCounterKind.Kills));
            Assert.IsFalse(ItemGrowth.Counts(_sword, ItemCounterKind.Victories));
        }

        [Test]
        public void MilestoneLines_ShowProgress_ThenATick()
        {
            var entry = new ItemSaveData { ItemKey = _sword.Key };
            ItemGrowth.Add(entry, "kills", 12);

            Assert.AreEqual("50 kills: +3 STR", ItemPresenter.MilestoneLines(_sword).Single());
            Assert.AreEqual("50 kills: +3 STR (12/50)", ItemPresenter.MilestoneLines(_sword, entry).Single());

            ItemGrowth.Add(entry, "kills", 38);
            Assert.AreEqual("✓ 50 kills: +3 STR", ItemPresenter.MilestoneLines(_sword, entry).Single());
        }

        // ------------------------------------------------------------------ the game-wide stream

        [Test]
        public void GameEvents_DeliversToSubscribers_UntilTheyUnsubscribe()
        {
            // Subscribe/unsubscribe rather than Reset: the stream is global, and a reset would also drop
            // the live InventoryManager's listeners if the suite ever ran beside a session.
            var heard = new List<EnemyDefeated>();
            System.Action<EnemyDefeated> listener = heard.Add;
            GameEvents.Subscribe(listener);
            try
            {
                GameEvents.Publish(new EnemyDefeated { EnemyKey = "test-only-rat", KillerHeroKey = "Warrior" });
                Assert.AreEqual(1, heard.Count);
            }
            finally
            {
                GameEvents.Unsubscribe(listener);
            }

            GameEvents.Publish(new EnemyDefeated { EnemyKey = "test-only-rat" });
            Assert.AreEqual(1, heard.Count, "an unsubscribed listener hears nothing more");
        }
    }
}
