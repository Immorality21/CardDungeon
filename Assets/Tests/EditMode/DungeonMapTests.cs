using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Rooms;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// The dungeon map's reveal rules (<see cref="DungeonMapOps"/>). All pure - rooms are plain
    /// <see cref="MapRoomInput"/> structs, no scene and no MonoBehaviours - which is the whole reason
    /// the rules live outside the view: what the map is *allowed to show* is the part that can leak a
    /// floor's contents, so it is the part that gets tested.
    /// </summary>
    public class DungeonMapTests
    {
        /// <summary>A room in a straight line: index i sits at x = i * 8, so nothing overlaps.</summary>
        private static MapRoomInput Room(
            int index,
            bool explored = false,
            bool isExit = false,
            bool enemies = false,
            bool captive = false,
            bool pendingEvent = false,
            bool cache = false,
            bool refuge = false,
            int[] neighbours = null)
        {
            return new MapRoomInput
            {
                Index = index,
                Bounds = new RectInt(index * 8, 0, 5, 4),
                IsExplored = explored,
                IsExit = isExit,
                HasEnemies = enemies,
                HasCaptive = captive,
                HasPendingEvent = pendingEvent,
                HasPendingCache = cache,
                HasPendingRefuge = refuge,
                Neighbours = neighbours != null ? new List<int>(neighbours) : new List<int>(),
            };
        }

        private static MapRoom Find(DungeonMapModel model, int index)
        {
            return model.Rooms.FirstOrDefault(r => r.Index == index);
        }

        [Test]
        public void Build_NoRooms_IsEmpty()
        {
            var model = DungeonMapOps.Build(new List<MapRoomInput>(), -1);

            Assert.IsEmpty(model.Rooms);
            Assert.IsEmpty(model.Links);
            Assert.AreEqual(0, model.Extent.width);
            Assert.AreEqual("", DungeonMapOps.StatusLine(model));
        }

        [Test]
        public void Build_NullRooms_IsEmptyRatherThanThrowing()
        {
            var model = DungeonMapOps.Build(null, 0);

            Assert.IsEmpty(model.Rooms);
        }

        [Test]
        public void UnvisitedNeighbourOfAnExploredRoom_IsKnown()
        {
            var model = DungeonMapOps.Build(new List<MapRoomInput>
            {
                Room(0, explored: true, neighbours: new[] { 1 }),
                Room(1, neighbours: new[] { 0 }),
            }, 0);

            Assert.AreEqual(MapRoomState.Explored, Find(model, 0).State);
            Assert.AreEqual(MapRoomState.Known, Find(model, 1).State);
            Assert.AreEqual(1, model.ExploredCount);
            Assert.AreEqual(1, model.FrontierCount);
        }

        /// <summary>
        /// The reveal is one step deep, not a flood fill: the room behind the frontier is not drawn
        /// at all. This is the rule that keeps the map from surveying the floor from the doorway.
        /// </summary>
        [Test]
        public void RoomBeyondTheFrontier_IsNotDrawnAtAll()
        {
            var model = DungeonMapOps.Build(new List<MapRoomInput>
            {
                Room(0, explored: true, neighbours: new[] { 1 }),
                Room(1, neighbours: new[] { 0, 2 }),
                Room(2, neighbours: new[] { 1 }),
            }, 0);

            Assert.IsNotNull(Find(model, 1));
            Assert.IsNull(Find(model, 2), "a room two doors out has never been seen");
            Assert.AreEqual(2, model.Rooms.Count);
        }

        [Test]
        public void UnexploredRoom_ReportsNoneOfItsContents()
        {
            var model = DungeonMapOps.Build(new List<MapRoomInput>
            {
                Room(0, explored: true, neighbours: new[] { 1 }),
                Room(1, enemies: true, cache: true, pendingEvent: true, isExit: true, neighbours: new[] { 0 }),
            }, 0);

            var frontier = Find(model, 1);
            Assert.AreEqual(MapMarker.None, frontier.Marker, "an outline must not say what is inside");
            Assert.IsFalse(frontier.ShowsExit);
            Assert.IsFalse(model.ExitFound);
        }

        [Test]
        public void Exit_IsShownOnceItsRoomIsExplored()
        {
            var model = DungeonMapOps.Build(new List<MapRoomInput>
            {
                Room(0, explored: true, neighbours: new[] { 1 }),
                Room(1, explored: true, isExit: true, neighbours: new[] { 0 }),
            }, 0);

            Assert.IsTrue(Find(model, 1).ShowsExit);
            Assert.IsTrue(model.ExitFound);
        }

        [Test]
        public void Door_WithNeitherEndExplored_IsNotDrawn()
        {
            // 1 and 2 are both frontier rooms off the same explored room, and the door between them
            // has never been stood beside.
            var model = DungeonMapOps.Build(new List<MapRoomInput>
            {
                Room(0, explored: true, neighbours: new[] { 1, 2 }),
                Room(1, neighbours: new[] { 0, 2 }),
                Room(2, neighbours: new[] { 0, 1 }),
            }, 0);

            Assert.IsFalse(model.Links.Any(l => (l.A == 1 && l.B == 2) || (l.A == 2 && l.B == 1)));
            Assert.AreEqual(2, model.Links.Count);
        }

        [Test]
        public void Door_WithOneEndExplored_IsDrawnButNotAsWalked()
        {
            var model = DungeonMapOps.Build(new List<MapRoomInput>
            {
                Room(0, explored: true, neighbours: new[] { 1 }),
                Room(1, neighbours: new[] { 0 }),
            }, 0);

            Assert.AreEqual(1, model.Links.Count);
            Assert.IsFalse(model.Links[0].BothExplored);
        }

        [Test]
        public void Door_BetweenTwoExploredRooms_IsWalked()
        {
            var model = DungeonMapOps.Build(new List<MapRoomInput>
            {
                Room(0, explored: true, neighbours: new[] { 1 }),
                Room(1, explored: true, neighbours: new[] { 0 }),
            }, 1);

            Assert.AreEqual(1, model.Links.Count);
            Assert.IsTrue(model.Links[0].BothExplored);
        }

        [Test]
        public void Doors_AreDrawnOncePerPair_NotOncePerSide()
        {
            var model = DungeonMapOps.Build(new List<MapRoomInput>
            {
                Room(0, explored: true, neighbours: new[] { 1 }),
                Room(1, explored: true, neighbours: new[] { 0 }),
            }, 0);

            Assert.AreEqual(1, model.Links.Count);
        }

        [Test]
        public void CurrentRoom_IsFlaggedAndOnlyOnce()
        {
            var model = DungeonMapOps.Build(new List<MapRoomInput>
            {
                Room(0, explored: true, neighbours: new[] { 1 }),
                Room(1, explored: true, neighbours: new[] { 0 }),
            }, 1);

            Assert.IsFalse(Find(model, 0).IsCurrent);
            Assert.IsTrue(Find(model, 1).IsCurrent);
        }

        [Test]
        public void Marker_PrefersEnemiesOverEveryOtherPayload()
        {
            var room = Room(0, explored: true, enemies: true, captive: true, pendingEvent: true,
                cache: true, refuge: true);

            Assert.AreEqual(MapMarker.Enemies, DungeonMapOps.MarkerFor(room));
        }

        [Test]
        public void Marker_FallsThroughTheAuthoredPriority()
        {
            Assert.AreEqual(MapMarker.Captive,
                DungeonMapOps.MarkerFor(Room(0, captive: true, pendingEvent: true, cache: true)));
            Assert.AreEqual(MapMarker.Event,
                DungeonMapOps.MarkerFor(Room(0, pendingEvent: true, cache: true, refuge: true)));
            Assert.AreEqual(MapMarker.Cache,
                DungeonMapOps.MarkerFor(Room(0, cache: true, refuge: true)));
            Assert.AreEqual(MapMarker.Refuge, DungeonMapOps.MarkerFor(Room(0, refuge: true)));
            Assert.AreEqual(MapMarker.None, DungeonMapOps.MarkerFor(Room(0)));
        }

        /// <summary>Every marker the enum can produce has a glyph, or the legend is lying.</summary>
        [Test]
        public void EveryMarker_HasAGlyph()
        {
            foreach (MapMarker marker in System.Enum.GetValues(typeof(MapMarker)))
            {
                if (marker == MapMarker.None)
                {
                    Assert.AreEqual("", DungeonMapOps.GlyphFor(marker));
                    continue;
                }
                Assert.IsNotEmpty(DungeonMapOps.GlyphFor(marker), marker + " has no glyph");
            }
        }

        [Test]
        public void Extent_CoversTheDrawnRoomsOnly()
        {
            // Room 5 is far to the right and has never been seen, so it must not stretch the fit.
            var model = DungeonMapOps.Build(new List<MapRoomInput>
            {
                Room(0, explored: true, neighbours: new[] { 1 }),
                Room(1, neighbours: new[] { 0 }),
                Room(5),
            }, 0);

            Assert.AreEqual(0, model.Extent.xMin);
            Assert.AreEqual(13, model.Extent.xMax, "room 1 ends at x = 8 + 5");
            Assert.AreEqual(4, model.Extent.height);
        }

        [Test]
        public void StatusLine_NamesTheFrontierAndNeverTheFloorSize()
        {
            var partway = DungeonMapOps.Build(new List<MapRoomInput>
            {
                Room(0, explored: true, neighbours: new[] { 1 }),
                Room(1, neighbours: new[] { 0, 2 }),
                Room(2, neighbours: new[] { 1 }),
            }, 0);

            string line = DungeonMapOps.StatusLine(partway);
            Assert.That(line, Does.Contain("1 room explored"));
            Assert.That(line, Does.Contain("1 way not yet taken"));
            Assert.That(line, Does.Not.Contain("3"), "the floor's room count is not the player's yet");
        }

        [Test]
        public void StatusLine_WithNothingLeft_SaysSo()
        {
            var done = DungeonMapOps.Build(new List<MapRoomInput>
            {
                Room(0, explored: true, neighbours: new[] { 1 }),
                Room(1, explored: true, isExit: true, neighbours: new[] { 0 }),
            }, 1);

            string line = DungeonMapOps.StatusLine(done);
            Assert.That(line, Does.Contain("2 rooms explored"));
            Assert.That(line, Does.Contain("nothing left unopened"));
            Assert.That(line, Does.Contain("exit found"));
        }

        // --- fast travel --------------------------------------------------------------

        /// <summary>
        /// The rule that keeps travel from being an exploit: the doors of a room with live enemies
        /// are disabled in the dungeon and combat offers Flee for leaving it, so a map that let the
        /// party step out would be a free Flee with none of its cost.
        /// </summary>
        [Test]
        public void Travel_OutOfARoomEnemiesHold_IsRefusedEntirely()
        {
            var rooms = new List<MapRoomInput>
            {
                Room(0, explored: true, enemies: true, neighbours: new[] { 1 }),
                Room(1, explored: true, neighbours: new[] { 0 }),
            };

            Assert.IsEmpty(DungeonMapOps.TravellableRooms(rooms, 0));
            Assert.IsEmpty(DungeonMapOps.RouteTo(rooms, 0, 1));

            var model = DungeonMapOps.Build(rooms, 0);
            Assert.IsTrue(model.TravelBlockedByEnemies);
            Assert.AreEqual(0, model.TravelCount);
            Assert.That(DungeonMapOps.TravelHint(model), Does.Contain("Enemies hold this room"));
        }

        /// <summary>
        /// A room with enemies is a legal destination - going back to finish a fight is a real
        /// choice - but it cannot be walked *through*, because the room seals as the party enters.
        /// </summary>
        [Test]
        public void Travel_ReachesAnEnemyHeldRoom_ButNotPastIt()
        {
            var rooms = new List<MapRoomInput>
            {
                Room(0, explored: true, neighbours: new[] { 1 }),
                Room(1, explored: true, enemies: true, neighbours: new[] { 0, 2 }),
                Room(2, explored: true, neighbours: new[] { 1 }),
            };

            var travellable = DungeonMapOps.TravellableRooms(rooms, 0);
            Assert.Contains(1, travellable, "the fight itself is a place the party may walk to");
            CollectionAssert.DoesNotContain(travellable, 2, "nothing routes through a sealed room");
            Assert.IsEmpty(DungeonMapOps.RouteTo(rooms, 0, 2));
        }

        [Test]
        public void Travel_NeverReachesTheFrontier()
        {
            var rooms = new List<MapRoomInput>
            {
                Room(0, explored: true, neighbours: new[] { 1 }),
                Room(1, neighbours: new[] { 0 }),
            };

            Assert.IsEmpty(DungeonMapOps.TravellableRooms(rooms, 0));
            Assert.IsFalse(DungeonMapOps.Build(rooms, 0).Rooms.Any(r => r.CanTravel));
        }

        [Test]
        public void Travel_NeverIncludesTheRoomThePartyIsIn()
        {
            var rooms = new List<MapRoomInput>
            {
                Room(0, explored: true, neighbours: new[] { 1 }),
                Room(1, explored: true, neighbours: new[] { 0 }),
            };

            Assert.That(DungeonMapOps.TravellableRooms(rooms, 1), Is.EquivalentTo(new[] { 0 }));
            Assert.IsEmpty(DungeonMapOps.RouteTo(rooms, 1, 1));
        }

        [Test]
        public void Travel_CrossesSeveralClearedRooms()
        {
            var rooms = new List<MapRoomInput>
            {
                Room(0, explored: true, neighbours: new[] { 1 }),
                Room(1, explored: true, neighbours: new[] { 0, 2 }),
                Room(2, explored: true, neighbours: new[] { 1, 3 }),
                Room(3, explored: true, neighbours: new[] { 2 }),
            };

            Assert.That(DungeonMapOps.TravellableRooms(rooms, 0), Is.EquivalentTo(new[] { 1, 2, 3 }));
            Assert.That(DungeonMapOps.RouteTo(rooms, 0, 3), Is.EqualTo(new[] { 0, 1, 2, 3 }));
        }

        /// <summary>
        /// The route has to be the shortest one, because the party arrives through its <i>last</i>
        /// door - a longer route would put them in the right room through the wrong doorway, and
        /// leave Flee pointing somewhere they never stood.
        /// </summary>
        [Test]
        public void RouteTo_TakesTheShortWayWhenThereAreTwo()
        {
            // 0-1-3 is two hops; 0-2-4-3 is three.
            var rooms = new List<MapRoomInput>
            {
                Room(0, explored: true, neighbours: new[] { 1, 2 }),
                Room(1, explored: true, neighbours: new[] { 0, 3 }),
                Room(2, explored: true, neighbours: new[] { 0, 4 }),
                Room(3, explored: true, neighbours: new[] { 1, 4 }),
                Room(4, explored: true, neighbours: new[] { 2, 3 }),
            };

            Assert.That(DungeonMapOps.RouteTo(rooms, 0, 3), Is.EqualTo(new[] { 0, 1, 3 }));
        }

        [Test]
        public void RouteTo_ARoomWithNoLegalWayThere_IsEmpty()
        {
            // 2 is explored but only reachable through the unexplored 1.
            var rooms = new List<MapRoomInput>
            {
                Room(0, explored: true, neighbours: new[] { 1 }),
                Room(1, neighbours: new[] { 0, 2 }),
                Room(2, explored: true, neighbours: new[] { 1 }),
            };

            Assert.IsEmpty(DungeonMapOps.RouteTo(rooms, 0, 2));
            CollectionAssert.DoesNotContain(DungeonMapOps.TravellableRooms(rooms, 0), 2);
        }

        [Test]
        public void TravelHint_SaysWhyThereIsNowhereToGo()
        {
            var alone = DungeonMapOps.Build(new List<MapRoomInput>
            {
                Room(0, explored: true, neighbours: new[] { 1 }),
                Room(1, neighbours: new[] { 0 }),
            }, 0);
            Assert.That(DungeonMapOps.TravelHint(alone), Does.Contain("Nowhere to travel"));

            var open = DungeonMapOps.Build(new List<MapRoomInput>
            {
                Room(0, explored: true, neighbours: new[] { 1 }),
                Room(1, explored: true, neighbours: new[] { 0 }),
            }, 0);
            Assert.That(DungeonMapOps.TravelHint(open), Does.Contain("travel there instantly"));
        }

        /// <summary>
        /// A neighbour index that names no room (a door onto nothing) must not crash the build or
        /// invent a room - generation can leave a connection without a placed door.
        /// </summary>
        [Test]
        public void NeighbourIndex_ThatNamesNoRoom_IsIgnored()
        {
            var model = DungeonMapOps.Build(new List<MapRoomInput>
            {
                Room(0, explored: true, neighbours: new[] { 99 }),
            }, 0);

            Assert.AreEqual(1, model.Rooms.Count);
            Assert.IsEmpty(model.Links);
        }
    }
}
