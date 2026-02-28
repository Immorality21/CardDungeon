using System;
using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.Rooms
{
    [Serializable]
    public class RoomNode
    {
        public RoomSO roomData;
        public Room room;
        public Vector2Int position;
        public Vector2Int placedDirection;
        public List<RoomNode> connections = new List<RoomNode>();
    }
}
