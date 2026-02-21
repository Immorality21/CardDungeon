using UnityEngine;

namespace Assets.Scripts.Rooms
{
    public class Enemy : MonoBehaviour
    {
        public Stats Stats;
        public Room Room;

        public bool IsAlive => Stats != null && Stats.Health > 0;

        public void PlaceInRoom(Room room)
        {
            Room = room;
            var center = new Vector3(
                room.GridPosition.x + room.RoomSO.Width / 2f - 0.5f,
                room.GridPosition.y + room.RoomSO.Height / 2f - 0.5f,
                -1f);
            // Offset slightly so enemy doesn't overlap player exactly
            center.x += 0.3f;
            center.y += 0.3f;
            transform.position = center;
        }
    }
}
