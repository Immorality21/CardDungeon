using System.Collections.Generic;
using Assets.Scripts.Heroes;
using NUnit.Framework;

namespace Tests.EditMode
{
    /// <summary>
    /// The two halves of "party size is a decision": how a kill's XP divides across the party
    /// (<see cref="XpSplit"/>) and how a stored selection resolves against what is owned and what the
    /// cap allows (<see cref="HeroRoster.ResolveSelection"/>, the pure half — the disk-facing wrappers
    /// need a save file and are exercised in-editor).
    /// </summary>
    public class PartySelectionTests
    {
        // --- XP split ----------------------------------------------------------

        [Test]
        public void Split_SoloParty_GivesEverythingToTheOneHero()
        {
            var shares = XpSplit.Split(12, 1);

            Assert.AreEqual(1, shares.Length);
            Assert.AreEqual(12, shares[0]);
        }

        [Test]
        public void Split_EvenlyDivisible_GivesEveryHeroTheSameShare()
        {
            var shares = XpSplit.Split(12, 4);

            Assert.AreEqual(new[] { 3, 3, 3, 3 }, shares);
        }

        [Test]
        public void Split_WithRemainder_GivesTheRemainderToTheLeader()
        {
            // 7 across 4 is 1 each with 3 left over — the case that would silently lose XP.
            var shares = XpSplit.Split(7, 4);

            Assert.AreEqual(new[] { 4, 1, 1, 1 }, shares);
        }

        [Test]
        public void Split_AlwaysSumsToTheAwardedTotal()
        {
            for (int size = 1; size <= PartySlots.MaxCap; size++)
            {
                for (int total = 0; total < 40; total++)
                {
                    int sum = 0;
                    foreach (int share in XpSplit.Split(total, size))
                    {
                        sum += share;
                    }
                    Assert.AreEqual(total, sum, $"total {total} across {size}");
                }
            }
        }

        [Test]
        public void Split_NothingToAwardOrNobodyToPay_ReturnsEmpty()
        {
            Assert.IsEmpty(XpSplit.Split(0, 3));
            Assert.IsEmpty(XpSplit.Split(-5, 3));
            Assert.IsEmpty(XpSplit.Split(10, 0));
        }

        [Test]
        public void ExpectedShare_QuartersTheRunForAFullParty()
        {
            Assert.AreEqual(100f, XpSplit.ExpectedShare(400f, 4));
            Assert.AreEqual(400f, XpSplit.ExpectedShare(400f, 1));
            Assert.AreEqual(0f, XpSplit.ExpectedShare(400f, 0));
        }

        // --- Party slots -------------------------------------------------------

        [Test]
        public void CapForBonus_StartsAtBaseAndStopsAtMax()
        {
            Assert.AreEqual(PartySlots.BaseCap, PartySlots.CapForBonus(0));
            Assert.AreEqual(PartySlots.MaxCap, PartySlots.CapForBonus(PartySlots.MaxBonus));
            Assert.AreEqual(PartySlots.MaxCap, PartySlots.CapForBonus(99), "a corrupt save must not widen the party");
            Assert.AreEqual(PartySlots.BaseCap, PartySlots.CapForBonus(-3));
        }

        [Test]
        public void CostForNext_RisesPerSlotAndIsFreeOnceMaxed()
        {
            int first = PartySlots.CostForNext(0);
            int second = PartySlots.CostForNext(1);

            Assert.Greater(first, 0);
            Assert.Greater(second, first, "each extra hero should cost more than the last");
            Assert.AreEqual(0, PartySlots.CostForNext(PartySlots.MaxBonus), "no cost when there is nothing to buy");
        }

        // --- Selection resolution ---------------------------------------------

        private static readonly List<string> Owned =
            new List<string> { "Warrior", "Tank", "Scout", "Acolyte" };

        private static PartySaveData SaveWith(params string[] selected)
        {
            return new PartySaveData { SelectedHeroKeys = new List<string>(selected) };
        }

        [Test]
        public void ResolveSelection_StoredSelection_IsHonouredInOrder()
        {
            var keys = HeroRoster.ResolveSelection(SaveWith("Scout", "Warrior"), Owned, 3);

            Assert.AreEqual(new[] { "Scout", "Warrior" }, keys, "index 0 is the leader, so order matters");
        }

        [Test]
        public void ResolveSelection_NoStoredSelection_FieldsTheOwnedRosterUpToTheCap()
        {
            // This is the migration path: a save written before selection existed.
            var keys = HeroRoster.ResolveSelection(SaveWith(), Owned, 2);

            Assert.AreEqual(new[] { "Warrior", "Tank" }, keys);
        }

        [Test]
        public void ResolveSelection_SelectionOverTheCap_IsTruncatedNotRejected()
        {
            // Buying a slot then losing it (or an edited save) must not brick the party.
            var keys = HeroRoster.ResolveSelection(SaveWith("Warrior", "Tank", "Scout", "Acolyte"), Owned, 2);

            Assert.AreEqual(new[] { "Warrior", "Tank" }, keys);
        }

        [Test]
        public void ResolveSelection_UnownedKeys_AreDroppedSilently()
        {
            var keys = HeroRoster.ResolveSelection(SaveWith("Ghost", "Tank"), Owned, 3);

            Assert.AreEqual(new[] { "Tank" }, keys);
        }

        [Test]
        public void ResolveSelection_EntirelyUnownedSelection_FallsBackToTheRoster()
        {
            // A hero removed from the catalog must not leave the player unable to enter a dungeon.
            var keys = HeroRoster.ResolveSelection(SaveWith("Ghost", "Wraith"), Owned, 2);

            Assert.AreEqual(new[] { "Warrior", "Tank" }, keys);
        }

        [Test]
        public void ResolveSelection_DuplicateKeys_AreCollapsed()
        {
            var keys = HeroRoster.ResolveSelection(SaveWith("Tank", "Tank", "Scout"), Owned, 4);

            Assert.AreEqual(new[] { "Tank", "Scout" }, keys);
        }

        [Test]
        public void ResolveSelection_NothingOwned_YieldsNobody()
        {
            Assert.IsEmpty(HeroRoster.ResolveSelection(SaveWith("Warrior"), new List<string>(), 2));
            Assert.IsEmpty(HeroRoster.ResolveSelection(SaveWith("Warrior"), null, 2));
        }

        [Test]
        public void ResolveSelection_NonsenseCap_StillFieldsExactlyOneHero()
        {
            var keys = HeroRoster.ResolveSelection(SaveWith("Warrior", "Tank"), Owned, 0);

            Assert.AreEqual(new[] { "Warrior" }, keys, "a party of nobody cannot enter a dungeon");
        }

        [Test]
        public void ResolveSelection_CapAboveTheCeiling_IsClampedToMaxCap()
        {
            var owned = new List<string>(Owned) { "Fifth" };
            var keys = HeroRoster.ResolveSelection(new PartySaveData(), owned, 99);

            Assert.AreEqual(PartySlots.MaxCap, keys.Count);
        }
    }
}
