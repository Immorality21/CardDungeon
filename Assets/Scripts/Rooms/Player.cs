using UnityEngine;

namespace Assets.Scripts.Rooms
{
    public class Player : MonoBehaviour
    {
        public Room CurrentRoom { get; private set; }

        public void PlaceInRoom(Room room)
        {
            CurrentRoom = room;
            var center = new Vector3(
                room.GridPosition.x + room.RoomSO.Width / 2f - 0.5f,
                room.GridPosition.y + room.RoomSO.Height / 2f - 0.5f,
                -1f);
            transform.position = center;
        }

        public void PlaceAtDoor(Door door, Room fromRoom)
        {
            var destRoom = door.GetOtherRoom(fromRoom);
            CurrentRoom = destRoom;
            var doorPos = door.GetPositionInRoom(destRoom);
            transform.position = new Vector3(doorPos.x, doorPos.y, -1f);
        }
    }
}
