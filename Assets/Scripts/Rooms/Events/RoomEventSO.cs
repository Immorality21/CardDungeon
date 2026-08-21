using System.Collections.Generic;
using Assets.Scripts.UnitStats;
using UnityEngine;

namespace Assets.Scripts.Rooms.Events
{
    /// <summary>Which room button offers the event; the other one keeps its flavour text.</summary>
    public enum RoomEventTrigger
    {
        /// <summary>Something the party notices - a tome, a carving, a glint in the water.</summary>
        Examine = 0,

        /// <summary>Something the party does to the room - a lever, a door, a body.</summary>
        Action = 1
    }

    /// <summary>
    /// A gamble the party's stats resolve, offered by a room. This is what turns
    /// <c>RoomSO.ExamineOptions</c> / <c>ActionOptions</c> from flavour strings into a decision:
    /// the option list gains one real entry, and taking it costs or pays something.
    ///
    /// <para><b>Which stat is part of the event's identity.</b> Agility for acrobatics,
    /// Intelligence for knowledge, Spirit for the consecrated and the cursed, Luck for blind risk,
    /// Strength or Endurance where force is the answer. The check reads the party's <i>best</i>
    /// hero at that stat, so bringing a specialist is worth a party slot.</para>
    ///
    /// <para>Odds are never shown as a number - see <see cref="RoomEventResolver"/>, which also
    /// makes the governing stat buy <i>information</i>: a dull party cannot judge the runes it is
    /// standing in front of.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "SO/Room Event")]
    public class RoomEventSO : ScriptableObject
    {
        [Tooltip("Stable id, written into the dungeon save so a consumed event stays consumed. " +
                 "Falls back to the asset name when blank, so renaming the asset is the only thing " +
                 "that can break an existing save.")]
        public string Key;

        [Tooltip("Window title, and the label of the entry added to the room's option list.")]
        public string Title;

        [TextArea]
        [Tooltip("The fiction. Set the scene and imply the risk; the odds line states it.")]
        public string Prompt;

        public RoomEventTrigger Trigger = RoomEventTrigger.Examine;

        [Tooltip("The stat every StatCheck option on this event is resolved against.")]
        public StatType GoverningStat = StatType.Luck;

        [Tooltip("The stat value at which a check is an even bet. Above it the party is favoured, " +
                 "below it they are not - and a party under half this cannot read the odds at all.")]
        public int Difficulty = 8;

        public List<RoomEventOption> Options = new List<RoomEventOption>();

        /// <summary>
        /// Id used in save data. Same pattern as <c>HeroSO.SaveKey</c>: the display name stays free
        /// to rename without invalidating saves.
        /// </summary>
        public string SaveKey
        {
            get { return string.IsNullOrEmpty(Key) ? name : Key; }
        }
    }
}
