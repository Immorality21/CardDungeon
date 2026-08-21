using System;
using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.Rooms.Events
{
    /// <summary>How an option resolves when the player picks it.</summary>
    public enum RoomEventOptionKind
    {
        /// <summary>Rolled against the event's governing stat: <see cref="RoomEventOption.Success"/>
        /// on a pass, <see cref="RoomEventOption.Failure"/> on a miss. Consumes the event.</summary>
        StatCheck = 0,

        /// <summary>No roll - the success pool always applies. For a known trade: pull the lever and
        /// pay the price. Consumes the event.</summary>
        Guaranteed = 1,

        /// <summary>Walk away. Shows <see cref="DeclineText"/> and leaves the event <b>unconsumed</b>,
        /// so a party can come back for it later; the choice is deferred, not spent.</summary>
        Decline = 2
    }

    /// <summary>One choice offered by a <see cref="RoomEventSO"/> - "Pick it up" / "Leave it".</summary>
    [Serializable]
    public class RoomEventOption
    {
        [Tooltip("The button text. Phrase it as the action, not the outcome.")]
        public string Label;

        public RoomEventOptionKind Kind = RoomEventOptionKind.StatCheck;

        [TextArea]
        [Tooltip("Shown for a Decline option. Ignored by the other kinds.")]
        public string DeclineText;

        [Tooltip("Weighted pool for a passed check (and the only pool a Guaranteed option uses).")]
        public List<RoomEventOutcome> Success = new List<RoomEventOutcome>();

        [Tooltip("Weighted pool for a failed check. Failure costs the party something; it never " +
                 "ends the run.")]
        public List<RoomEventOutcome> Failure = new List<RoomEventOutcome>();

        /// <summary>Whether resolving this option spends the event for good.</summary>
        public bool ConsumesEvent
        {
            get { return Kind != RoomEventOptionKind.Decline; }
        }
    }
}
