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

        private SpriteRenderer _spriteRenderer;

        public string DisplayName => gameObject.name;
        public Sprite Icon => GetIcon();
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

        private Sprite GetIcon()
        {
            if (_spriteRenderer == null)
            {
                _spriteRenderer = GetComponent<SpriteRenderer>();
            }
            return _spriteRenderer != null ? _spriteRenderer.sprite : null;
        }
    }
}
