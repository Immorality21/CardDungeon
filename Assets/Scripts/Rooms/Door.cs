using System;
using UnityEngine;

namespace Assets.Scripts.Rooms
{
    public class Door : MonoBehaviour
    {
        public Room RoomA;
        public Room RoomB;
        public Vector2 PositionInA;
        public Vector2 PositionInB;

        public event Action<Door> OnDoorClicked;

        public Room GetOtherRoom(Room current)
        {
            return current == RoomA ? RoomB : RoomA;
        }

        public Vector2 GetPositionInRoom(Room room)
        {
            return room == RoomA ? PositionInA : PositionInB;
        }

        private void OnMouseDown()
        {
            OnDoorClicked?.Invoke(this);
        }
    }
}
