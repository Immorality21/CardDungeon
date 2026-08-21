using System;

namespace Assets.Scripts.Rooms
{
    [Serializable]
    public class RoomSaveData
    {
        public int RoomIndex;
        public bool IsExplored;
        public int EnemyCount;

        // --- Room event state. A resolved event must stay resolved, or the player re-rolls a bad
        // outcome by walking out and back in, or by quitting to the menu and resuming.
        public bool EventConsumed;

        /// <summary>
        /// SaveKey of the event that was resolved here. Placement is deterministic from the seed, so
        /// the same event lands in the same room on reload - this is the guard for when it does not
        /// (a re-authored pool, a shifted RNG stream), so a consumed flag can never be applied to
        /// somebody else's event.
        /// </summary>
        public string EventKey;

        // Which option, and which outcome from its pool, actually resolved. Stored rather than the
        // outcome's consequences: the outcome is still in the asset at load, so re-spawning the
        // enemies it woke needs the coordinates, not an enemy manifest.
        public int EventOptionIndex = -1;

        public int EventOutcomeIndex = -1;

        public bool EventSucceeded;
    }
}
