using Assets.Scripts.Items;
using UnityEngine;

namespace Assets.Scripts.Rooms
{
    public class Player : MonoBehaviour
    {
        public Stats Stats = new Stats(5, 2, 20);
        public Room CurrentRoom { get; private set; }
        public Room PreviousRoom { get; private set; }

        public int GetEffectiveAttack()
        {
            var raw = InventoryManager.Instance.ComputeRawBonuses();
            var pct = InventoryManager.Instance.ComputePercentageBonuses();
            float baseVal = Stats.Attack + raw[StatType.Attack];
            return Mathf.RoundToInt(baseVal * (1f + pct[StatType.Attack] / 100f));
        }

        public int GetEffectiveDefense()
        {
            var raw = InventoryManager.Instance.ComputeRawBonuses();
            var pct = InventoryManager.Instance.ComputePercentageBonuses();
            float baseVal = Stats.Defense + raw[StatType.Defense];
            return Mathf.RoundToInt(baseVal * (1f + pct[StatType.Defense] / 100f));
        }

        public int GetEffectiveMaxHealth()
        {
            var raw = InventoryManager.Instance.ComputeRawBonuses();
            var pct = InventoryManager.Instance.ComputePercentageBonuses();
            float baseVal = Stats.MaxHealth + raw[StatType.MaxHealth];
            return Mathf.RoundToInt(baseVal * (1f + pct[StatType.MaxHealth] / 100f));
        }

        public void PlaceInRoom(Room room)
        {
            PreviousRoom = CurrentRoom;
            CurrentRoom = room;
            var center = new Vector3(
                room.GridPosition.x + room.RoomSO.Width / 2f - 0.5f,
                room.GridPosition.y + room.RoomSO.Height / 2f - 0.5f,
                -1f);
            transform.position = center;
        }

        public void PlaceAtDoor(Door door, Room fromRoom)
        {
            PreviousRoom = CurrentRoom;
            var destRoom = door.GetOtherRoom(fromRoom);
            CurrentRoom = destRoom;
            var doorPos = door.GetPositionInRoom(destRoom);
            transform.position = new Vector3(doorPos.x, doorPos.y, -1f);
        }
    }
}
