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

        // The single prefab every enemy spawns from; its identity comes from the EnemySO.
        private const string EnemyPrefabResource = "Enemy";
        private GameObject _enemyPrefab;

        private GameObject EnemyPrefab()
        {
            if (_enemyPrefab == null)
            {
                // Qualify UnityEngine.Resources — the game has its own Assets.Scripts.Resources.
                _enemyPrefab = UnityEngine.Resources.Load<GameObject>(EnemyPrefabResource);
                if (_enemyPrefab == null)
                {
                    Debug.LogError($"Shared enemy prefab missing at Resources/{EnemyPrefabResource}.");
                }
            }
            return _enemyPrefab;
        }

        /// <summary>
        /// What the level being played does to the enemies it spawns. Set once by
        /// <c>DungeonManager</c> before generation rather than threaded through every spawn call,
        /// because <see cref="SpawnSingle"/> is also reached from a room event waking something
        /// mid-level, and that caller has no idea which run it is in. Null means the templates'
        /// own numbers, which is what free-play in the scene gets.
        /// </summary>
        public LevelEnemyTuning LevelTuning { get; private set; }

        public void SetLevelTuning(LevelEnemyTuning tuning)
        {
            LevelTuning = tuning;
        }

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

                var prefab = EnemyPrefab();
                if (prefab == null)
                {
                    continue;
                }

                foreach (var entry in spawnTable)
                {
                    if (entry.Enemy == null)
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

                        // One shared prefab, stamped with the spawn entry's EnemySO definition.
                        var enemyObj = Instantiate(prefab, transform);
                        var enemy = enemyObj.GetComponent<Enemy>();
                        enemy.Initialize(entry.Enemy, LevelTuning);
                        enemy.PlaceInRoom(room, position);

                        room.Enemies.Add(enemy);
                        _spawnedEnemies.Add(enemy);
                    }
                }
            }
        }

        /// <summary>
        /// Removes and destroys every enemy currently in <paramref name="room"/> (used to clear an
        /// exit room before dropping a boss in, so the climax is a clean fight).
        /// </summary>
        public void ClearRoomEnemies(Room room)
        {
            if (room == null)
            {
                return;
            }

            foreach (var enemy in room.Enemies.Where(e => e != null).ToList())
            {
                _spawnedEnemies.Remove(enemy);
                Destroy(enemy.gameObject);
            }
            room.Enemies.Clear();
        }

        /// <summary>
        /// Spawns a single enemy from <paramref name="definition"/> into <paramref name="room"/>
        /// (guaranteed, no roll). Used for boss placement in the exit room.
        /// </summary>
        public Enemy SpawnSingle(EnemySO definition, Room room)
        {
            if (definition == null || room == null)
            {
                return null;
            }

            var prefab = EnemyPrefab();
            if (prefab == null)
            {
                return null;
            }

            var occupied = room.Enemies
                .Where(e => e != null)
                .Select(e => e.transform.position)
                .ToList();
            var position = room.GetRandomWalkablePosition(occupied, 0.5f);

            var enemyObj = Instantiate(prefab, transform);
            var enemy = enemyObj.GetComponent<Enemy>();
            enemy.Initialize(definition, LevelTuning);
            enemy.PlaceInRoom(room, position);

            room.Enemies.Add(enemy);
            _spawnedEnemies.Add(enemy);
            return enemy;
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
