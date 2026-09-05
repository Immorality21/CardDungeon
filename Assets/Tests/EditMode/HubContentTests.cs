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
        public void NoTwoLots_HitBoxesOverlap()
        {
            // UI Toolkit hit-testing is rectangular. Overlapping HIT boxes make one lot steal the
            // other's clicks, and the symptom is a building that looks fine and does nothing. The
            // sprites are free to overlap - that is what makes the town look painted.
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
            Assert.AreEqual(1, BuildingOps.LevelOf(campfire, HubProgress.Fresh),
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

        /// <summary>
        /// A fresh save has to be able to *do* something in town. With the gates on, this is the
        /// check that the opening sequence was authored rather than assumed: at least one lot is
        /// standing, and at least one more is offered so there is a reason to want a material.
        /// </summary>
        [Test]
        public void AFreshSave_HasAWorkingTown()
        {
            var hub = LoadHub();
            var built = new List<string>();
            var offered = new List<string>();
            foreach (var building in BuildingOps.InDrawOrder(hub))
            {
                switch (BuildingOps.StateOf(building, HubProgress.Fresh))
                {
                    case BuildingState.Built:
                        built.Add(building.SaveKey);
                        break;
                    case BuildingState.Available:
                        offered.Add(building.SaveKey);
                        break;
                }
            }

            CollectionAssert.IsNotEmpty(built,
                "Nothing is standing on a fresh save, so the hub opens with nothing to click.");
            CollectionAssert.IsNotEmpty(offered,
                "Nothing is offered on a fresh save, so a first-time player sees a finished town "
                + "with no reason to bring materials home.");
        }

        /// <summary>
        /// Every gate names a run that exists. A typo here is silent: the lot simply never becomes
        /// available, and it looks like content that was never authored.
        /// </summary>
        [Test]
        public void EveryGate_NamesARunThatExists()
        {
            var campaign = UnityEngine.Resources.Load<CampaignSO>(CampaignSO.ResourcePath);
            var runKeys = new List<string>();
            foreach (var node in campaign.Nodes)
            {
                if (node?.Run != null)
                {
                    runKeys.Add(CampaignOps.RunKeyOf(node.Run));
                }
            }

            CollectionAssert.IsEmpty(BuildingOps.GetUnreachableLots(LoadHub(), runKeys));
        }

        /// <summary>
        /// Every lot is reachable by clearing the campaign in order. A lot gated behind a run that
        /// is itself unreachable, or behind a branch the player may never take, is content nobody
        /// sees - the hub equivalent of an unreachable sphere-grid node.
        /// </summary>
        [Test]
        public void EveryLot_BecomesAvailableByClearingTheCampaign()
        {
            var campaign = UnityEngine.Resources.Load<CampaignSO>(CampaignSO.ResourcePath);
            var everyRun = new List<string>();
            foreach (var node in campaign.Nodes)
            {
                if (node?.Run != null)
                {
                    everyRun.Add(CampaignOps.RunKeyOf(node.Run));
                }
            }

            var wholeCampaignCleared = new HubProgress(null, everyRun);
            var stuck = new List<string>();
            foreach (var building in BuildingOps.InDrawOrder(LoadHub()))
            {
                if (BuildingOps.StateOf(building, wholeCampaignCleared) == BuildingState.Absent)
                {
                    stuck.Add(building.SaveKey);
                }
            }
            CollectionAssert.IsEmpty(stuck,
                "These lots are never offered, even with the whole campaign cleared.");
        }

        /// <summary>
        /// A material price has to be payable. Every line names a real material, and no lot asks
        /// for more of one than a whole campaign yields - see the measured table in HUB.md section
        /// 7. A price above the tap is a lot nobody can ever build.
        /// </summary>
        [Test]
        public void EveryPlacementPrice_IsValidAndPayable()
        {
            var broken = new List<string>();
            foreach (var building in BuildingOps.InDrawOrder(LoadHub()))
            {
                if (building.PlacementCost == null)
                {
                    continue;
                }
                for (int i = 0; i < building.PlacementCost.Count; i++)
                {
                    var line = building.PlacementCost[i];
                    if (line == null || !line.IsValid)
                    {
                        broken.Add($"{building.SaveKey} placement line {i} is not a valid price "
                                   + "(no item, a non-material item, or amount 0).");
                    }
                    else if (line.Amount > MaxAffordableMaterialUnits)
                    {
                        broken.Add($"{building.SaveKey} asks for {line.Amount} "
                                   + $"{line.Material.Key}, past what a campaign yields.");
                    }
                }
            }
            CollectionAssert.IsEmpty(broken, string.Join("\n", broken));
        }

        /// <summary>The thinnest material in the yield table (Rotted Timber, ~3.1 a campaign) sets
        /// the ceiling for what any single line may ask.</summary>
        private const int MaxAffordableMaterialUnits = 12;

        [Test]
        public void NoLot_OffersAFreeUpgrade()
        {
            // Gold gates *when*. An upgradable lot with no price is a level the player is handed,
            // which is almost certainly a slip rather than a design.
            CollectionAssert.IsEmpty(BuildingOps.GetFreeUpgrades(LoadHub()));
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
