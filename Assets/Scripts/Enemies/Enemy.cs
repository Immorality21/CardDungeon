using Assets.Scripts.Combat;
using Assets.Scripts.Items;
using Assets.Scripts.Rooms;
using UnityEngine;

namespace Assets.Scripts.Enemies
{
    public class Enemy : MonoBehaviour, ICombatUnit
    {
        public Stats Stats;
        public Room Room;
        public ItemSO LootItem;

        public string DisplayName => gameObject.name;
        public bool IsAlive => Stats != null && Stats.Health > 0;
        public bool IsHero => false;
        public Transform Transform => transform;

        Stats ICombatUnit.Stats => Stats;

        public void PlaceInRoom(Room room, Vector3 position)
        {
            Room = room;
            transform.position = position;
        }

        public int GetEffectiveAttack()
        {
            return Stats.Attack;
        }

        public int GetEffectiveDefense()
        {
            return Stats.Defense;
        }
    }
}
