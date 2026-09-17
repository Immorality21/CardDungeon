using System.Collections.Generic;
using Assets.Scripts.Heroes;
using Assets.Scripts.Hub;
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

        // --- Party width -------------------------------------------------------

        [Test]
        public void PartyWidth_IsFourFromTheStart_AndIsNotBought()
        {
            // The slot purchase was removed on 2026-09-17: gold never buys a hero (section 5b), and
            // buying the right to FIELD one was the same trade wearing a different hat. What paces
            // width now is the roster, and what prices it is the XP split.
            Assert.AreEqual(4, PartySlots.MaxCap);
            Assert.AreEqual(PartySlots.MaxCap, PartySlots.BaseCap,
                "A fresh save may field the full party - nothing widens it any more.");
        }

        [Test]
        public void FreeWidth_IsTheSoloStart_NotTheCap()
        {
            // What the investment frontier charges width from. Pricing from the cap would hand the
            // model three heroes nobody went and earned.
            Assert.AreEqual(1, PartySlots.FreeWidth);
            Assert.Less(PartySlots.FreeWidth, PartySlots.MaxCap);
        }

        // --- The campfire's XP split -------------------------------------------

        [Test]
        public void FavouredSplit_GivesTheChosenHeroADoubleShare()
        {
            // 12 XP across four heroes with one mentored divides into FIVE parts, not four: 2 a
            // part, a double share for the mentor, and the remainder follows the mentor too.
            Assert.AreEqual(new[] { 6, 2, 2, 2 }, XpSplit.Split(12, 4, 0));
            Assert.AreEqual(new[] { 2, 6, 2, 2 }, XpSplit.Split(12, 4, 1));
        }

        [Test]
        public void FavouredSplit_AlwaysSumsToTheAwardedTotal()
        {
            // The load-bearing property: every mode is a REDISTRIBUTION. If a campfire level could
            // change the total, it would be granting power and the balance model's XP accounting
            // would be wrong wherever the player had upgraded.
            for (int size = 1; size <= PartySlots.MaxCap; size++)
            {
                for (int favoured = 0; favoured < size; favoured++)
                {
                    for (int total = 0; total < 40; total++)
                    {
                        int sum = 0;
                        foreach (int share in XpSplit.Split(total, size, favoured))
                        {
                            sum += share;
                        }
                        Assert.AreEqual(total, sum, $"total {total} across {size} favouring {favoured}");
                    }
                }
            }
        }

        [Test]
        public void FavouredSplit_NeverPaysTheFavouredHeroLessThanTheRest()
        {
            for (int size = 2; size <= PartySlots.MaxCap; size++)
            {
                // Index 1, so a leader-shaped bug could not hide behind index 0.
                var shares = XpSplit.Split(100, size, 1);
                for (int i = 0; i < size; i++)
                {
                    if (i == 1)
                    {
                        continue;
                    }
                    Assert.GreaterOrEqual(shares[1], shares[i], $"party of {size}, hero {i}");
                }
            }
        }

        [Test]
        public void FavouredSplit_NobodyNamed_FallsBackToEven()
        {
            // A party that has benched its mentor keeps earning rather than silently paying no one.
            Assert.AreEqual(XpSplit.Split(12, 4), XpSplit.Split(12, 4, -1));
            Assert.AreEqual(XpSplit.Split(12, 4), XpSplit.Split(12, 4, 9));
        }

        [Test]
        public void FavouredIndex_MentorTakesTheNomination_CatchUpTakesTheLowest()
        {
            var banked = new List<int> { 400, 120, 900 };

            Assert.AreEqual(2, XpSplit.FavouredIndex(XpSplitMode.Mentor, 2, banked));
            Assert.AreEqual(1, XpSplit.FavouredIndex(XpSplitMode.CatchUp, 2, banked),
                "Catch Up ignores the nomination and picks whoever is furthest behind.");
            Assert.AreEqual(-1, XpSplit.FavouredIndex(XpSplitMode.Even, 2, banked));
        }

        [Test]
        public void FavouredIndex_CatchUp_BreaksTiesOnTheEarliest()
        {
            // Stable rather than flickering between two equally-behind heroes on consecutive kills.
            var banked = new List<int> { 50, 50, 900 };

            Assert.AreEqual(0, XpSplit.FavouredIndex(XpSplitMode.CatchUp, -1, banked));
        }

        [Test]
        public void CampfireModes_UnlockOneLevelAtATime()
        {
            CollectionAssert.AreEqual(new[] { XpSplitMode.Even }, CampfireOps.ModesFor(1));
            CollectionAssert.AreEqual(new[] { XpSplitMode.Even, XpSplitMode.Mentor }, CampfireOps.ModesFor(2));
            CollectionAssert.AreEqual(new[] { XpSplitMode.Even, XpSplitMode.Mentor, XpSplitMode.CatchUp },
                CampfireOps.ModesFor(3));
        }

        [Test]
        public void CampfireModes_AnUnbuiltFire_StillSplitsEvenly()
        {
            // Level 0 can only be a scene with no hub asset. Degrading to "the way it has always
            // worked" beats degrading to a party that earns nothing.
            CollectionAssert.AreEqual(new[] { XpSplitMode.Even }, CampfireOps.ModesFor(0));
        }

        [Test]
        public void EffectiveMode_RefusesAModeTheFireCannotGrant()
        {
            Assert.AreEqual(XpSplitMode.Even, CampfireOps.EffectiveMode(1, XpSplitMode.Mentor),
                "A save naming a mode it never unlocked must not run it.");
            Assert.AreEqual(XpSplitMode.Mentor, CampfireOps.EffectiveMode(2, XpSplitMode.Mentor));
            Assert.AreEqual(XpSplitMode.Even, CampfireOps.EffectiveMode(2, XpSplitMode.CatchUp));
            Assert.AreEqual(XpSplitMode.CatchUp, CampfireOps.EffectiveMode(3, XpSplitMode.CatchUp));
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
