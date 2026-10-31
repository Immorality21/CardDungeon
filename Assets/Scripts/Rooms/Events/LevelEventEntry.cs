using System;
using UnityEngine;

namespace Assets.Scripts.Rooms.Events
{
    /// <summary>
    /// One event a level can offer, at that level's own odds: the same event can be guaranteed on an
    /// early floor and rare later on. A stronger version of an event on a deeper level is its own
    /// <see cref="RoomEventSO"/> sharing the art and the text, not a setting here.
    /// </summary>
    [Serializable]
    public class LevelEventEntry
    {
        public RoomEventSO Event;

        [Range(0f, 100f)]
        [Tooltip("Percent chance per eligible room. 100 = the first eligible room has it, so every " +
                 "floor does. An event is placed at most once per floor, and the list rolls in order, " +
                 "so keep the rarest first or a guaranteed entry takes the room before they roll.")]
        public float ChancePercent = 10f;
    }
}
