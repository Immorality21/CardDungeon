using System;
using System.Collections.Generic;

namespace Assets.Scripts.Rooms
{
    /// <summary>
    /// Decides which rooms of a generated level become non-combat rooms. Pure and roll-injected, so
    /// the dungeon, the balance model and the tests all agree on how many fights a level actually has.
    ///
    /// <para>Kinds are placed <b>per instance</b>, not per template, for the same reason room events
    /// are: a template used three times in one level must not turn into three treasure rooms. The
    /// level says how <i>many</i> (`LevelDefinitionSO.TreasureRooms` / `RestRooms`), the planner says
    /// <i>which</i>.</para>
    /// </summary>
    public static class RoomKindPlanner
    {
        /// <summary>
        /// Maps room index to the kind it is promoted to. Rooms absent from the result keep the kind
        /// their template gave them.
        /// </summary>
        /// <param name="eligible">
        /// Indices that may be promoted, in a stable order. The caller filters (no start room, no
        /// exit, no connector) so this stays free of room state.
        /// </param>
        /// <param name="pick">
        /// Given a count, returns an index in <c>[0, count)</c> — <c>Random.Range(0, count)</c> at the
        /// call site. Drawn from the dungeon's seeded stream, which is what lets a resumed level
        /// reproduce its own layout.
        /// </param>
        public static Dictionary<int, RoomKind> Plan(
            IList<int> eligible, int treasureRooms, int restRooms, Func<int, int> pick)
        {
            return Plan(eligible, treasureRooms, restRooms, pick, null);
        }

        /// <summary>
        /// As above, but every draw comes from <paramref name="preferred"/> while any of it is left -
        /// the dead ends (playtest 4 finding 56): a room with one door that holds nothing is a walk
        /// for nothing, so a cache or a refuge goes there first. Only once the preferred rooms are
        /// used up does a draw fall back to the other eligible rooms. Preferred indices that are not
        /// eligible are ignored.
        /// </summary>
        public static Dictionary<int, RoomKind> Plan(
            IList<int> eligible, int treasureRooms, int restRooms, Func<int, int> pick, ICollection<int> preferred)
        {
            var plan = new Dictionary<int, RoomKind>();
            if (eligible == null || eligible.Count == 0 || pick == null)
            {
                return plan;
            }

            // Working copies, because a promoted room is off the table for the next draw: one room,
            // one kind, one thing to do.
            var first = new List<int>();
            var rest = new List<int>();
            foreach (int index in eligible)
            {
                if (preferred != null && preferred.Contains(index))
                {
                    first.Add(index);
                }
                else
                {
                    rest.Add(index);
                }
            }

            // Treasure first, then rest. The order is fixed rather than interleaved so that a level
            // whose quotas exceed its eligible rooms degrades predictably - it loses refuges before
            // it loses caches, instead of losing whichever the RNG got to last.
            Draw(first, rest, treasureRooms, RoomKind.Treasure, plan, pick);
            Draw(first, rest, restRooms, RoomKind.Rest, plan, pick);

            return plan;
        }

        /// <summary>
        /// <paramref name="items"/> with the ones <paramref name="isPreferred"/> picks moved to the
        /// front, each half keeping its order (a stable partition). Room events walk the rooms in this
        /// order so a dead end gets the first roll at each event.
        /// </summary>
        public static List<T> PreferredFirst<T>(IEnumerable<T> items, Func<T, bool> isPreferred)
        {
            var first = new List<T>();
            var rest = new List<T>();
            if (items == null)
            {
                return first;
            }
            foreach (var item in items)
            {
                if (isPreferred != null && isPreferred(item))
                {
                    first.Add(item);
                }
                else
                {
                    rest.Add(item);
                }
            }
            first.AddRange(rest);
            return first;
        }

        private static void Draw(
            List<int> first, List<int> rest, int count, RoomKind kind, Dictionary<int, RoomKind> plan, Func<int, int> pick)
        {
            for (int i = 0; i < count && (first.Count > 0 || rest.Count > 0); i++)
            {
                var pool = first.Count > 0 ? first : rest;
                int index = pick(pool.Count);
                if (index < 0 || index >= pool.Count)
                {
                    // A roll outside the pool would silently place the kind in the wrong room; clamp
                    // rather than throw, because this runs mid-generation.
                    index = pool.Count - 1;
                }

                plan[pool[index]] = kind;
                pool.RemoveAt(index);
            }
        }
    }
}
