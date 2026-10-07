using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Balance;
using Assets.Scripts.Events;
using Assets.Scripts.Items;
using Assets.Scripts.Rooms;
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

        // ------------------------------------------------------------------ what the hub shows

        private static Stats TenStrength()
        {
            return new Stats(new StatBlock(new UnitStat(StatType.MaxHealth, 30), new UnitStat(StatType.Strength, 10)));
        }

        private GearPiece GrownSword()
        {
            var entry = new ItemSaveData { ItemKey = _sword.Key };
            ItemGrowth.Add(entry, ItemGrowth.KeyOf(ItemCounterKind.Kills), 50);
            return new GearPiece(_sword, entry);
        }

        [Test]
        public void WithGear_AGrownCopy_ShowsWhatItFightsWith()
        {
            var stats = HeroStatCalculator.WithGear(TenStrength(), new[] { GrownSword() });

            Assert.AreEqual(17, stats[StatType.Strength], "10 base + the sword's 4 + the 50-kill milestone's 3");
        }

        [Test]
        public void WithGear_AFreshCopy_MatchesTheBalanceModelsView()
        {
            var fresh = HeroStatCalculator.WithGear(TenStrength(), new[] { new GearPiece(_sword, null) });
            var model = HeroStatCalculator.WithGear(TenStrength(), new[] { _sword });

            Assert.AreEqual(model[StatType.Strength], fresh[StatType.Strength]);
            Assert.AreEqual(14, fresh[StatType.Strength]);
        }

        [Test]
        public void SwapIn_KeepsTheGrowthOfWhatStaysOn_AndBringsTheCandidates()
        {
            _sword.SlotType = SlotType.MainHand;
            var helm = ScriptableObject.CreateInstance<ItemSO>();
            try
            {
                helm.Key = "helm";
                helm.SlotType = SlotType.Head;
                var worn = new List<GearPiece> { GrownSword() };

                var withHelm = ItemPresenter.SwapIn(worn, new GearPiece(helm, null));
                var freshSword = ItemPresenter.SwapIn(worn, new GearPiece(_sword, null));

                Assert.AreEqual(17, HeroStatCalculator.WithGear(TenStrength(), withHelm)[StatType.Strength],
                    "the grown sword stays on beside the helm");
                Assert.AreEqual(14, HeroStatCalculator.WithGear(TenStrength(), freshSword)[StatType.Strength],
                    "a fresh copy replaces the grown one in its slot");
                Assert.AreEqual(10, HeroStatCalculator.WithGear(TenStrength(),
                    ItemPresenter.SwapOut(worn, SlotType.MainHand))[StatType.Strength]);
                Assert.AreEqual(1, worn.Count, "the input list is not modified");
            }
            finally
            {
                Object.DestroyImmediate(helm);
            }
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
