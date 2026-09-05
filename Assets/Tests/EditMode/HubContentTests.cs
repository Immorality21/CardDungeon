using System.Collections.Generic;
using Assets.Scripts.Dungeon;
using Assets.Scripts.Hub;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// The project's real hub asset, checked the way <see cref="CampaignAssetTests"/> checks the
    /// campaign: an authored layout where every fault is invisible until someone clicks the lot that
    /// does not work. Nothing here is a fault play skill can work around.
    /// </summary>
    public class HubContentTests
    {
        private static HubSO LoadHub()
        {
            return UnityEngine.Resources.Load<HubSO>(HubSO.ResourcePath);
        }

        [Test]
        public void Hub_ExistsInResources()
        {
            Assert.IsNotNull(LoadHub(),
                $"No hub at Assets/Resources/{HubSO.ResourcePath}.asset - the town is Resources-loaded "
                + "like the campaign and the item catalog, so without it the hub has nothing to draw.");
        }

        [Test]
        public void EveryLot_HasAUniqueNonEmptyKey()
        {
            var hub = LoadHub();
            foreach (var building in hub.Buildings)
            {
                Assert.IsNotNull(building, hub.name + " has an empty slot in its building list.");
                Assert.IsNotEmpty(building.SaveKey, building.name + " has no save key.");
            }
            CollectionAssert.IsEmpty(BuildingOps.GetDuplicateKeys(hub),
                "Two lots share a save key, so the save records the wrong building.");
        }

        [Test]
        public void EveryService_IsOpenedByExactlyOneLot()
        {
            var hub = LoadHub();

            CollectionAssert.IsEmpty(BuildingOps.GetServicesWithNoBuilding(hub),
                "A HubService with no lot is a screen the player cannot reach at all.");
            CollectionAssert.IsEmpty(BuildingOps.GetDuplicateServices(hub),
                "Two lots opening one screen means one of them is a building with nothing behind it.");
        }

        [Test]
        public void NoTwoLots_Overlap()
        {
            // UI Toolkit hit-testing is rectangular. Overlapping silhouettes steal each other's
            // clicks, and the symptom is a building that looks fine and does nothing.
            CollectionAssert.IsEmpty(BuildingOps.GetOverlappingLots(LoadHub()));
        }

        [Test]
        public void EveryLot_SitsInsideTheReferenceRect()
        {
            var hub = LoadHub();
            Assert.Greater(hub.ReferenceSize.x, 0f, "The reference rect is what every Position means.");
            Assert.Greater(hub.ReferenceSize.y, 0f);
            CollectionAssert.IsEmpty(BuildingOps.GetLotsOutsideTheRect(hub),
                "The town letterboxes as one unit, so a lot outside the rect is never on screen.");
        }

        [Test]
        public void TheCampfire_IsPlacedByDefault()
        {
            var campfire = LoadHub().Find(HubService.Party);

            Assert.IsNotNull(campfire, "No lot opens the party screen.");
            Assert.IsTrue(campfire.PlacedByDefault,
                "The campfire is the one building that exists in minute one (HUB.md section 7). "
                + "Without it a fresh save opens a hub it cannot do anything in.");
            Assert.AreEqual(1, BuildingOps.LevelOf(campfire, null, false),
                "It must read as built with nothing in the save, or a fresh profile has to write "
                + "before it can play.");
        }

        [Test]
        public void TheStory_IsNotABuilding()
        {
            // A building must never be able to lock the player out of running (HUB.md section 7 open
            // question 4). The road is a fixed element of the hub view, so this is the structural
            // half of the guarantee CampaignAssetTests.Campaign_NeverStrandsASaveWithNothingToPlay
            // makes about the graph: with no lots placed at all, there is still something to play.
            var campaign = UnityEngine.Resources.Load<CampaignSO>(CampaignSO.ResourcePath);
            Assert.IsTrue(CampaignOps.HasSomethingToPlay(campaign, new List<string>(), ""),
                "A fresh save with an empty hub has no run available.");

            foreach (HubService service in System.Enum.GetValues(typeof(HubService)))
            {
                Assert.AreNotEqual("Story", service.ToString(),
                    "The campaign map must not become a HubService - it is the way out of town, not "
                    + "a service the town provides.");
            }
        }

        [Test]
        public void EveryLot_HasAServiceLabelWorthShowing()
        {
            foreach (var building in LoadHub().Buildings)
            {
                Assert.IsNotEmpty(building.Label,
                    building.SaveKey + " has no display name, so its lot renders unlabelled - and "
                    + "with placeholder art the label is the only thing identifying it.");
            }
        }
    }
}
