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

        [Tooltip("Events this kind of room CAN offer - a swamp offers swamp events. Each is rolled " +
                 "separately against its own RoomEventSO.SpawnChancePercent, so listing two raises " +
                 "the odds of the room having something; the first to pass is placed.")]
        public List<Events.RoomEventSO> PossibleEvents = new List<Events.RoomEventSO>();

        public List<EnemySpawnEntry> EnemySpawnTable;
    }
}
