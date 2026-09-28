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

        [Tooltip("What this room is. Combat is an ordinary room. Rest or Treasure pins a refuge or a " +
                 "cache *here*, instead of leaving it to the level's quota - which promotes rooms at " +
                 "random and, on a hand-drawn floor, can land on the fight the layout was built " +
                 "around. Authored kinds count toward the level's TreasureRooms / RestRooms; only what " +
                 "is left of the quota is placed at random. Ignored on the start and exit rooms.")]
        public RoomKind Kind = RoomKind.Combat;
    }
}
