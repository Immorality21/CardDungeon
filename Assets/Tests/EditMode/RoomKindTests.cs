using System.Collections.Generic;
using Assets.Scripts.Items;
using Assets.Scripts.Rooms;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// Room kinds: which rooms become non-combat, and what they pay out. Both halves are pure so the
    /// dungeon, the balance model and these tests agree on how many fights a level actually has —
    /// the number that decides whether a level is clearable.
    /// </summary>
    public class RoomKindTests
    {
        private static List<int> Rooms(int count)
        {
            var list = new List<int>();
            for (int i = 0; i < count; i++)
            {
                list.Add(i);
            }
            return list;
        }

        /// <summary>Always takes the first of the pool, so a plan is deterministic under test.</summary>
        private static int First(int count)
        {
            return 0;
        }

        // ---------------------------------------------------------------- the planner

        [Test]
        public void Plan_NoQuotas_PromotesNothing()
        {
            var plan = RoomKindPlanner.Plan(Rooms(5), 0, 0, First);

            CollectionAssert.IsEmpty(plan);
        }

        [Test]
        public void Plan_PromotesExactlyTheQuota()
        {
            var plan = RoomKindPlanner.Plan(Rooms(6), 1, 1, First);

            Assert.AreEqual(2, plan.Count);
            CollectionAssert.Contains(plan.Values, RoomKind.Treasure);
            CollectionAssert.Contains(plan.Values, RoomKind.Rest);
        }

        [Test]
        public void Plan_NeverPromotesOneRoomTwice()
        {
            // Every draw returns index 0 of the remaining pool, so a planner that failed to remove a
            // promoted room would hand back one room with two kinds - and a room with two buttons.
            var plan = RoomKindPlanner.Plan(Rooms(4), 2, 2, First);

            Assert.AreEqual(4, plan.Count);
            CollectionAssert.AllItemsAreUnique(new List<int>(plan.Keys));
        }

        [Test]
        public void Plan_QuotaBeyondEligibleRooms_LosesRefugesFirst()
        {
            // A level whose quotas exceed what it can hold has to degrade predictably rather than by
            // whichever kind the RNG reached last.
            var plan = RoomKindPlanner.Plan(Rooms(2), 2, 2, First);

            Assert.AreEqual(2, plan.Count);
            foreach (var kind in plan.Values)
            {
                Assert.AreEqual(RoomKind.Treasure, kind);
            }
        }

        [Test]
        public void Plan_OnlyEverPromotesEligibleRooms()
        {
            // The caller filters out the start room, the exit and connectors; the planner must not
            // reach past that list.
            var eligible = new List<int> { 3, 7 };
            var plan = RoomKindPlanner.Plan(eligible, 1, 1, count => count - 1);

            foreach (var index in plan.Keys)
            {
                CollectionAssert.Contains(eligible, index);
            }
        }

        [Test]
        public void Plan_EmptyEligibleList_IsSafe()
        {
            CollectionAssert.IsEmpty(RoomKindPlanner.Plan(new List<int>(), 2, 2, First));
            CollectionAssert.IsEmpty(RoomKindPlanner.Plan(null, 2, 2, First));
        }

        [Test]
        public void Plan_RollOutsideThePool_ClampsRatherThanMisplacing()
        {
            // Defensive, because this runs mid-generation: a bad roll must not put the kind in a room
            // that was never eligible.
            var eligible = new List<int> { 2, 5 };
            var plan = RoomKindPlanner.Plan(eligible, 1, 0, count => count + 99);

            Assert.AreEqual(1, plan.Count);
            foreach (var index in plan.Keys)
            {
                CollectionAssert.Contains(eligible, index);
            }
        }

        // ---------------------------------------------------------------- kind behaviour

        [Test]
        public void OnlyCombatRoomsHoldEnemies()
        {
            Assert.IsTrue(RoomKind.Combat.HoldsEnemies());
            Assert.IsFalse(RoomKind.Connector.HoldsEnemies());
            Assert.IsFalse(RoomKind.Treasure.HoldsEnemies());
            Assert.IsFalse(RoomKind.Rest.HoldsEnemies());
        }

        [Test]
        public void OnlyTreasureAndRestCarryAPayload()
        {
            Assert.IsTrue(RoomKind.Treasure.HasPayload());
            Assert.IsTrue(RoomKind.Rest.HasPayload());
            Assert.IsFalse(RoomKind.Combat.HasPayload());
            Assert.IsFalse(RoomKind.Connector.HasPayload());
        }

        [Test]
        public void APayloadRoomTakesNoOtherSpecials()
        {
            // A room offers one thing, so its button means one thing: no captive and no stat-check
            // event share a cache or a refuge.
            Assert.IsFalse(RoomKind.Treasure.AcceptsOtherSpecials());
            Assert.IsFalse(RoomKind.Rest.AcceptsOtherSpecials());
            Assert.IsTrue(RoomKind.Combat.AcceptsOtherSpecials());
        }

        // ---------------------------------------------------------------- rewards

        [Test]
        public void RestHealAmount_IsAShareOfTheBar_RoundedDown()
        {
            // 35% of 13 is 4.55.
            Assert.AreEqual(4, RoomKindRewards.RestHealAmount(13));
            Assert.AreEqual(35, RoomKindRewards.RestHealAmount(100));
        }

        [Test]
        public void RestHealAmount_TinyBar_StillHealsSomething()
        {
            Assert.AreEqual(1, RoomKindRewards.RestHealAmount(2));
            Assert.AreEqual(0, RoomKindRewards.RestHealAmount(0));
        }

        [Test]
        public void TreasureGold_GrowsWithDepth()
        {
            Assert.AreEqual(RoomKindRewards.TreasureGoldBase, RoomKindRewards.TreasureGold(0));
            Assert.AreEqual(
                RoomKindRewards.TreasureGoldBase + RoomKindRewards.TreasureGoldPerDepth * 3,
                RoomKindRewards.TreasureGold(3));
        }

        [Test]
        public void TreasureGold_NegativeIndexIsTreatedAsTheFirstFloor()
        {
            // Free play runs with RunLevelIndex -1.
            Assert.AreEqual(RoomKindRewards.TreasureGoldBase, RoomKindRewards.TreasureGold(-1));
        }

        [Test]
        public void PickTreasureItem_TakesTheFirstItemThatPassesItsRoll()
        {
            var cheap = Item("Cheap", ItemRarity.Common, itemLevel: 1);
            var dear = Item("Dear", ItemRarity.Legendary, itemLevel: 9);

            // A roll of 0 passes anything, so the first candidate wins - one item, not the whole list.
            var picked = RoomKindRewards.PickTreasureItem(
                new List<ItemSO> { dear, cheap }, 0, () => 0f);

            Assert.AreEqual(dear, picked);
        }

        [Test]
        public void PickTreasureItem_SkipsWhatFailsItsRoll()
        {
            var overLevel = Item("Deep", ItemRarity.Legendary, itemLevel: 9);
            var common = Item("Rag", ItemRarity.Common, itemLevel: 1);

            // 0.4 is inside a common's 0.60 chance and far outside a depth-suppressed legendary's.
            var picked = RoomKindRewards.PickTreasureItem(
                new List<ItemSO> { overLevel, common }, 0, () => 0.4f);

            Assert.AreEqual(common, picked);
        }

        [Test]
        public void PickTreasureItem_NothingPasses_ReturnsNull()
        {
            var item = Item("Rag", ItemRarity.Common, itemLevel: 1);

            Assert.IsNull(RoomKindRewards.PickTreasureItem(new List<ItemSO> { item }, 0, () => 0.99f));
            Assert.IsNull(RoomKindRewards.PickTreasureItem(null, 0, () => 0f));
        }

        [Test]
        public void ExpectedRestHealing_MatchesWhatTheRoomActuallyGives()
        {
            // The model's sustain credit and the game's heal have to be the same number, or the curve
            // measures a level nobody plays.
            int partyPool = 60;
            Assert.AreEqual(21, RoomKindRewards.ExpectedRestHealing(1, partyPool));
            Assert.AreEqual(42, RoomKindRewards.ExpectedRestHealing(2, partyPool));
            Assert.AreEqual(0, RoomKindRewards.ExpectedRestHealing(0, partyPool));
        }

        private static ItemSO Item(string key, ItemRarity rarity, int itemLevel)
        {
            var item = ScriptableObject.CreateInstance<ItemSO>();
            item.Key = key;
            item.DisplayName = key;
            item.Rarity = rarity;
            item.ItemLevel = itemLevel;
            return item;
        }
    }
}
