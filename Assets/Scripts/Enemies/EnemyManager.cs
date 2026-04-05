using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Dungeon;
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
            SpawnEnemies(rooms, playerRoom, null);
        }

        public void SpawnEnemies(List<Room> rooms, Room playerRoom, List<ManualRoomEntry> manualEntries)
        {
            for (int roomIdx = 0; roomIdx < rooms.Count; roomIdx++)
            {
                var room = rooms[roomIdx];
                if (room == playerRoom)
                {
                    continue;
                }

                // Determine spawn table: use manual override if provided, otherwise RoomSO table
                List<EnemySpawnEntry> spawnTable = null;
                bool guaranteeAll = false;

                if (manualEntries != null && roomIdx < manualEntries.Count &&
                    manualEntries[roomIdx].EnemySpawnOverride != null &&
                    manualEntries[roomIdx].EnemySpawnOverride.Count > 0)
                {
                    spawnTable = manualEntries[roomIdx].EnemySpawnOverride;
                    guaranteeAll = manualEntries[roomIdx].GuaranteeAllSpawns;
                }
                else
                {
                    var roomSO = room.RoomSO;
                    if (roomSO.EnemySpawnTable != null && roomSO.EnemySpawnTable.Count > 0)
                    {
                        spawnTable = roomSO.EnemySpawnTable;
                    }
                }

                if (spawnTable == null)
                {
                    continue;
                }

                foreach (var entry in spawnTable)
                {
                    if (entry.Prefab == null)
                    {
                        continue;
                    }

                    for (int i = 0; i < entry.EvaluationCount; i++)
                    {
                        if (!guaranteeAll && Random.Range(0f, 1f) > entry.SpawnChance)
                        {
                            continue;
                        }

                        var occupied = room.Enemies
                            .Where(e => e != null)
                            .Select(e => e.transform.position)
                            .ToList();
                        var position = room.GetRandomWalkablePosition(occupied, 0.5f);
                        var enemyObj = Instantiate(entry.Prefab, transform);
                        var enemy = enemyObj.GetComponent<Enemy>();
                        enemy.Stats = new Stats(entry.Stats.Attack, entry.Stats.Defense, entry.Stats.Health, entry.Stats.Agility);
                        enemy.LootItem = entry.LootItem;
                        enemy.LootCard = entry.LootCard;
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

    }
}
