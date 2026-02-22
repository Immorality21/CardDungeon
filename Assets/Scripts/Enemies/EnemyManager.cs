using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Rooms;
using ImmoralityGaming.Fundamentals;
using UnityEngine;

namespace Assets.Scripts.Enemies
{
    public class EnemyManager : SingletonBehaviour<EnemyManager>
    {
        private List<Enemy> _spawnedEnemies = new List<Enemy>();

        public void SpawnEnemies(List<Room> rooms, Room playerRoom)
        {
            foreach (var room in rooms)
            {
                if (room == playerRoom)
                {
                    continue;
                }

                var roomSO = room.RoomSO;
                if (roomSO.EnemySpawnTable == null || roomSO.EnemySpawnTable.Count == 0)
                {
                    continue;
                }

                foreach (var entry in roomSO.EnemySpawnTable)
                {
                    if (entry.Prefab == null)
                    {
                        continue;
                    }

                    for (int i = 0; i < entry.EvaluationCount; i++)
                    {
                        if (Random.Range(0f, 1f) >= entry.SpawnChance)
                        {
                            continue;
                        }

                        var position = GetSpawnPosition(room);
                        var enemyObj = Instantiate(entry.Prefab, transform);
                        var enemy = enemyObj.GetComponent<Enemy>();
                        enemy.PlaceInRoom(room, position);

                        room.Enemies.Add(enemy);
                        _spawnedEnemies.Add(enemy);
                    }
                }
            }
        }

        public void CleanupEnemies()
        {
            foreach (var enemy in _spawnedEnemies.Where(x => x))
            {
                Destroy(enemy.gameObject);
            }

            _spawnedEnemies.Clear();
        }

        private Vector3 GetSpawnPosition(Room room)
        {
            var gridPos = room.GridPosition;
            var width = room.RoomSO.Width;
            var height = room.RoomSO.Height;

            // Walkable area: 1 tile inward from walls
            float minX = gridPos.x + 1;
            float maxX = gridPos.x + width - 2;
            float minY = gridPos.y + 1;
            float maxY = gridPos.y + height - 2;

            // For very small rooms, fall back to center
            if (minX > maxX || minY > maxY)
            {
                return new Vector3(
                    gridPos.x + width / 2f - 0.5f,
                    gridPos.y + height / 2f - 0.5f,
                    -1f);
            }

            Vector3 bestPos = Vector3.zero;
            for (int attempt = 0; attempt < 10; attempt++)
            {
                bestPos = new Vector3(
                    Random.Range(minX, maxX + 1),
                    Random.Range(minY, maxY + 1),
                    -1f);

                bool overlaps = false;
                foreach (var existing in room.Enemies)
                {
                    if (Vector3.Distance(existing.transform.position, bestPos) < 0.5f)
                    {
                        overlaps = true;
                        break;
                    }
                }

                if (!overlaps)
                {
                    return bestPos;
                }
            }

            return bestPos;
        }
    }
}
