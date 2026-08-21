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

        [TextArea]
        public List<string> ExamineOptions;

        [TextArea]
        public List<string> ActionOptions;

        public bool IsConnectorRoom;

        [Tooltip("Events this kind of room CAN offer - a swamp offers swamp events. How many rooms " +
                 "in a level actually get one is LevelDefinitionSO.EventsPerLevel, so a template " +
                 "used three times in a level does not repeat its event three times.")]
        public List<Events.RoomEventSO> PossibleEvents = new List<Events.RoomEventSO>();

        public List<EnemySpawnEntry> EnemySpawnTable;
    }
}
