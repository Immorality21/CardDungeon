using System.Collections.Generic;
using Assets.Scripts.Hub;
using Assets.Scripts.Progression;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// The hub-building rules (<see cref="BuildingOps"/>): how a lot's state and level resolve from
    /// the save and the campaign, how the town's paint order is decided, where the sprite paints, and
    /// the authoring validators. All pure — buildings are built in memory via
    /// <c>ScriptableObject.CreateInstance</c>, no assets and no scene, the same shape as
    /// <c>SphereGridOpsTests</c>.
    /// </summary>
    public class BuildingOpsTests
    {
        private static BuildingSO Lot(
            string key,
            HubService service = HubService.Merchant,
            bool placedByDefault = false,
            Vector2 position = default,
            Vector2 size = default,
            int drawOrder = 0,
            int maxLevel = 1,
            params string[] requiredRuns)
        {
            var building = ScriptableObject.CreateInstance<BuildingSO>();
            building.name = key;
            building.Key = key;
            building.Service = service;
            building.PlacedByDefault = placedByDefault;
            building.Position = position;
            building.HitSize = size == default ? new Vector2(100f, 100f) : size;
            building.DrawOrder = drawOrder;
            building.MaxLevel = maxLevel;
            building.RequiredRunKeys = new List<string>(requiredRuns);
            return building;
        }

        private static HubSO Town(params BuildingSO[] buildings)
        {
            var hub = ScriptableObject.CreateInstance<HubSO>();
            hub.ReferenceSize = new Vector2(1280f, 720f);
            hub.Buildings = new List<BuildingSO>(buildings);
            return hub;
        }

        private static HubProgress Built(string key, int level, params string[] clearedRuns)
        {
            return new HubProgress(
                new List<BuildingProgress> { new BuildingProgress { Key = key, Level = level } },
                new List<string>(clearedRuns));
        }

        private static HubProgress Cleared(params string[] runs)
        {
            return new HubProgress(null, new List<string>(runs));
        }

        // --- level + state ---------------------------------------------------------

        [Test]
        public void LevelOf_PlacedByDefault_IsLevelOneOnAFreshSave()
        {
            // The campfire has to work on a fresh profile without the save writing anything to own it.
            Assert.AreEqual(1, BuildingOps.LevelOf(Lot("campfire", placedByDefault: true), HubProgress.Fresh));
        }

        [Test]
        public void LevelOf_UnbuiltLot_IsZero()
        {
            Assert.AreEqual(0, BuildingOps.LevelOf(Lot("forge"), HubProgress.Fresh));
        }

        [Test]
        public void LevelOf_SavedLevelWins_AndIsClampedToMaxLevel()
        {
            var lot = Lot("forge", maxLevel: 2);

            Assert.AreEqual(2, BuildingOps.LevelOf(lot, Built("forge", 2)));
            Assert.AreEqual(2, BuildingOps.LevelOf(lot, Built("forge", 9)),
                "A save written against a taller building must not out-level the authored ceiling.");
            Assert.AreEqual(0, BuildingOps.LevelOf(lot, Built("someone-else", 3)),
                "Another lot's entry is not this lot's.");
        }

        [Test]
        public void StateOf_UngatedLot_IsAvailableBeforeItIsBuilt()
        {
            // A bare lot the player could build on is the affordance that makes a material worth
            // wanting - it must not read the same as empty ground.
            Assert.AreEqual(BuildingState.Available, BuildingOps.StateOf(Lot("forge"), HubProgress.Fresh));
            Assert.AreEqual(BuildingState.Built, BuildingOps.StateOf(Lot("forge"), Built("forge", 1)));
        }

        [Test]
        public void StateOf_GatedLot_IsAbsentUntilEveryRequiredRunIsCleared()
        {
            var lot = Lot("forge", requiredRuns: new[] { "run-a", "run-b" });

            Assert.AreEqual(BuildingState.Absent, BuildingOps.StateOf(lot, HubProgress.Fresh));
            Assert.AreEqual(BuildingState.Absent, BuildingOps.StateOf(lot, Cleared("run-a")),
                "Every requirement has to fall, not just one.");
            Assert.AreEqual(BuildingState.Available, BuildingOps.StateOf(lot, Cleared("run-a", "run-b")));
        }

        [Test]
        public void StateOf_ALotAlreadyBuilt_StaysBuiltEvenIfItsGateWouldNotPassNow()
        {
            // Placement is a one-way door. Re-authoring a requirement must never un-build something
            // the player already paid for.
            var lot = Lot("forge", requiredRuns: new[] { "run-a" });

            Assert.AreEqual(BuildingState.Built, BuildingOps.StateOf(lot, Built("forge", 1)));
        }

        [Test]
        public void StateOf_NullBuilding_IsAbsent()
        {
            Assert.AreEqual(BuildingState.Absent, BuildingOps.StateOf(null, HubProgress.Fresh));
            Assert.AreEqual(0, BuildingOps.LevelOf(null, HubProgress.Fresh));
        }

        // --- what can be done to a lot ---------------------------------------------

        [Test]
        public void CanPlace_OnlyWhenOfferedAndUnbuilt()
        {
            Assert.IsTrue(BuildingOps.CanPlace(Lot("forge"), HubProgress.Fresh));
            Assert.IsFalse(BuildingOps.CanPlace(Lot("forge"), Built("forge", 1)), "Already standing.");
            Assert.IsFalse(BuildingOps.CanPlace(Lot("forge", requiredRuns: new[] { "run-a" }), HubProgress.Fresh),
                "Not offered yet.");
        }

        [Test]
        public void CanUpgrade_NeedsALevelLeftAndABuildingToRaise()
        {
            var tall = Lot("merchant", maxLevel: 2);
            var flat = Lot("bestiary");

            Assert.IsFalse(BuildingOps.CanUpgrade(tall, HubProgress.Fresh), "Nothing to raise yet.");
            Assert.IsTrue(BuildingOps.CanUpgrade(tall, Built("merchant", 1)));
            Assert.IsFalse(BuildingOps.CanUpgrade(tall, Built("merchant", 2)), "Already at the ceiling.");
            Assert.IsFalse(BuildingOps.CanUpgrade(flat, Built("bestiary", 1)), "MaxLevel 1 never upgrades.");
        }

        [Test]
        public void NextLevel_IsOneForAPlacement_AndTheNextRungForAnUpgrade()
        {
            var tall = Lot("merchant", maxLevel: 3);

            Assert.AreEqual(1, BuildingOps.NextLevel(tall, HubProgress.Fresh));
            Assert.AreEqual(2, BuildingOps.NextLevel(tall, Built("merchant", 1)));
            Assert.AreEqual(0, BuildingOps.NextLevel(tall, Built("merchant", 3)), "Nothing left to buy.");
        }

        // --- geometry ---------------------------------------------------------------

        [Test]
        public void DrawRect_FallsBackToTheHitBox_WhenNoDrawSizeIsAuthored()
        {
            var lot = Lot("forge", position: new Vector2(10f, 20f), size: new Vector2(100f, 80f));

            Assert.AreEqual(new Rect(10f, 20f, 100f, 80f), BuildingOps.DrawRect(lot),
                "A lot authored before the split must still draw where it is clicked.");
        }

        [Test]
        public void DrawRect_MayOverhangTheHitBox_WhichIsThePoint()
        {
            // A painted town needs silhouettes bigger than the box you click - a tower behind a roof,
            // a banner past a wall.
            var lot = Lot("forge", position: new Vector2(100f, 100f), size: new Vector2(80f, 60f));
            lot.DrawOffset = new Vector2(-20f, -70f);
            lot.DrawSize = new Vector2(140f, 150f);

            Assert.AreEqual(new Rect(80f, 30f, 140f, 150f), BuildingOps.DrawRect(lot));
            Assert.AreEqual(new Rect(100f, 100f, 80f, 60f), BuildingOps.LotRect(lot),
                "The clickable box is untouched by where the art goes.");
        }

        // --- paint order -----------------------------------------------------------

        [Test]
        public void InDrawOrder_SortsByDrawOrderThenY_ThenListOrder()
        {
            // UI Toolkit has no z-index - siblings paint in the order they are added - so this sort is
            // the only thing deciding which building is in front.
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
        public void GetOverlappingLots_ChecksHitBoxes_AndIgnoresOverlappingArt()
        {
            // Overlapping HIT boxes make one lot silently swallow the other's clicks. Overlapping ART
            // is the goal, so it must not be reported.
            var a = Lot("a", position: new Vector2(0f, 0f), size: new Vector2(100f, 100f));
            var b = Lot("b", position: new Vector2(150f, 0f), size: new Vector2(100f, 100f));
            b.DrawOffset = new Vector2(-120f, 0f);
            b.DrawSize = new Vector2(200f, 200f);
            var clash = Lot("clash", position: new Vector2(50f, 50f), size: new Vector2(100f, 100f));

            CollectionAssert.IsEmpty(BuildingOps.GetOverlappingLots(Town(a, b)),
                "Their sprites overlap heavily; their hit boxes do not.");

            var overlaps = BuildingOps.GetOverlappingLots(Town(a, clash));
            Assert.AreEqual(1, overlaps.Count, string.Join(" | ", overlaps));
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

        [Test]
        public void GetUnreachableLots_FindsAGateOnARunThatDoesNotExist()
        {
            var typo = Lot("forge", requiredRuns: new[] { "TutorailRun" });
            var fine = Lot("merchant", requiredRuns: new[] { "TutorialRun" });
            var runs = new List<string> { "TutorialRun" };

            var stuck = BuildingOps.GetUnreachableLots(Town(typo, fine), runs);

            Assert.AreEqual(1, stuck.Count, string.Join(" | ", stuck));
            StringAssert.Contains("forge", stuck[0]);
        }

        [Test]
        public void GetFreeUpgrades_FindsAnUpgradableLotWithNoPrice()
        {
            var free = Lot("merchant", maxLevel: 2);
            var priced = Lot("forge", maxLevel: 2);
            priced.GoldPerUpgrade = 120;

            Assert.AreEqual(new List<string> { "merchant" }, BuildingOps.GetFreeUpgrades(Town(free, priced)));
        }
    }
}
