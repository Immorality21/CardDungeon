using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Combat;
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
            var rolled = new List<RolledRoom>();
            for (int roomIdx = 0; roomIdx < rooms.Count; roomIdx++)
            {
                var room = rooms[roomIdx];
                if (room == playerRoom)
                {
                    continue;
                }

                // A non-combat room holds no guards. A treasure cache with three imps on it is just
                // another fight, and the point of the kind is that it is not one - so this is what
                // makes a level's room kinds cost it fights rather than merely decorate them.
                if (!room.Kind.HoldsEnemies())
                {
                    continue;
                }

                // Determine spawn table: use manual override if provided, otherwise RoomSO table
                List<EnemySpawnEntry> spawnTable = null;
                bool guaranteeAll = false;
                bool authored = false;

                if (manualEntries != null && roomIdx < manualEntries.Count &&
                    manualEntries[roomIdx].EnemySpawnOverride != null &&
                    manualEntries[roomIdx].EnemySpawnOverride.Count > 0)
                {
                    spawnTable = manualEntries[roomIdx].EnemySpawnOverride;
                    guaranteeAll = manualEntries[roomIdx].GuaranteeAllSpawns;
                    authored = true;
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

                        // One shared prefab, stamped with the spawn entry's EnemySO definition.
                        var enemyObj = Instantiate(prefab, transform);
                        var enemy = enemyObj.GetComponent<Enemy>();
                        enemy.Initialize(entry.Enemy, LevelTuning);
                        enemy.PlaceInRoom(room, room.ClaimSpot(enemy, -1f));

                        room.Enemies.Add(enemy);
                        _spawnedEnemies.Add(enemy);
                    }
                }

                rolled.Add(new RolledRoom { Room = room, Table = spawnTable, Authored = authored });
            }

            KeepWithinBand(rolled);
            foreach (var roll in rolled)
            {
                AddRevisitExtras(roll.Room, roll.Table, EnemyPrefab());
            }
        }

        private struct RolledRoom
        {
            public Room Room;
            public List<EnemySpawnEntry> Table;
            public bool Authored;
        }

        /// <summary>How far a floor's enemy total may stray from what its spawn tables expect: 0.25 is
        /// 75% to 125%. A first pass (playtest 2026-10-30: "10 enemies one try, 2 the next"); the owner
        /// means to revisit the run shape, so this is deliberately one constant.</summary>
        public const float BandFraction = 0.25f;

        /// <summary>The allowed range of enemies on a floor that expects <paramref name="expected"/>.</summary>
        public static Vector2Int Band(float expected)
        {
            if (expected <= 0f)
            {
                return new Vector2Int(0, 0);
            }
            int min = Mathf.FloorToInt(expected * (1f - BandFraction));
            int max = Mathf.Max(min, Mathf.CeilToInt(expected * (1f + BandFraction)));
            return new Vector2Int(min, max);
        }

        /// <summary>
        /// Trims or tops up the rolled (not authored) rooms so the floor's total falls inside
        /// <see cref="Band"/>. Trims the fullest room first; tops up the emptiest room that already
        /// holds a fight, from its own table, so an empty room stays a breather. Draws on the same
        /// seeded stream as the rolls, so a resumed floor reproduces it.
        /// </summary>
        private void KeepWithinBand(List<RolledRoom> rolled)
        {
            var rooms = rolled.Where(r => !r.Authored).ToList();
            float expected = 0f;
            int total = 0;
            foreach (var r in rooms)
            {
                foreach (var entry in r.Table)
                {
                    if (entry != null && entry.Enemy != null)
                    {
                        expected += Mathf.Clamp01(entry.SpawnChance) * entry.EvaluationCount;
                    }
                }
                total += r.Room.Enemies.Count;
            }
            var band = Band(expected);
            var prefab = EnemyPrefab();

            while (total > band.y)
            {
                var fullest = rooms.OrderByDescending(r => r.Room.Enemies.Count).First().Room;
                var enemy = fullest.Enemies[fullest.Enemies.Count - 1];
                fullest.Enemies.RemoveAt(fullest.Enemies.Count - 1);
                fullest.ReleaseSpot(enemy);
                _spawnedEnemies.Remove(enemy);
                Destroy(enemy.gameObject);
                total--;
            }

            while (total < band.x && prefab != null)
            {
                var open = rooms.Where(r => r.Room.Enemies.Count > 0 && r.Room.Enemies.Count < EnemyFormation.DesignMax
                                            && r.Table.Any(e => e != null && e.Enemy != null)).ToList();
                if (open.Count == 0)
                {
                    break;
                }
                var target = open.OrderBy(r => r.Room.Enemies.Count).First();
                var candidates = target.Table.Where(e => e != null && e.Enemy != null).ToList();
                var definition = candidates[Random.Range(0, candidates.Count)].Enemy;
                var enemyObj = Instantiate(prefab, transform);
                var added = enemyObj.GetComponent<Enemy>();
                added.Initialize(definition, LevelTuning);
                added.PlaceInRoom(target.Room, target.Room.ClaimSpot(added, -1f));
                target.Room.Enemies.Add(added);
                _spawnedEnemies.Add(added);
                total++;
            }
        }

        /// <summary>
        /// A revisit's "more enemies" condition (<see cref="RunFear.ExtraEnemiesPerRoom"/>): extra
        /// bodies drawn from the room's own spawn table, only in a room that already rolled a fight
        /// (an empty room stays a breather) and never past the stage's design size. Drawn from the
        /// dungeon's seeded stream, and a run's fear is fixed, so a resumed floor regenerates the
        /// same extras.
        /// </summary>
        private void AddRevisitExtras(Room room, List<EnemySpawnEntry> spawnTable, GameObject prefab)
        {
            int extra = RunFear.Current.ExtraEnemiesPerRoom;
            if (extra <= 0 || room.Enemies.Count == 0)
            {
                return;
            }

            var candidates = spawnTable.Where(e => e != null && e.Enemy != null).ToList();
            if (candidates.Count == 0)
            {
                return;
            }

            int toAdd = Mathf.Min(extra, EnemyFormation.DesignMax - room.Enemies.Count);
            for (int i = 0; i < toAdd; i++)
            {
                var definition = candidates[Random.Range(0, candidates.Count)].Enemy;
                var enemyObj = Instantiate(prefab, transform);
                var enemy = enemyObj.GetComponent<Enemy>();
                enemy.Initialize(definition, LevelTuning);
                enemy.PlaceInRoom(room, room.ClaimSpot(enemy, -1f));

                room.Enemies.Add(enemy);
                _spawnedEnemies.Add(enemy);
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
                room.ReleaseSpot(enemy);
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

            var enemyObj = Instantiate(prefab, transform);
            var enemy = enemyObj.GetComponent<Enemy>();
            enemy.Initialize(definition, LevelTuning);
            enemy.PlaceInRoom(room, room.ClaimSpot(enemy, -1f));

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
