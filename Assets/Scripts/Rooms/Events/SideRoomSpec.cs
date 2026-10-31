using System;
using System.Collections.Generic;
using Assets.Scripts.Enemies;
using Assets.Scripts.Items;
using UnityEngine;

namespace Assets.Scripts.Rooms.Events
{
    /// <summary>
    /// A room an event outcome leads to, off the floor's own map and behind no door: the Bone Choir's
    /// arena, the room behind a Choked Passage. It is built the first time it is needed, is never
    /// saved as the party's location (<see cref="Room.HostRoom"/> stands in for it), and never counts
    /// as one of the floor's rooms.
    /// </summary>
    [Serializable]
    public class SideRoomSpec
    {
        [Tooltip("The room to build. Empty means the outcome leads nowhere.")]
        public RoomSO Template;

        [Tooltip("The party is taken there at once and cannot leave until the fight is won, then is " +
                 "brought back. The event only counts as resolved once it is won. Off: the event's " +
                 "object becomes a way in and a way back, walked through as often as the party likes.")]
        public bool Mandatory;

        [Tooltip("Always here, every one of them: a boss and its escort. Separate from the roll below.")]
        public List<EnemySO> Guards = new List<EnemySO>();

        [Range(0f, 100f)]
        [Tooltip("Percent chance anything is waiting in the room.")]
        public float EnemyChancePercent = 100f;

        [Tooltip("What can be waiting. Each body is picked from this list.")]
        public List<EnemySO> Enemies = new List<EnemySO>();

        [Min(0)] public int MinEnemies = 1;
        [Min(0)] public int MaxEnemies = 1;

        [Tooltip("A cache stands in the room, paying as any cache does.")]
        public bool Chest;

        [Range(0f, 100f)]
        [Tooltip("Percent chance the chest also holds one of ChestLoot.")]
        public float ChestLootChancePercent;

        public List<ItemSO> ChestLoot = new List<ItemSO>();

        public bool IsSet
        {
            get { return Template != null; }
        }

        /// <summary>Whether a fight can be waiting in the room.</summary>
        public bool HasFoes
        {
            get
            {
                return (Guards != null && Guards.Count > 0)
                    || (Enemies != null && Enemies.Count > 0 && EnemyChancePercent > 0f);
            }
        }

        /// <summary>How many bodies wait in the room, from two rolls in [0, 1).</summary>
        public int RollEnemyCount(double presentRoll, double countRoll)
        {
            if (Enemies == null || Enemies.Count == 0 || presentRoll * 100.0 >= EnemyChancePercent)
            {
                return 0;
            }

            int low = Mathf.Max(0, MinEnemies);
            int high = Mathf.Max(low, MaxEnemies);
            return low + Mathf.Min((int)(countRoll * (high - low + 1)), high - low);
        }
    }
}
