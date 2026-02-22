using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.Rooms
{
    public class Room : MonoBehaviour
    {
        public RoomSO RoomSO;
        public List<Door> Doors = new List<Door>();
        public Vector2Int GridPosition;
        public List<Enemy> Enemies = new List<Enemy>();
    }
}
