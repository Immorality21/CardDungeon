using System;
using System.Collections.Generic;
using Assets.Scripts.Enemies;
using Assets.Scripts.Rooms;
using UnityEngine;

namespace Assets.Scripts.Dungeon
{
    [Serializable]
    public class ManualRoomEntry
    {
        public RoomSO RoomTemplate;
        public Vector2Int GridPosition;
        public List<EnemySpawnEntry> EnemySpawnOverride = new List<EnemySpawnEntry>();
        public bool GuaranteeAllSpawns;
    }
}
