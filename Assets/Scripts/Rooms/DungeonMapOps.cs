using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.Rooms
{
    /// <summary>How much the player knows about one room on the map.</summary>
    public enum MapRoomState
    {
        /// <summary>Never seen and not adjacent to anything seen - not drawn at all.</summary>
        Hidden = 0,
        /// <summary>Not visited, but a door from an explored room leads here. Drawn as an empty outline.</summary>
        Known = 1,
        /// <summary>Visited. Drawn filled, with whatever it still holds.</summary>
        Explored = 2,
    }

    /// <summary>
    /// The one thing a room's centre glyph says. Exactly one per room, by the priority in
    /// <see cref="DungeonMapOps.MarkerFor"/> - "what is still in there" is a single answer, and a box
    /// this small has room for a single character.
    /// </summary>
    public enum MapMarker
    {
        None = 0,
        /// <summary>Enemies still standing. Beats everything: it is what stops the party walking in.</summary>
        Enemies = 1,
        /// <summary>A captive hero waiting to be freed.</summary>
        Captive = 2,
        /// <summary>An unresolved room event.</summary>
        Event = 3,
        /// <summary>An unsearched cache.</summary>
        Cache = 4,
        /// <summary>An unspent refuge.</summary>
        Refuge = 5,
    }

    /// <summary>
    /// What the map needs to know about one room. A plain struct rather than a <c>Room</c>, so the
    /// reveal rules can be tested without a scene - <c>DungeonMapView</c> reads the MonoBehaviours
    /// and fills these in.
    /// </summary>
    public struct MapRoomInput
    {
        public int Index;
        /// <summary>Room footprint in dungeon grid units (<c>Room.GridPosition</c> plus the template's size).</summary>
        public RectInt Bounds;
        public bool IsExplored;
        public bool IsExit;
        public bool HasEnemies;
        public bool HasCaptive;
        public bool HasPendingEvent;
        public bool HasPendingCache;
        public bool HasPendingRefuge;
        /// <summary>Indices of the rooms this one has a door to.</summary>
        public List<int> Neighbours;
    }

    /// <summary>One drawn room.</summary>
    public class MapRoom
    {
        public int Index;
        public RectInt Bounds;
        public MapRoomState State;
        public MapMarker Marker;
        public bool IsCurrent;
        /// <summary>Whether to draw the descend glyph. Only ever true for an explored room.</summary>
        public bool ShowsExit;
        /// <summary>
        /// Whether clicking this room travels the party there. See
        /// <see cref="DungeonMapOps.TravellableRooms"/> for what makes a room reachable.
        /// </summary>
        public bool CanTravel;
    }

    /// <summary>One drawn door, as the pair of rooms it joins.</summary>
    public class MapLink
    {
        public int A;
        public int B;
        /// <summary>True when both ends are explored - a route the party has actually walked.</summary>
        public bool BothExplored;
    }

    /// <summary>Everything the map draws, plus the counts its status line reads.</summary>
    public class DungeonMapModel
    {
        public List<MapRoom> Rooms = new List<MapRoom>();
        public List<MapLink> Links = new List<MapLink>();
        /// <summary>Bounding box of every drawn room, in grid units. Zero-sized when nothing is drawn.</summary>
        public RectInt Extent;
        public int ExploredCount;
        /// <summary>Rooms a known door leads to that the party has not entered - the frontier.</summary>
        public int FrontierCount;
        /// <summary>Whether the exit has been found. The map never spoils it before then.</summary>
        public bool ExitFound;
        /// <summary>How many rooms fast travel can currently reach.</summary>
        public int TravelCount;
        /// <summary>
        /// Whether travel is off because live enemies hold the room the party is standing in. Told
        /// to the player as such - "nowhere to go" and "you may not leave" are different sentences.
        /// </summary>
        public bool TravelBlockedByEnemies;
    }

    /// <summary>
    /// Turns the floor into the map the player is allowed to see.
    ///
    /// <para>The knowledge model is already in the scene - <c>Room.IsExplored</c>, and
    /// <c>Room.Hide</c>'s rule that a door renderer stays visible when the room on the *other* side
    /// has been explored - so this mirrors it rather than inventing a second one. A room is drawn if
    /// it has been entered, or if a door from an entered room leads to it; a door is drawn if either
    /// of its rooms has been entered. Everything else is <see cref="MapRoomState.Hidden"/> and simply
    /// absent, so the map can never answer a question standing in the dungeon would not.</para>
    ///
    /// <para>Content is reported for explored rooms only, which is what keeps the frontier honest: an
    /// unvisited neighbour is an outline saying a room is there, and nothing at all about what is in
    /// it.</para>
    /// </summary>
    public static class DungeonMapOps
    {
        /// <summary>The descend glyph, drawn in the exit room's corner so it never competes with a marker.</summary>
        public const string ExitGlyph = "▼";

        /// <summary>Builds the model. <paramref name="currentIndex"/> may be -1 (no current room).</summary>
        public static DungeonMapModel Build(IReadOnlyList<MapRoomInput> rooms, int currentIndex)
        {
            var model = new DungeonMapModel();

            if (rooms == null || rooms.Count == 0)
            {
                return model;
            }

            var byIndex = new Dictionary<int, MapRoomInput>(rooms.Count);
            var states = new Dictionary<int, MapRoomState>(rooms.Count);
            foreach (var room in rooms)
            {
                byIndex[room.Index] = room;
                states[room.Index] = room.IsExplored ? MapRoomState.Explored : MapRoomState.Hidden;
            }

            // Promotion is walked from the explored side only: a neighbour of an explored room is
            // Known even though it has never been entered, and a chain of unexplored rooms behind it
            // stays Hidden.
            foreach (var room in rooms)
            {
                if (!room.IsExplored || room.Neighbours == null)
                {
                    continue;
                }

                foreach (int neighbour in room.Neighbours)
                {
                    if (!states.TryGetValue(neighbour, out var state) || state != MapRoomState.Hidden)
                    {
                        continue;
                    }
                    states[neighbour] = MapRoomState.Known;
                }
            }

            var travellable = TravellableRooms(rooms, currentIndex);
            model.TravelCount = travellable.Count;
            model.TravelBlockedByEnemies = byIndex.TryGetValue(currentIndex, out var standingIn)
                && standingIn.HasEnemies;

            bool haveExtent = false;
            int minX = 0, minY = 0, maxX = 0, maxY = 0;

            foreach (var room in rooms)
            {
                var state = states[room.Index];
                if (state == MapRoomState.Hidden)
                {
                    continue;
                }

                bool explored = state == MapRoomState.Explored;
                model.Rooms.Add(new MapRoom
                {
                    Index = room.Index,
                    Bounds = room.Bounds,
                    State = state,
                    Marker = explored ? MarkerFor(room) : MapMarker.None,
                    IsCurrent = room.Index == currentIndex,
                    ShowsExit = explored && room.IsExit,
                    CanTravel = travellable.Contains(room.Index),
                });

                if (explored)
                {
                    model.ExploredCount++;
                    if (room.IsExit)
                    {
                        model.ExitFound = true;
                    }
                }
                else
                {
                    model.FrontierCount++;
                }

                if (!haveExtent)
                {
                    minX = room.Bounds.xMin;
                    minY = room.Bounds.yMin;
                    maxX = room.Bounds.xMax;
                    maxY = room.Bounds.yMax;
                    haveExtent = true;
                    continue;
                }

                minX = Mathf.Min(minX, room.Bounds.xMin);
                minY = Mathf.Min(minY, room.Bounds.yMin);
                maxX = Mathf.Max(maxX, room.Bounds.xMax);
                maxY = Mathf.Max(maxY, room.Bounds.yMax);
            }

            model.Extent = haveExtent
                ? new RectInt(minX, minY, maxX - minX, maxY - minY)
                : new RectInt(0, 0, 0, 0);

            // Each door once, from the lower index, and only where the party has stood on one side.
            foreach (var room in rooms)
            {
                if (room.Neighbours == null)
                {
                    continue;
                }

                foreach (int neighbour in room.Neighbours)
                {
                    if (neighbour <= room.Index || !byIndex.TryGetValue(neighbour, out var other))
                    {
                        continue;
                    }
                    if (!room.IsExplored && !other.IsExplored)
                    {
                        continue;
                    }

                    model.Links.Add(new MapLink
                    {
                        A = room.Index,
                        B = neighbour,
                        BothExplored = room.IsExplored && other.IsExplored,
                    });
                }
            }

            return model;
        }

        /// <summary>
        /// Every room fast travel can reach from where the party is standing, and the rules that
        /// decide it. Travel exists to delete the walk back through rooms already dealt with, so it
        /// may never buy anything a walk could not.
        ///
        /// <para>Three rules, and the first two are the ones that matter:</para>
        /// <list type="bullet">
        /// <item><b>Live enemies hold the room the party is in: nothing is travellable at all.</b>
        /// The dungeon already disables that room's doors, and combat offers Flee for exactly this -
        /// so a map that let the party step out would be a free Flee with none of its cost.</item>
        /// <item><b>A route may not pass through an enemy-held room.</b> Walking cannot, because the
        /// room seals the moment the party enters it. A room with enemies is still a legal
        /// <i>destination</i> - going back to finish a fight is a real choice - it just cannot be
        /// walked through on the way to somewhere else.</item>
        /// <item><b>Only explored rooms.</b> The frontier is an outline, not a place, and travelling
        /// into a room nobody has entered would skip the floor rather than re-cross it.</item>
        /// </list>
        /// </summary>
        public static List<int> TravellableRooms(IReadOnlyList<MapRoomInput> rooms, int currentIndex)
        {
            var reachable = new List<int>();
            if (rooms == null || rooms.Count == 0)
            {
                return reachable;
            }

            var byIndex = new Dictionary<int, MapRoomInput>(rooms.Count);
            foreach (var room in rooms)
            {
                byIndex[room.Index] = room;
            }

            if (!byIndex.TryGetValue(currentIndex, out var start) || start.HasEnemies)
            {
                return reachable;
            }

            var seen = new List<int> { currentIndex };
            var queue = new Queue<int>();
            queue.Enqueue(currentIndex);

            while (queue.Count > 0)
            {
                int index = queue.Dequeue();
                var room = byIndex[index];

                // Terminal: reachable, but nothing can be reached *through* it.
                if (index != currentIndex && room.HasEnemies)
                {
                    continue;
                }

                if (room.Neighbours == null)
                {
                    continue;
                }

                foreach (int neighbour in room.Neighbours)
                {
                    if (seen.Contains(neighbour) || !byIndex.TryGetValue(neighbour, out var next))
                    {
                        continue;
                    }
                    if (!next.IsExplored)
                    {
                        continue;
                    }

                    seen.Add(neighbour);
                    reachable.Add(neighbour);
                    queue.Enqueue(neighbour);
                }
            }

            return reachable;
        }

        /// <summary>
        /// The rooms to walk, from the party's room through to <paramref name="targetIndex"/>
        /// inclusive, or an empty list when there is no legal route. Same rules as
        /// <see cref="TravellableRooms"/> - this is the shortest one of them, which is what the
        /// caller needs because the party arrives through the <i>last</i> door on it and must end up
        /// with the same entry door, previous room and flee route a walk would have left.
        /// </summary>
        public static List<int> RouteTo(
            IReadOnlyList<MapRoomInput> rooms, int currentIndex, int targetIndex)
        {
            var route = new List<int>();
            if (rooms == null || rooms.Count == 0 || currentIndex == targetIndex)
            {
                return route;
            }

            var byIndex = new Dictionary<int, MapRoomInput>(rooms.Count);
            foreach (var room in rooms)
            {
                byIndex[room.Index] = room;
            }

            if (!byIndex.TryGetValue(currentIndex, out var start) || start.HasEnemies
                || !byIndex.ContainsKey(targetIndex))
            {
                return route;
            }

            var cameFrom = new Dictionary<int, int>();
            var seen = new List<int> { currentIndex };
            var queue = new Queue<int>();
            queue.Enqueue(currentIndex);
            bool found = false;

            while (queue.Count > 0 && !found)
            {
                int index = queue.Dequeue();
                var room = byIndex[index];

                if (index != currentIndex && room.HasEnemies)
                {
                    continue;
                }
                if (room.Neighbours == null)
                {
                    continue;
                }

                foreach (int neighbour in room.Neighbours)
                {
                    if (seen.Contains(neighbour) || !byIndex.TryGetValue(neighbour, out var next)
                        || !next.IsExplored)
                    {
                        continue;
                    }

                    seen.Add(neighbour);
                    cameFrom[neighbour] = index;
                    if (neighbour == targetIndex)
                    {
                        found = true;
                        break;
                    }
                    queue.Enqueue(neighbour);
                }
            }

            if (!found)
            {
                return route;
            }

            for (int step = targetIndex; step != currentIndex; step = cameFrom[step])
            {
                route.Insert(0, step);
            }
            route.Insert(0, currentIndex);
            return route;
        }

        /// <summary>
        /// The line under the map. It says what travel is offering, and when travel is off it says
        /// <i>why</i> rather than going quiet - "nowhere to go" and "you may not leave" are different
        /// facts and the player can act on only one of them.
        /// </summary>
        public static string TravelHint(DungeonMapModel model)
        {
            if (model == null || model.Rooms.Count == 0)
            {
                return "";
            }
            if (model.TravelBlockedByEnemies)
            {
                return "Enemies hold this room - clear them or flee before travelling.";
            }
            if (model.TravelCount == 0)
            {
                return "Nowhere to travel yet - the floor beyond this room is unexplored.";
            }
            return "Click an explored room, or arrows then Enter, to travel there instantly.";
        }

        /// <summary>
        /// The one glyph an explored room shows. Ordered by what the player would act on first: a
        /// fight blocks the room outright, a captive is the only unrepeatable thing on a floor, an
        /// event is a decision, and the two payloads are only resources.
        /// </summary>
        public static MapMarker MarkerFor(MapRoomInput room)
        {
            if (room.HasEnemies)
            {
                return MapMarker.Enemies;
            }
            if (room.HasCaptive)
            {
                return MapMarker.Captive;
            }
            if (room.HasPendingEvent)
            {
                return MapMarker.Event;
            }
            if (room.HasPendingCache)
            {
                return MapMarker.Cache;
            }
            if (room.HasPendingRefuge)
            {
                return MapMarker.Refuge;
            }
            return MapMarker.None;
        }

        /// <summary>The character drawn in a room's centre. Beside the enum so the legend cannot drift.</summary>
        public static string GlyphFor(MapMarker marker)
        {
            switch (marker)
            {
                case MapMarker.Enemies:
                    return "✖";
                case MapMarker.Captive:
                    return "✦";
                case MapMarker.Event:
                    return "?";
                case MapMarker.Cache:
                    return "◆";
                case MapMarker.Refuge:
                    return "✚";
                default:
                    return "";
            }
        }

        /// <summary>
        /// The status line: what has been walked, and how much is still open. Deliberately does not
        /// name the floor's total room count - "have I searched everything" is answered by the
        /// frontier reaching zero, and a total would tell the player how big the floor is before they
        /// have walked it.
        /// </summary>
        public static string StatusLine(DungeonMapModel model)
        {
            if (model == null || model.Rooms.Count == 0)
            {
                return "";
            }

            string rooms = model.ExploredCount == 1
                ? "1 room explored"
                : string.Format("{0} rooms explored", model.ExploredCount);
            string frontier = model.FrontierCount == 0
                ? "nothing left unopened"
                : model.FrontierCount == 1
                    ? "1 way not yet taken"
                    : string.Format("{0} ways not yet taken", model.FrontierCount);
            string exit = model.ExitFound ? " · exit found" : "";
            return string.Format("{0} · {1}{2}", rooms, frontier, exit);
        }
    }
}
