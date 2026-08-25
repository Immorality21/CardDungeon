using System.Collections.Generic;
using Assets.Scripts.Enemies;
using UnityEngine;

namespace Assets.Scripts.Rooms
{
    [CreateAssetMenu(menuName = "SO/Room")]
    public class RoomSO : ScriptableObject
    {
        public string Name;

        public int Width;

        public int Height;

        public Color Color;

        [Tooltip("What this room type is. Combat rooms may hold enemies; a Connector is a hallway. " +
                 "Treasure and Rest are normally *promoted* onto an ordinary room at generation " +
                 "(LevelDefinitionSO quotas) rather than authored here, so one template can serve as " +
                 "both - author them here only for a room type that is always a cache or a refuge.")]
        public RoomKind Kind = RoomKind.Combat;

        /// <summary>
        /// Hallway: no enemies, no events, no payload. Derived from <see cref="Kind"/> rather than a
        /// second serialized flag, because two sources of truth for "what is this room" is how a
        /// connector ends up spawning a treasure cache.
        /// </summary>
        public bool IsConnectorRoom
        {
            get { return Kind == RoomKind.Connector; }
        }

        [Tooltip("Events this kind of room CAN offer - a swamp offers swamp events. Each is rolled " +
                 "separately against its own RoomEventSO.SpawnChancePercent, so listing two raises " +
                 "the odds of the room having something; the first to pass is placed.")]
        public List<Events.RoomEventSO> PossibleEvents = new List<Events.RoomEventSO>();

        public List<EnemySpawnEntry> EnemySpawnTable;
    }
}
