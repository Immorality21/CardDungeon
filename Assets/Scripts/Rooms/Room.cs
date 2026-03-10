using Assets.Scripts.Enemies;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Assets.Scripts.Rooms
{
    public class Room : MonoBehaviour
    {
        public RoomSO RoomSO;
        public List<Door> Doors = new List<Door>();
        public Vector2Int GridPosition;
        public List<Enemy> Enemies = new List<Enemy>();
        public int RoomIndex { get; set; }
        public bool IsExplored { get; private set; }

        public void Reveal()
        {
            IsExplored = true;
            SetChildRenderersEnabled(true);
            SetEnemyRenderersEnabled(true);

            foreach (var door in Doors)
            {
                SetDoorRenderersEnabled(door, true);
            }
        }

        public void Hide()
        {
            SetChildRenderersEnabled(false);
            SetEnemyRenderersEnabled(false);

            foreach (var door in Doors)
            {
                var otherRoom = door.GetOtherRoom(this);
                if (otherRoom == null || !otherRoom.IsExplored)
                {
                    SetDoorRenderersEnabled(door, false);
                }
            }
        }

        private void SetChildRenderersEnabled(bool enabled)
        {
            foreach (var sr in GetComponentsInChildren<SpriteRenderer>())
            {
                sr.enabled = enabled;
            }
        }

        private void SetEnemyRenderersEnabled(bool enabled)
        {
            foreach (var enemy in Enemies
                .Where(x => x))
            {
                var sr = enemy.GetComponent<SpriteRenderer>();

                if (sr != null)
                {
                    sr.enabled = enabled;
                }
            }
        }

        private void SetDoorRenderersEnabled(Door door, bool enabled)
        {
            var sr = door.GetComponent<SpriteRenderer>();

            if (sr != null)
            {
                sr.enabled = enabled;
            }
        }

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
