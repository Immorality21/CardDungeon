using System.Collections.Generic;

namespace Assets.Scripts.Rooms.Events
{
    /// <summary>What actually happened when an outcome was applied, ready for the result window.</summary>
    public class RoomEventOutcomeReport
    {
        /// <summary>The outcome's authored copy.</summary>
        public string Text;

        public bool Succeeded;

        /// <summary>
        /// One line per concrete consequence - damage taken, gear found, gold banked. Kept separate
        /// from <see cref="Text"/> so the fiction and the bookkeeping read as two things.
        /// </summary>
        public List<string> Lines = new List<string>();

        /// <summary>
        /// True when the outcome woke something up, so the caller knows to re-show the room: a safe
        /// room has just become a fight.
        /// </summary>
        public bool SpawnedEnemies;
    }
}
