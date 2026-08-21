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

        public bool IsConnectorRoom;

        [Tooltip("Events this kind of room CAN offer - a swamp offers swamp events. How many rooms " +
                 "in a level actually get one is LevelDefinitionSO.EventsPerLevel, so a template " +
                 "used three times in a level does not repeat its event three times.")]
        public List<Events.RoomEventSO> PossibleEvents = new List<Events.RoomEventSO>();

        [Tooltip("An event EVERY instance of this room offers, exempt from the level event " +
                 "budget. For rooms whose whole identity is an interaction - a treasury the " +
                 "player can always loot. A room with one is skipped by budgeted placement, so " +
                 "it offers this and nothing else.")]
        public Events.RoomEventSO GuaranteedEvent;

        public List<EnemySpawnEntry> EnemySpawnTable;
    }
}
