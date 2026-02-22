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

        public void SetDoorsEnabled(Door excludeDoor)
        {
            foreach (var door in Doors)
            {
                var col = door.GetComponent<Collider2D>();
                if (col != null)
                {
                    col.enabled = excludeDoor == null || door == excludeDoor;
                }
            }
        }

        public void EnableAllDoors()
        {
            foreach (var door in Doors)
            {
                var col = door.GetComponent<Collider2D>();
                if (col != null)
                {
                    col.enabled = true;
                }
            }
        }
    }
}
