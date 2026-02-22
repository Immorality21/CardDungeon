using Assets.Scripts.Items;
using UnityEngine;

namespace Assets.Scripts.Rooms
{
    public class Enemy : MonoBehaviour
    {
        public Stats Stats;
        public Room Room;
        public ItemSO LootItem;

        public bool IsAlive => Stats != null && Stats.Health > 0;

        public void PlaceInRoom(Room room, Vector3 position)
        {
            Room = room;
            transform.position = position;
        }
    }
}
