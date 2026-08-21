using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Cards;
using Assets.Scripts.Enemies;
using Assets.Scripts.Heroes;
using Assets.Scripts.IO;
using Assets.Scripts.Items;
using Assets.Scripts.Progression;
using Assets.Scripts.Resources;
using Assets.Scripts.Rooms;
using ImmoralityGaming.Fundamentals;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Assets.Scripts.Dungeon
{
    public class DungeonManager : SingletonBehaviour<DungeonManager>
    {
        [SerializeField]
        private RoomManager _roomManager;

        [SerializeField]
        private GameObject _partyPrefab;

        [SerializeField]
        [Tooltip("Shared party roster (single source of truth, also read by the hub). Falls back to " +
                 "_heroDefinitions if unset.")]
        private PartyRosterSO _partyRoster;

        [SerializeField]
        private List<HeroSO> _heroDefinitions;

        [SerializeField]
        [Tooltip("Healing-potion consumable topped up to the belt cap on each fresh dungeon entry.")]
        private ItemSO _healingPotion;

        /// <summary>
        /// The heroes that actually enter the dungeon: the *owned* subset of the roster catalog, not
        /// every authored hero. A fresh save starts with <c>PartyRosterSO.StartingHeroes</c> only and
        /// grows by rescuing captives or recruiting at the tavern. Falls back to the inline
        /// definitions when no roster is wired (free-play in the scene).
        /// </summary>
        private List<HeroSO> RosterHeroes()
        {
            if (_partyRoster != null && _partyRoster.Heroes.Count > 0)
            {
                var owned = HeroRoster.GetOwnedHeroes(_partyRoster);
                if (owned.Count > 0)
                {
                    return owned;
                }
            }
            return _heroDefinitions;
        }

        private RoomActionUI _roomActionUI;

        private RoomActionUI GetRoomActionUI()
        {
            if (_roomActionUI == null)
            {
                _roomActionUI = FindAnyObjectByType<RoomActionUI>(FindObjectsInactive.Include);
            }
            return _roomActionUI;
        }

        [SerializeField]
        private bool _randomGenerateOn;

        [SerializeField]
        private int _customSeed = 0;

        [SerializeField]
        private LevelDefinitionSO _testLevel;

        [SerializeField]
        private Sprite _exitRoomMarkerSprite;

        public static int? SeedToLoad;
        public static LevelDefinitionSO LevelToLoad;
        public static RunDefinitionSO ActiveRun;
        public static int RunLevelIndex;

        /// <summary>True when the active run is on its last level (drives the run-complete fanfare).</summary>
        public static bool IsFinalRunLevel =>
            ActiveRun != null && RunLevelIndex == ActiveRun.Levels.Count - 1;
        public Party Party { get; private set; }
        public EquippedMagicState MagicState { get; private set; }

        /// <summary>The level definition currently being played (drives the per-level combat backdrop).</summary>
        public LevelDefinitionSO CurrentLevel => _level;

        private LevelDefinitionSO _level;
        private ManualLevelLayoutSO _manualLayout;
        private FileHandler _fileHandler;
        private int _currentSeed;

        private void Start()
        {
            _fileHandler = new FileHandler();
            _level = LevelToLoad != null ? LevelToLoad : _testLevel;
            LevelToLoad = null;

            // Defer inventory saves during dungeon play
            if (InventoryManager.HasInstance)
            {
                InventoryManager.Instance.SetDeferSaves(true);
            }

            // Subscribe to dungeon cleared event
            CombatManager.Instance.OnDungeonCleared += OnDungeonCleared;

            // Resolve manual layout from active run
            ManualLevelLayoutSO manualLayout = null;
            if (ActiveRun != null && RunLevelIndex >= 0 && RunLevelIndex < ActiveRun.Levels.Count)
            {
                manualLayout = ActiveRun.Levels[RunLevelIndex].ManualLayout;
            }

            if (manualLayout != null)
            {
                SpawnManualDungeon(manualLayout);
            }
            else if (SeedToLoad.HasValue)
            {
                var seed = SeedToLoad.Value;
                SeedToLoad = null;
                LoadSavedDungeon(seed);
            }
            else if (_randomGenerateOn)
            {
                SpawnDungeon();
            }
        }

        private void OnDestroy()
        {
            if (CombatManager.HasInstance)
            {
                CombatManager.Instance.OnDungeonCleared -= OnDungeonCleared;
            }
        }

        private void SpawnManualDungeon(ManualLevelLayoutSO layout)
        {
            _manualLayout = layout;
            var seed = layout.GetDeterministicSeed();
            _currentSeed = seed;
            Random.InitState(seed);

            EnemyManager.Instance.CleanupEnemies();

            if (Party != null)
            {
                Destroy(Party.gameObject);
            }

            // Build dungeon from manual layout
            var rooms = _roomManager.BuildManualDungeon(layout);

            // Assign stable room indices
            for (int i = 0; i < rooms.Count; i++)
            {
                rooms[i].RoomIndex = i;
            }

            // Designate start and exit rooms
            var startRoom = rooms[layout.StartRoomIndex];
            var exitRoom = rooms[layout.ExitRoomIndex];
            exitRoom.IsExit = true;
            PlaceExitMarker(exitRoom);

            // Spawn enemies with manual overrides
            EnemyManager.Instance.SpawnEnemies(rooms, startRoom, layout.Rooms);
            PlaceBossIfConfigured(rooms);
            PlaceCaptiveIfConfigured(rooms, startRoom);

            // Check for saved state to resume
            DungeonSaveData saveData = null;
            if (SeedToLoad.HasValue)
            {
                saveData = DungeonSaveManager.Instance.Load(SeedToLoad.Value);
                SeedToLoad = null;
                if (saveData.Seed == 0)
                {
                    saveData = null;
                }
            }

            if (saveData != null)
            {
                RestoreSavedState(saveData, rooms);
            }
            else
            {
                SpawnFreshDungeon(seed, rooms, startRoom);
            }
        }

        [ContextMenu("Spawn Dungeon")]
        private void SpawnDungeon()
        {
            SpawnDungeon(null);
        }

        private void SpawnDungeon(DungeonSaveData saveData)
        {
            var seed = _customSeed;

            if (saveData != null)
            {
                seed = saveData.Seed;
            }
            else if (seed == 0)
            {
                seed = System.Guid.NewGuid().GetHashCode();
                Debug.Log(seed);
            }

            _currentSeed = seed;
            Random.InitState(seed);

            EnemyManager.Instance.CleanupEnemies();

            if (Party != null)
            {
                Destroy(Party.gameObject);
            }

            // Step 1: Generate rooms, doors, and walls
            var rooms = _roomManager.GenerateDungeon(_level);

            // Step 2: Assign stable room indices
            for (int i = 0; i < rooms.Count; i++)
            {
                rooms[i].RoomIndex = i;
            }

            // Step 3: Pick starting room (first room = graph root, always at one end)
            var startRoom = rooms[0];

            // Step 4: Designate exit room (farthest from start via BFS)
            DesignateExitRoom(rooms, startRoom);

            // Step 5: Spawn enemies
            EnemyManager.Instance.SpawnEnemies(rooms, startRoom);
            PlaceBossIfConfigured(rooms);
            PlaceCaptiveIfConfigured(rooms, startRoom);

            if (saveData != null)
            {
                RestoreSavedState(saveData, rooms);
            }
            else
            {
                SpawnFreshDungeon(seed, rooms, startRoom);
            }
        }

        private void SpawnFreshDungeon(int seed, List<Room> rooms, Room startRoom)
        {
            // Spawn party in the chosen starting room
            var partyObj = Instantiate(_partyPrefab, transform);
            Party = partyObj.GetComponent<Party>();
            Party.Initialize(RosterHeroes());
            Party.HealAll();
            Party.PlaceInRoom(startRoom);
            GameManager.Instance.Initialize(Party, GetRoomActionUI());

            // Hide all rooms (fog of war), then reveal the starting room
            foreach (var room in rooms)
            {
                room.Hide();
            }
            startRoom.Reveal();

            // Initialize equipped-magic state, carrying magic drawn on earlier levels of
            // this run (persisted in the run save; empty on the first level or a fresh run).
            MagicState = new EquippedMagicState();
            MagicState.Initialize(Party.Heroes, GetMagicSlotCount());
            if (ActiveRun != null && MagicCatalog.HasInstance)
            {
                var carried = _fileHandler.Load<RunSaveData>();
                MagicState.Restore(carried.EquippedMagic, MagicCatalog.Instance.GetMagic);
            }

            // Top the healing-potion belt back up to its cap for the new dungeon. Consumables
            // now live in the item inventory; the "belt" is just the carry cap the Merchant raises.
            if (_healingPotion != null && InventoryManager.HasInstance && PartyResourceManager.Instance != null)
            {
                int cap = PartyResourceManager.Instance.GetMax(PartyResourceType.HealingPotion);
                InventoryManager.Instance.TopUpConsumableToCap(_healingPotion, cap);
            }

            // Initialize save manager and persist initial state
            var levelKey = _level != null ? _level.Key : _manualLayout != null ? _manualLayout.Key : "unknown";
            DungeonSaveManager.Instance.Initialize(seed, levelKey, rooms);
            DungeonSaveManager.Instance.Save(startRoom);

            // Store active dungeon seed in run save so we can resume
            if (ActiveRun != null)
            {
                var runSave = _fileHandler.Load<RunSaveData>();
                runSave.ActiveDungeonSeed = seed;
                _fileHandler.Save(runSave);
            }

            GameManager.Instance.EnterRoom(startRoom);
        }

        private void RestoreSavedState(DungeonSaveData saveData, List<Room> rooms)
        {
            // Remove killed enemies based on saved counts
            foreach (var roomData in saveData.Rooms)
            {
                if (roomData.RoomIndex < 0 || roomData.RoomIndex >= rooms.Count)
                {
                    continue;
                }

                var room = rooms[roomData.RoomIndex];

                while (room.Enemies.Count > roomData.EnemyCount)
                {
                    var last = room.Enemies[room.Enemies.Count - 1];
                    room.Enemies.RemoveAt(room.Enemies.Count - 1);
                    if (last != null)
                    {
                        Destroy(last.gameObject);
                    }
                }
            }

            // Spawn party in the saved current room
            var currentRoom = rooms[saveData.CurrentRoomIndex];
            var partyObj = Instantiate(_partyPrefab, transform);
            Party = partyObj.GetComponent<Party>();
            Party.Initialize(RosterHeroes());
            Party.PlaceInRoom(currentRoom);
            GameManager.Instance.Initialize(Party, GetRoomActionUI());

            // Hide all rooms, then reveal explored ones
            foreach (var room in rooms)
            {
                room.Hide();
            }

            foreach (var roomData in saveData.Rooms)
            {
                if (roomData.IsExplored && roomData.RoomIndex >= 0 && roomData.RoomIndex < rooms.Count)
                {
                    rooms[roomData.RoomIndex].Reveal();
                }
            }

            // Restore equipped-magic state from the dungeon save (mid-level resume)
            MagicState = new EquippedMagicState();
            MagicState.Initialize(Party.Heroes, GetMagicSlotCount());
            if (MagicCatalog.HasInstance)
            {
                MagicState.Restore(saveData.EquippedMagic, MagicCatalog.Instance.GetMagic);
            }

            // Consumable quantities are part of the item inventory now (committed on level-clear),
            // so there is no separate per-dungeon resource state to restore here.

            var restoreLevelKey = _level != null ? _level.Key : _manualLayout != null ? _manualLayout.Key : "unknown";
            DungeonSaveManager.Instance.Initialize(saveData.Seed, restoreLevelKey, rooms);
            GameManager.Instance.EnterRoom(currentRoom);
        }

        private void DesignateExitRoom(List<Room> rooms, Room startRoom)
        {
            var distance = new Dictionary<Room, int>();
            var queue = new Queue<Room>();

            distance[startRoom] = 0;
            queue.Enqueue(startRoom);

            Room farthest = startRoom;
            int maxDist = 0;

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();

                foreach (var door in current.Doors)
                {
                    var neighbor = door.GetOtherRoom(current);
                    if (neighbor != null && !distance.ContainsKey(neighbor))
                    {
                        var dist = distance[current] + 1;
                        distance[neighbor] = dist;
                        queue.Enqueue(neighbor);

                        if (dist > maxDist)
                        {
                            maxDist = dist;
                            farthest = neighbor;
                        }
                    }
                }
            }

            farthest.IsExit = true;
            PlaceExitMarker(farthest);
        }

        /// <summary>
        /// If the active run's current level defines a boss, guarantees it (alone) in the exit
        /// room so the level climaxes in a boss fight. No-op for normal levels or free-play.
        /// Must run after the exit room is designated and normal enemies are spawned.
        /// </summary>
        private void PlaceBossIfConfigured(List<Room> rooms)
        {
            if (ActiveRun == null || RunLevelIndex < 0 || RunLevelIndex >= ActiveRun.Levels.Count)
            {
                return;
            }

            var boss = ActiveRun.Levels[RunLevelIndex].BossEnemy;
            if (boss == null)
            {
                return;
            }

            var exitRoom = rooms.FirstOrDefault(r => r != null && r.IsExit);
            if (exitRoom == null)
            {
                Debug.LogWarning("Boss configured for this level but no exit room was found; boss not placed.");
                return;
            }

            // Clear the exit room's rolled enemies so the boss fight is a clean climax.
            EnemyManager.Instance.ClearRoomEnemies(exitRoom);
            EnemyManager.Instance.SpawnSingle(boss, exitRoom);
        }

        /// <summary>
        /// If the active run's current level defines a captive hero the player does not already own,
        /// puts them in one room off the critical endpoints. Skips the start room (a free hero on
        /// turn one is not a discovery) and the exit room (entering a cleared exit ends the level, so
        /// the player would never get the chance to act). Must run after the exit room is designated.
        ///
        /// Placement is deliberately random among the remaining rooms: on a generated level the
        /// captive may sit off the path and be missed, which is the intended risk. Hand-authored
        /// levels guarantee the find by shape - every room in the tutorial layout is on the route.
        /// </summary>
        private void PlaceCaptiveIfConfigured(List<Room> rooms, Room startRoom)
        {
            if (ActiveRun == null || RunLevelIndex < 0 || RunLevelIndex >= ActiveRun.Levels.Count)
            {
                return;
            }

            var captive = ActiveRun.Levels[RunLevelIndex].RescueHero;
            if (captive == null)
            {
                return;
            }

            // Already recruited (rescued before, or bought at the tavern) - nothing to find.
            if (_partyRoster != null && HeroRoster.Owns(_partyRoster, captive))
            {
                return;
            }

            var candidates = rooms
                .Where(r => r != null && r != startRoom && !r.IsExit && !r.RoomSO.IsConnectorRoom)
                .ToList();

            if (candidates.Count == 0)
            {
                Debug.LogWarning($"Captive {captive.DisplayName} configured but no eligible room was found; not placed.");
                return;
            }

            var room = candidates[Random.Range(0, candidates.Count)];
            room.CaptiveHero = captive;
            PlaceCaptiveMarker(room, captive);
        }

        /// <summary>
        /// Shows the captive in the room using their own portrait, so the room reads as holding
        /// someone without needing bespoke cage art. Tinted down to suggest captivity and to keep it
        /// distinct from the hero sprites that appear during combat fan-out.
        /// </summary>
        private void PlaceCaptiveMarker(Room room, HeroSO captive)
        {
            if (captive.Sprite == null)
            {
                return;
            }

            var markerObj = new GameObject("Captive");
            markerObj.transform.SetParent(room.transform, false);
            var center = room.GetCenter();
            center.z = -0.5f;
            markerObj.transform.position = center;

            var sr = markerObj.AddComponent<SpriteRenderer>();
            sr.sprite = captive.Sprite;
            sr.sortingOrder = 2;
            sr.color = new Color(0.55f, 0.55f, 0.7f, 1f);
            _captiveMarkers[room] = markerObj;
        }

        /// <summary>
        /// Frees the captive in <paramref name="room"/>: they join the live party at once so the
        /// rescue pays off in this level's remaining fights, and ownership is recorded *deferred* -
        /// written only when the level is cleared, so dying loses them along with the run's XP and
        /// loot. Returns false when there was nobody to free.
        /// </summary>
        public bool TryRescueCaptive(Room room)
        {
            if (room == null || room.CaptiveHero == null || Party == null)
            {
                return false;
            }

            var captive = room.CaptiveHero;
            var hero = Party.AddHero(captive);
            if (hero == null)
            {
                // Already in the party somehow - clear the marker so it cannot be re-triggered.
                room.CaptiveHero = null;
                RemoveCaptiveMarker(room);
                return false;
            }

            Party.MarkOwnedDeferred(captive);
            room.CaptiveHero = null;
            RemoveCaptiveMarker(room);

            // Give the newcomer their own magic slots, or they cannot cast anything this run.
            if (MagicState != null)
            {
                MagicState.AddHero(hero, GetMagicSlotCount());
            }

            Debug.Log($"Rescued {captive.DisplayName}; party is now {Party.Heroes.Count} strong.");
            return true;
        }

        private void RemoveCaptiveMarker(Room room)
        {
            if (_captiveMarkers.TryGetValue(room, out var marker))
            {
                if (marker != null)
                {
                    Destroy(marker);
                }
                _captiveMarkers.Remove(room);
            }
        }

        private readonly Dictionary<Room, GameObject> _captiveMarkers = new Dictionary<Room, GameObject>();

        private void PlaceExitMarker(Room room)
        {
            if (_exitRoomMarkerSprite == null)
            {
                return;
            }

            var markerObj = new GameObject("ExitMarker");
            markerObj.transform.SetParent(room.transform, false);
            var center = room.GetCenter();
            center.z = -0.5f;
            markerObj.transform.position = center;
            var sr = markerObj.AddComponent<SpriteRenderer>();
            sr.sprite = _exitRoomMarkerSprite;
            sr.sortingOrder = 3;
        }

        private void OnDungeonCleared()
        {
            // Commit all deferred progress to persistent save files
            if (Party != null)
            {
                Party.CommitProgress();
            }

            // Award persistent meta-currency for clearing the level
            MetaProgressManager.Instance.AwardLevelClear();

            if (InventoryManager.HasInstance)
            {
                InventoryManager.Instance.CommitInventory();
                InventoryManager.Instance.SetDeferSaves(false);
            }

            // Delete dungeon save
            if (DungeonSaveManager.HasInstance)
            {
                DungeonSaveManager.Instance.DeleteCurrentSave();
            }

            // Advance run progress
            if (ActiveRun != null)
            {
                var runSave = _fileHandler.Load<RunSaveData>();
                runSave.RunKey = !string.IsNullOrEmpty(ActiveRun.Key) ? ActiveRun.Key : ActiveRun.name;
                runSave.CurrentLevelIndex = RunLevelIndex + 1;
                runSave.ActiveDungeonSeed = 0;

                // Carry equipped magic to the next level of the run.
                if (MagicState != null)
                {
                    runSave.EquippedMagic = MagicState.GetSaveData();
                }

                _fileHandler.Save(runSave);
                Debug.Log($"Run advanced to level {runSave.CurrentLevelIndex}/{ActiveRun.Levels.Count}, RunKey={runSave.RunKey}");

                if (runSave.CurrentLevelIndex >= ActiveRun.Levels.Count)
                {
                    // Run complete — clear run save
                    _fileHandler.Delete(runSave);
                    ActiveRun = null;
                    MainMenuManager.MarkRunCompleted();
                }
            }

            SceneManager.LoadScene("MenuScene");
        }

        /// <summary>Number of equipped-magic slots each hero gets (raised by meta slot upgrades).</summary>
        private int GetMagicSlotCount()
        {
            int bonus = MetaProgressManager.HasInstance ? MetaProgressManager.Instance.GetBonusSlotCount() : 0;
            return EquippedMagicState.DefaultSlotCount + bonus;
        }

        public void HandlePartyDeath()
        {
            // Award consolation meta-currency for run progress before wiping saves.
            // Meta-progress persists immediately, so this survives the death wipe.
            if (ActiveRun != null)
            {
                MetaProgressManager.Instance.AwardRunProgressOnDeath(RunLevelIndex);
            }

            // Forfeit this level's un-banked kill-gold.
            MetaProgressManager.Instance.DiscardPendingGold();

            // Delete dungeon save — all in-memory XP/items are discarded with the scene
            if (DungeonSaveManager.HasInstance)
            {
                DungeonSaveManager.Instance.DeleteCurrentSave();
            }

            // Delete run save — run is over
            if (ActiveRun != null)
            {
                var runSave = new RunSaveData();
                _fileHandler.Delete(runSave);
                ActiveRun = null;
            }

            // Reload inventory from disk to discard in-memory changes
            if (InventoryManager.HasInstance)
            {
                InventoryManager.Instance.Load();
                InventoryManager.Instance.SetDeferSaves(false);
            }
        }

        public void LoadSavedDungeon(int seed)
        {
            var saveData = DungeonSaveManager.Instance.Load(seed);
            if (saveData.Seed != 0)
            {
                SpawnDungeon(saveData);
            }
        }
    }
}
