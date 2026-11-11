using System.Collections.Generic;
using Assets.Scripts.Dungeon;
using Assets.Scripts.Heroes;
using Assets.Scripts.Heroes.UI;
using Assets.Scripts.Hub;
using Assets.Scripts.Hub.UI;
using Assets.Scripts.Items;
using Assets.Scripts.Tutorial;
using ImmoralityGaming.Localization;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// Playtest 4's UX pass (2026-11-10): the Controls list (finding 58), the campfire's lead and potion
    /// picker (49, 50, 52), the story map's view keys and joiner line (48, 22) and the town's shape-true
    /// hit test (47).
    /// </summary>
    public class Playtest4UxTests
    {
        // --- Controls ---------------------------------------------------------------

        [Test]
        public void ControlsList_EveryKey_IsInTheTextTable()
        {
            var missing = new List<string>();
            foreach (var group in ControlsList.Groups)
            {
                if (!Loc.Has(group.TitleKey))
                {
                    missing.Add(group.TitleKey);
                }
                foreach (var line in group.Lines)
                {
                    if (!Loc.Has(line.KeysKey))
                    {
                        missing.Add(line.KeysKey);
                    }
                    if (!Loc.Has(line.ActionKey))
                    {
                        missing.Add(line.ActionKey);
                    }
                }
            }
            Assert.That(missing, Is.Empty, "Controls keys with no text-table entry");
        }

        [Test]
        public void ControlsList_NoKeyIsUsedTwice_AndNoGroupIsEmpty()
        {
            var seen = new HashSet<string>();
            foreach (var group in ControlsList.Groups)
            {
                Assert.That(group.Lines.Count, Is.GreaterThan(0), group.TitleKey);
                Assert.That(seen.Add(group.TitleKey), Is.True, group.TitleKey);
                foreach (var line in group.Lines)
                {
                    Assert.That(seen.Add(line.KeysKey), Is.True, line.KeysKey);
                    Assert.That(seen.Add(line.ActionKey), Is.True, line.ActionKey);
                }
            }
        }

        [Test]
        public void ControlsKeys_BackKeysAndF1Close_ArrowsAndPagesScroll()
        {
            Assert.AreEqual(ControlsKeys.Action.Close, ControlsKeys.ActionFor(KeyCode.Escape));
            Assert.AreEqual(ControlsKeys.Action.Close, ControlsKeys.ActionFor(KeyCode.F1));
            Assert.AreEqual(ControlsKeys.Action.Close, ControlsKeys.ActionFor(KeyCode.Return));
            Assert.AreEqual(ControlsKeys.Action.ScrollUp, ControlsKeys.ActionFor(KeyCode.UpArrow));
            Assert.AreEqual(ControlsKeys.Action.ScrollDown, ControlsKeys.ActionFor(KeyCode.DownArrow));
            Assert.AreEqual(ControlsKeys.Action.PageDown, ControlsKeys.ActionFor(KeyCode.PageDown));
            Assert.AreEqual(ControlsKeys.Action.None, ControlsKeys.ActionFor(KeyCode.M));
        }

        // --- Campfire: the leader ----------------------------------------------------

        [Test]
        public void CampfireLead_BenchedHeroWithRoom_JoinsInFront()
        {
            var fielded = new List<string> { "warrior", "paladin" };
            Assert.AreEqual(LeadRefusal.None, CampfireLeadOps.CanLead(fielded, "mage", 4, false));
            CollectionAssert.AreEqual(new[] { "mage", "warrior", "paladin" },
                CampfireLeadOps.WithLeader(fielded, "mage", 4, false));
        }

        [Test]
        public void CampfireLead_MarchingHero_MovesToSeatOne_EvenWhileTheRunLocksTheLineup()
        {
            var fielded = new List<string> { "warrior", "paladin", "mage" };
            CollectionAssert.AreEqual(new[] { "mage", "warrior", "paladin" },
                CampfireLeadOps.WithLeader(fielded, "mage", 4, true));
        }

        [Test]
        public void CampfireLead_RefusesAFullPartyALockedLineupAndTheLeader()
        {
            var full = new List<string> { "a", "b", "c", "d" };
            Assert.AreEqual(LeadRefusal.PartyFull, CampfireLeadOps.CanLead(full, "e", 4, false));
            Assert.AreEqual(LeadRefusal.LineupLocked, CampfireLeadOps.CanLead(new List<string> { "a" }, "e", 4, true));
            Assert.AreEqual(LeadRefusal.AlreadyLeads, CampfireLeadOps.CanLead(full, "a", 4, false));
            CollectionAssert.AreEqual(full, CampfireLeadOps.WithLeader(full, "e", 4, false));
        }

        // --- Campfire: the potion picker ---------------------------------------------

        [Test]
        public void BeltAssign_TypeInAnotherSlot_Swaps()
        {
            var loadout = new List<string> { "Healing", "Ether", PotionBeltOps.Empty };
            CollectionAssert.AreEqual(new[] { "Ether", "Healing", "" }, PotionBeltOps.Assign(loadout, 0, "Ether"));
            CollectionAssert.AreEqual(new[] { "Healing", "Ether", "" }, loadout, "the input is not changed");
        }

        [Test]
        public void BeltAssign_IntoAnEmptySlot_LeavesTheOtherSlotEmpty()
        {
            var loadout = new List<string> { "Healing", PotionBeltOps.Empty };
            CollectionAssert.AreEqual(new[] { "", "Healing" }, PotionBeltOps.Assign(loadout, 1, "Healing"));
        }

        [Test]
        public void BeltAssign_EmptyNeverSwaps_AndAnUnknownSlotChangesNothing()
        {
            var loadout = new List<string> { "Healing", "Ether" };
            CollectionAssert.AreEqual(new[] { "", "Ether" }, PotionBeltOps.Assign(loadout, 0, PotionBeltOps.Empty));
            CollectionAssert.AreEqual(loadout, PotionBeltOps.Assign(loadout, 5, "Healing"));
        }

        // --- Story map ----------------------------------------------------------------

        [Test]
        public void MapViewKeys_WasdPans_PlusMinusZoom_ArrowsAreLeftToTheCursor()
        {
            Assert.That(MapViewKeys.PanFor(KeyCode.W).y, Is.GreaterThan(0f));
            Assert.That(MapViewKeys.PanFor(KeyCode.D).x, Is.LessThan(0f));
            Assert.AreEqual(Vector2.zero, MapViewKeys.PanFor(KeyCode.UpArrow));
            Assert.That(MapViewKeys.ZoomFor(KeyCode.Equals), Is.GreaterThan(1f));
            Assert.That(MapViewKeys.ZoomFor(KeyCode.KeypadMinus), Is.LessThan(1f));
            Assert.AreEqual(1f, MapViewKeys.ZoomFor(KeyCode.PageUp));
        }

        [Test]
        public void ZoomedPan_KeepsTheAnchoredPointStill()
        {
            var pan = new Vector2(30f, -10f);
            var anchor = new Vector2(400f, 300f);
            var gridPoint = (anchor - pan) / 1f;
            var newPan = SphereGridView.ZoomedPan(pan, 1f, 2f, anchor);
            Assert.That((gridPoint * 2f + newPan - anchor).magnitude, Is.LessThan(0.001f));
        }

        [Test]
        public void HeroJoiningOnClear_OnlyWhileNotOwned()
        {
            var hero = ScriptableObject.CreateInstance<HeroSO>();
            hero.Key = "tinkerer";
            var run = ScriptableObject.CreateInstance<RunDefinitionSO>();
            run.JoinsOnClear = hero;
            try
            {
                Assert.AreSame(hero, CampaignOps.HeroJoiningOnClear(run, new List<string> { "warrior" }));
                Assert.IsNull(CampaignOps.HeroJoiningOnClear(run, new List<string> { "tinkerer" }));
                Assert.IsNull(CampaignOps.HeroJoiningOnClear(run, null), "an unknown roster says nothing");
                run.JoinsOnClear = null;
                Assert.IsNull(CampaignOps.HeroJoiningOnClear(run, new List<string>()));
            }
            finally
            {
                Object.DestroyImmediate(run);
                Object.DestroyImmediate(hero);
            }
        }

        // --- Town: shape-true hover and click -----------------------------------------

        /// <summary>A 4x4 sprite, bottom row first as Unity hands it over: solid only at the top-left texel.</summary>
        private static SpriteAlphaMask TopLeftDot()
        {
            var pixels = new Color32[16];
            // Bottom-up: the top row is the last four pixels; its first is the top-left texel.
            pixels[12] = new Color32(255, 255, 255, 255);
            return SpriteAlphaMask.FromPixelsBottomUp(pixels, 4, 4);
        }

        [Test]
        public void AlphaMask_ReadsBottomUpPixels_AsTopDown()
        {
            var mask = TopLeftDot();
            Assert.IsTrue(mask.IsSolid(0, 0));
            Assert.IsFalse(mask.IsSolid(0, 3));
        }

        [Test]
        public void AlphaMask_Hits_OnTheShapeAndOneTexelAround_NotFarther()
        {
            var mask = TopLeftDot();
            Assert.IsTrue(mask.Hits(new Vector2(0.1f, 0.1f), 0));
            Assert.IsFalse(mask.Hits(new Vector2(0.3f, 0.1f), 0));
            Assert.IsTrue(mask.Hits(new Vector2(0.3f, 0.1f), 1), "one texel of slack");
            Assert.IsFalse(mask.Hits(new Vector2(0.9f, 0.9f), 1));
        }

        [Test]
        public void AlphaMask_Outline_IsTheRingAroundTheShape()
        {
            var ring = TopLeftDot().OutlineTopDown();
            const int w = 6;
            // The dot is at ring (1,1); its four neighbours are on the ring, it and the diagonals are not.
            Assert.IsTrue(ring[0 * w + 1]);
            Assert.IsTrue(ring[1 * w + 0]);
            Assert.IsTrue(ring[1 * w + 2]);
            Assert.IsTrue(ring[2 * w + 1]);
            Assert.IsFalse(ring[1 * w + 1]);
            Assert.IsFalse(ring[0 * w + 0]);
        }

        [Test]
        public void FitInside_CentresAndScalesUniformly()
        {
            var rect = SpriteAlphaMask.FitInside(new Rect(0f, 0f, 300f, 192f), 64, 64);
            Assert.AreEqual(192f, rect.width, 0.001f);
            Assert.AreEqual(54f, rect.x, 0.001f);
            Assert.AreEqual(0f, rect.y, 0.001f);
        }
    }
}
