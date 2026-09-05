using System.Collections.Generic;
using Assets.Scripts.Hub;
using Assets.Scripts.Progression;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// The hub-building rules (<see cref="BuildingOps"/>): how a lot's state and level resolve from
    /// the save, how the town's paint order is decided, and the authoring validators. All pure —
    /// buildings are built in memory via <c>ScriptableObject.CreateInstance</c>, no assets and no
    /// scene, the same shape as <c>SphereGridOpsTests</c>.
    ///
    /// <para>Several tests pass the phase switch explicitly rather than relying on
    /// <see cref="BuildingOps.EverythingIsPlaced"/>. That is the point of the overload: the gated
    /// behaviour <c>docs/plans/HUB.md</c> §7 phase 4 will switch on is covered *now*, so it does not
    /// arrive untested on the day the constant flips.</para>
    /// </summary>
    public class BuildingOpsTests
    {
        private static BuildingSO Lot(
            string key,
            HubService service = HubService.Merchant,
            bool placedByDefault = false,
            Vector2 position = default,
            Vector2 size = default,
            int drawOrder = 0)
        {
            var building = ScriptableObject.CreateInstance<BuildingSO>();
            building.name = key;
            building.Key = key;
            building.Service = service;
            building.PlacedByDefault = placedByDefault;
            building.Position = position;
            building.HitSize = size == default ? new Vector2(100f, 100f) : size;
            building.DrawOrder = drawOrder;
            return building;
        }

        private static HubSO Town(params BuildingSO[] buildings)
        {
            var hub = ScriptableObject.CreateInstance<HubSO>();
            hub.ReferenceSize = new Vector2(1280f, 720f);
            hub.Buildings = new List<BuildingSO>(buildings);
            return hub;
        }

        private static List<BuildingProgress> Saved(string key, int level)
        {
            return new List<BuildingProgress> { new BuildingProgress { Key = key, Level = level } };
        }

        // --- level + state ---------------------------------------------------------

        [Test]
        public void LevelOf_PlacedByDefault_IsLevelOneWithNothingSaved()
        {
            // The campfire has to work on a fresh profile without the save writing anything to own it.
            Assert.AreEqual(1, BuildingOps.LevelOf(Lot("campfire", placedByDefault: true), null, false));
        }

        [Test]
        public void LevelOf_UnbuiltLot_IsZeroOnceTheGatesAreOn()
        {
            Assert.AreEqual(0, BuildingOps.LevelOf(Lot("forge"), null, false));
        }

        [Test]
        public void LevelOf_SavedLevelWins_AndIsClampedToMaxLevel()
        {
            var lot = Lot("forge");
            lot.MaxLevel = 2;

            Assert.AreEqual(2, BuildingOps.LevelOf(lot, Saved("forge", 2), false));
            Assert.AreEqual(2, BuildingOps.LevelOf(lot, Saved("forge", 9), false),
                "A save written against a taller building must not out-level the authored ceiling.");
            Assert.AreEqual(0, BuildingOps.LevelOf(lot, Saved("someone-else", 3), false),
                "Another lot's entry is not this lot's.");
        }

        [Test]
        public void StateOf_UnbuiltLot_IsAvailableWhenNothingGatesIt_AbsentWhenSomethingDoes()
        {
            Assert.AreEqual(BuildingState.Available, BuildingOps.StateOf(Lot("forge"), null, false),
                "A bare lot the player could build on is the affordance that makes a material worth "
                + "wanting - it must not read the same as empty ground.");

            var gated = Lot("forge");
            gated.RequiredRunKeys = new List<string> { "run-2" };
            Assert.AreEqual(BuildingState.Absent, BuildingOps.StateOf(gated, null, false));
        }

        [Test]
        public void StateOf_WhileEverythingIsPlaced_EveryLotIsBuiltAtLevelOne()
        {
            // The phase 2/3 contract: the data model and the town render while the game plays exactly
            // as it did. A lot reading Built at level 0 would be a state nothing could draw.
            var lot = Lot("forge");
            Assert.AreEqual(BuildingState.Built, BuildingOps.StateOf(lot, null));
            Assert.AreEqual(1, BuildingOps.LevelOf(lot, null));
            Assert.IsTrue(BuildingOps.IsBuilt(lot, null));
        }

        [Test]
        public void StateOf_NullBuilding_IsAbsent()
        {
            Assert.AreEqual(BuildingState.Absent, BuildingOps.StateOf(null, null));
            Assert.AreEqual(0, BuildingOps.LevelOf(null, null));
        }

        // --- paint order -----------------------------------------------------------

        [Test]
        public void InDrawOrder_SortsByDrawOrderThenY_ThenListOrder()
        {
            // UI Toolkit has no z-index - siblings paint in the order they are added - so this sort
            // is the only thing deciding which building is in front.
            var back = Lot("back", drawOrder: 0, position: new Vector2(0f, 500f));
            var front = Lot("front", drawOrder: 10, position: new Vector2(0f, 0f));
            var high = Lot("high", drawOrder: 0, position: new Vector2(0f, 100f));

            var order = BuildingOps.InDrawOrder(Town(back, front, high));

            Assert.AreEqual(new[] { "high", "back", "front" },
                new[] { order[0].SaveKey, order[1].SaveKey, order[2].SaveKey },
                "DrawOrder first, then lower on the screen paints later (a painter's algorithm).");
        }

        [Test]
        public void InDrawOrder_EqualLots_KeepListOrder_AndDropTheUnkeyed()
        {
            var first = Lot("first");
            var second = Lot("second");
            var nameless = Lot("nameless");
            nameless.Key = "";
            nameless.name = "";

            var order = BuildingOps.InDrawOrder(Town(first, null, nameless, second));

            Assert.AreEqual(2, order.Count, "A null or unkeyed lot is dropped, not drawn.");
            Assert.AreEqual("first", order[0].SaveKey);
            Assert.AreEqual("second", order[1].SaveKey);
        }

        [Test]
        public void InDrawOrder_NullHub_IsEmptyRatherThanThrowing()
        {
            CollectionAssert.IsEmpty(BuildingOps.InDrawOrder(null));
        }

        // --- authoring validators --------------------------------------------------

        [Test]
        public void GetDuplicateKeys_FindsTwoLotsSharingASaveIdentifier()
        {
            var hub = Town(Lot("forge"), Lot("forge", HubService.Bestiary), Lot("merchant"));

            Assert.AreEqual(new List<string> { "forge" }, BuildingOps.GetDuplicateKeys(hub));
        }

        [Test]
        public void GetServicesWithNoBuilding_NamesEveryScreenNobodyCanOpen()
        {
            var missing = BuildingOps.GetServicesWithNoBuilding(Town(Lot("merchant", HubService.Merchant)));

            CollectionAssert.DoesNotContain(missing, HubService.Merchant);
            CollectionAssert.Contains(missing, HubService.Forge);
            CollectionAssert.Contains(missing, HubService.Party);
        }

        [Test]
        public void GetDuplicateServices_FindsTwoDoorsToOneRoom()
        {
            var hub = Town(Lot("a", HubService.Forge), Lot("b", HubService.Forge));

            Assert.AreEqual(new List<HubService> { HubService.Forge }, BuildingOps.GetDuplicateServices(hub));
        }

        [Test]
        public void GetOverlappingLots_FindsLotsThatWouldStealEachOthersClicks()
        {
            // UI Toolkit hit-testing is rectangular, so an overlap makes one lot silently swallow the
            // other's clicks - it looks like a dead building, not like a layout mistake.
            var a = Lot("a", position: new Vector2(0f, 0f), size: new Vector2(100f, 100f));
            var b = Lot("b", position: new Vector2(50f, 50f), size: new Vector2(100f, 100f));
            var clear = Lot("clear", position: new Vector2(400f, 400f), size: new Vector2(100f, 100f));

            var overlaps = BuildingOps.GetOverlappingLots(Town(a, b, clear));

            Assert.AreEqual(1, overlaps.Count, string.Join(" | ", overlaps));
            StringAssert.Contains("a", overlaps[0]);
            StringAssert.Contains("b", overlaps[0]);
        }

        [Test]
        public void GetOverlappingLots_TouchingEdges_DoNotCount()
        {
            var a = Lot("a", position: new Vector2(0f, 0f), size: new Vector2(100f, 100f));
            var b = Lot("b", position: new Vector2(100f, 0f), size: new Vector2(100f, 100f));

            CollectionAssert.IsEmpty(BuildingOps.GetOverlappingLots(Town(a, b)));
        }

        [Test]
        public void GetLotsOutsideTheRect_FindsContentNoLetterboxCanShow()
        {
            var inside = Lot("inside", position: new Vector2(10f, 10f), size: new Vector2(100f, 100f));
            var over = Lot("over", position: new Vector2(1250f, 10f), size: new Vector2(100f, 100f));

            var outside = BuildingOps.GetLotsOutsideTheRect(Town(inside, over));

            Assert.AreEqual(1, outside.Count, string.Join(" | ", outside));
            StringAssert.Contains("over", outside[0]);
        }
    }
}
