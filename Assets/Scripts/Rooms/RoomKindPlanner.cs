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
            var plan = new Dictionary<int, RoomKind>();
            if (eligible == null || eligible.Count == 0 || pick == null)
            {
                return plan;
            }

            // A working copy, because a promoted room is off the table for the next draw: one room,
            // one kind, one thing to do.
            var pool = new List<int>(eligible);

            // Treasure first, then rest. The order is fixed rather than interleaved so that a level
            // whose quotas exceed its eligible rooms degrades predictably - it loses refuges before
            // it loses caches, instead of losing whichever the RNG got to last.
            Draw(pool, treasureRooms, RoomKind.Treasure, plan, pick);
            Draw(pool, restRooms, RoomKind.Rest, plan, pick);

            return plan;
        }

        private static void Draw(
            List<int> pool, int count, RoomKind kind, Dictionary<int, RoomKind> plan, Func<int, int> pick)
        {
            for (int i = 0; i < count && pool.Count > 0; i++)
            {
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
