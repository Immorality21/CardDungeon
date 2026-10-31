using System.Collections.Generic;
using Assets.Scripts.UnitStats;
using UnityEngine;
using ImmoralityGaming.Localization;

namespace Assets.Scripts.Rooms.Events
{
    /// <summary>Where an event's object stands in its room.</summary>
    public enum RoomEventPlacement
    {
        /// <summary>On a floor tile, solid, walked up to from any side.</summary>
        Floor = 0,

        /// <summary>On the north wall, above a free floor tile: a grate, a door.</summary>
        NorthWall = 1
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
        [TextKeys("event", "title", "prompt")]
        public string Key;

        [System.NonSerialized] private string _title;

        /// <summary>From the text table (<c>event.{key}.title</c>). Set only on instances built in code.</summary>
        public string Title
        {
            get { return _title ?? Loc.AssetText("event", Key, this, "title"); }
            set { _title = value; }
        }

        [System.NonSerialized] private string _prompt;

        /// <summary>From the text table (<c>event.{key}.prompt</c>). Set only on instances built in code.</summary>
        public string Prompt
        {
            get { return _prompt ?? Loc.AssetText("event", Key, this, "prompt"); }
            set { _prompt = value; }
        }

        [Tooltip("The stat every StatCheck option on this event is resolved against.")]
        public StatType GoverningStat = StatType.Luck;

        [Header("In the room")]
        [Tooltip("The object drawn in the room, seen from the moment the room is. It stands on a tile, " +
                 "is solid, and is used by walking up to it once the room is clear.")]
        public Sprite WorldSprite;

        [Tooltip("What that object looks like once the event is resolved.")]
        public Sprite SpentSprite;

        public RoomEventPlacement Placement = RoomEventPlacement.Floor;

        [Header("Turning up at all")]
        [Tooltip("Set aside until it is reworked: no level lists it, and the balance analyzer does " +
                 "not report that as a mistake.")]
        public bool Parked;

        [Tooltip("Optional: a party stat that raises the level's chance, read as the party's best at " +
                 "it. Luck for blind finds, Intelligence for things only a scholar would notice.")]
        public StatType SpawnModifierStat = StatType.None;

        [Tooltip("How hard that stat pushes: chance = base + base * (stat * rate / 100). Relative to " +
                 "the base, so it scales a rare find and a common one alike. Small rates are almost " +
                 "invisible - at base 5 and rate 1.5, 10 Luck buys 0.75 of a percent.")]
        public float SpawnModifierRate;

        [Tooltip("Optional gate: minimum stat values, ALL of which must be met - though not " +
                 "necessarily by the same hero, so 10 Strength AND 15 Intelligence can be covered by " +
                 "two different heroes. Empty means no condition. For finds only a specialist would " +
                 "register: a tome nobody in the party can read is not a decision, it is furniture. " +
                 "Rows left at StatType.None are ignored.")]
        public List<UnitStat> SpawnRequirements = new List<UnitStat>();

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
