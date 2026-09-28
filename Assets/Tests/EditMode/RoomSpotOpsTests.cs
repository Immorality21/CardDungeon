using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Rooms;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    public class RoomSpotOpsTests
    {
        [Test]
        public void Choose_EmptyRoom_TakesTheTileNearestTheCentre()
        {
            var tiles = RoomSpotOps.Tiles(Vector2Int.zero, 3, 3);

            var spot = RoomSpotOps.Choose(tiles, new List<Vector2>(), new Vector2(1f, 1f));

            Assert.AreEqual(new Vector2(1f, 1f), spot);
        }

        [Test]
        public void Choose_TwoRowRoom_FourOccupantsNeverShareATile()
        {
            // The playtest's room: 5x2, doors at opposite ends. The old inset rule put all four on the centre.
            var tiles = RoomSpotOps.Tiles(new Vector2Int(-5, 3), 5, 2);
            var center = new Vector2(-3f, 3.5f);
            var avoid = new List<Vector2> { new Vector2(-1f, 3f), new Vector2(-5f, 4f) };
            var placed = new List<Vector2>();

            for (int i = 0; i < 4; i++)
            {
                var spot = RoomSpotOps.Choose(tiles, avoid.Concat(placed).ToList(), center);
                placed.Add(spot);
            }

            Assert.AreEqual(4, placed.Distinct().Count());
            CollectionAssert.DoesNotContain(placed, new Vector2(-1f, 3f));
            CollectionAssert.DoesNotContain(placed, new Vector2(-5f, 4f));
        }

        [Test]
        public void Choose_KeepsClearOfTheDoorTheParty_WalksIn()
        {
            var tiles = RoomSpotOps.Tiles(Vector2Int.zero, 4, 1);
            var door = new Vector2(0f, 0f);
            var center = new Vector2(1.5f, 0f);
            var avoid = new List<Vector2> { door, RoomSpotOps.EntrySpot(door, center) };

            var spot = RoomSpotOps.Choose(tiles, avoid, center);

            Assert.AreEqual(new Vector2(3f, 0f), spot);
        }

        [Test]
        public void Choose_MoreOccupantsThanTiles_StillReturnsATileOfTheRoom()
        {
            var tiles = RoomSpotOps.Tiles(Vector2Int.zero, 2, 1);
            var avoid = new List<Vector2>(tiles);

            var spot = RoomSpotOps.Choose(tiles, avoid, new Vector2(0.5f, 0f));

            CollectionAssert.Contains(tiles, spot);
        }

        [Test]
        public void EntrySpot_StepsFromTheDoorTowardTheCentre()
        {
            var spot = RoomSpotOps.EntrySpot(new Vector2(0f, 0f), new Vector2(2f, 0f));

            Assert.AreEqual(RoomSpotOps.EntryStep, spot.x, 0.0001f);
            Assert.AreEqual(0f, spot.y, 0.0001f);
        }
    }
}
