using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Balance;
using Assets.Scripts.Cards;
using Assets.Scripts.Enemies;
using Assets.Scripts.Heroes;
using Assets.Scripts.IO;
using Assets.Scripts.Items;
using Assets.Scripts.Progression;
using Assets.Scripts.Resources;
using Assets.Scripts.Rooms;
using Assets.Scripts.UnitStats;
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
        /// The heroes that actually enter the dungeon: the party the player *selected* in the hub, not
        /// every hero they own and not every hero authored. A fresh save starts with
        /// <c>PartyRosterSO.StartingHeroes</c>, grows by rescuing captives or recruiting at the tavern,
        /// and is narrowed to a fielded lineup of at most <c>MetaProgressManager.GetPartyCap()</c> by
        /// the party-select screen. Falls back to the inline definitions when no roster is wired
        /// (free-play in the scene).
        ///
        /// <para>Party size is the game's strongest difficulty dial - each hero roughly halves
        /// per-enemy danger while quartering each hero's XP share - so this is the one place that
        /// decides both.</para>
        /// </summary>
        private List<HeroSO> FieldedHeroes()
        {
            if (_partyRoster != null && _partyRoster.Heroes.Count > 0)
            {
                var selected = HeroRoster.GetSelectedHeroes(_partyRoster, PartyCap());
                if (selected.Count > 0)
                {
                    return selected;
                }
            }
            return _heroDefinitions;
        }

        private static int PartyCap()
        {
            return MetaProgressManager.HasInstance
                ? MetaProgressManager.Instance.GetPartyCap()
                : PartySlots.BaseCap;
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

        /// <summary>
        /// Buffs and debuffs room events hung on the party, standing until the level is cleared.
        /// Level-scoped like health: <c>CombatManager</c> seeds each fight's buff tracker from here,
        /// so a curse picked up in a corridor is paid for in every encounter that follows.
        /// </summary>
        public Rooms.Events.LevelAfflictionTracker Afflictions { get; private set; }
            = new Rooms.Events.LevelAfflictionTracker();

        /// <summary>The level definition currently being played (drives the per-level combat backdrop).</summary>
        public LevelDefinitionSO CurrentLevel => _level;

        /// <summary>
        /// The run entry for the level being played, or null in free-play. The entry is where a
        /// level's authored content that is not the room template lives - its boss, its rescue, and
        /// its enemy tuning.
        /// </summary>
        public RunLevelEntry CurrentLevelEntry =>
            ActiveRun != null && RunLevelIndex >= 0 && RunLevelIndex < ActiveRun.Levels.Count
                ? ActiveRun.Levels[RunLevelIndex]
                : null;

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

            // An EnemySO is a template; this level owns the numbers. Set before anything spawns,
            // and it covers SpawnSingle too - the boss, and whatever a room event wakes up.
            var levelEntry = CurrentLevelEntry;
            EnemyManager.Instance.SetLevelTuning(levelEntry != null ? levelEntry.EnemyTuning : null);

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

            // Room kinds first, for the same reason as the generated path: a promoted room holds no
            // guards, and EnemyManager has to see the kind before it populates anything.
            PlaceRoomKinds(rooms, startRoom);

            // Spawn enemies with manual overrides
            EnemyManager.Instance.SpawnEnemies(rooms, startRoom, layout.Rooms);
            PlaceBossIfConfigured(rooms);
            PlaceCaptiveIfConfigured(rooms, startRoom);
            PlaceRoomEvents(rooms, startRoom);

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

            if (saveData != null && DungeonSaveCompatibility.IsCompatible(saveData, LevelKeyForSave(), rooms.Count))
            {
                RestoreSavedState(saveData, rooms);
            }
            else
            {
                if (saveData != null)
                {
                    // The save is stale, not corrupt: start this level over rather than crashing on a
                    // room index that no longer exists. Everything outside the level - which run,
                    // which floor, XP, gear, meta progress - lives in other files and is untouched, so
                    // the run continues and only this floor's progress is lost.
                    Debug.LogWarning(
                        $"Dungeon save for seed {saveData.Seed} no longer matches the layout "
                        + $"'{LevelKeyForSave()}' generates ({DungeonSaveCompatibility.Describe(saveData, LevelKeyForSave(), rooms.Count)}). "
                        + "Starting the level fresh.");
                }
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

            // An EnemySO is a template; this level owns the numbers. Set before anything spawns,
            // and it covers SpawnSingle too - the boss, and whatever a room event wakes up.
            var levelEntry = CurrentLevelEntry;
            EnemyManager.Instance.SetLevelTuning(levelEntry != null ? levelEntry.EnemyTuning : null);

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

            // Step 5: Decide what each room *is*, then populate it. Kinds come first because a
            // non-combat room holds no guards - EnemyManager reads the kind and skips it.
            PlaceRoomKinds(rooms, startRoom);

            // Step 6: Spawn enemies
            EnemyManager.Instance.SpawnEnemies(rooms, startRoom);
            PlaceBossIfConfigured(rooms);
            PlaceCaptiveIfConfigured(rooms, startRoom);
            PlaceRoomEvents(rooms, startRoom);

            if (saveData != null && DungeonSaveCompatibility.IsCompatible(saveData, LevelKeyForSave(), rooms.Count))
            {
                RestoreSavedState(saveData, rooms);
            }
            else
            {
                if (saveData != null)
                {
                    // The save is stale, not corrupt: start this level over rather than crashing on a
                    // room index that no longer exists. Everything outside the level - which run,
                    // which floor, XP, gear, meta progress - lives in other files and is untouched, so
                    // the run continues and only this floor's progress is lost.
                    Debug.LogWarning(
                        $"Dungeon save for seed {saveData.Seed} no longer matches the layout "
                        + $"'{LevelKeyForSave()}' generates ({DungeonSaveCompatibility.Describe(saveData, LevelKeyForSave(), rooms.Count)}). "
                        + "Starting the level fresh.");
                }
                SpawnFreshDungeon(seed, rooms, startRoom);
            }
        }

        /// <summary>The key a dungeon save for the current level is stamped with.</summary>
        private string LevelKeyForSave()
        {
            if (_level != null)
            {
                return _level.Key;
            }
            return _manualLayout != null ? _manualLayout.Key : "unknown";
        }

        private void SpawnFreshDungeon(int seed, List<Room> rooms, Room startRoom)
        {
            // Spawn party in the chosen starting room
            var partyObj = Instantiate(_partyPrefab, transform);
            Party = partyObj.GetComponent<Party>();
            Party.Initialize(FieldedHeroes());
            Party.HealAll();
            // Afflictions and the consumption ledger are level-scoped like health, so a fresh level
            // starts clean on all three.
            Afflictions.Clear();
            if (InventoryManager.HasInstance)
            {
                InventoryManager.Instance.BeginDungeonConsumption();
            }
            Party.PlaceInRoom(startRoom);
            GameManager.Instance.Initialize(Party, GetRoomActionUI());

            // Hide all rooms (fog of war), then reveal the starting room
            foreach (var room in rooms)
            {
                room.Hide();
            }
            startRoom.Reveal();

            // Initialize equipped-magic state, then carry a loadout into it. Two sources, in
            // precedence order: the run save holds what was drawn on earlier levels of *this* run,
            // and the magic-loadout file holds what the heroes walked out of previous runs with.
            // The run save wins while a run is in flight because it is the more recent of the two;
            // on level 1 it is empty, and that is when a hero picks their old kit back up.
            MagicState = new EquippedMagicState();
            MagicState.Initialize(Party.Heroes);
            if (MagicCatalog.HasInstance)
            {
                var carried = ActiveRun != null ? _fileHandler.Load<RunSaveData>().EquippedMagic : null;
                if (carried == null || carried.Count == 0)
                {
                    carried = _fileHandler.Load<MagicLoadoutSaveData>().Heroes;
                }
                MagicState.Restore(carried, MagicCatalog.Instance.GetMagic);
            }

            // Top the healing-potion belt back up to its cap for the new dungeon. Consumables
            // now live in the item inventory; the "belt" is just the carry cap the Merchant raises.
            if (_healingPotion != null && InventoryManager.HasInstance && PartyResourceManager.Instance != null)
            {
                int cap = PartyResourceManager.Instance.GetMax(PartyResourceType.HealingPotion);
                InventoryManager.Instance.TopUpConsumableToCap(_healingPotion, cap);
            }

            // Initialize save manager and persist initial state
            var levelKey = LevelKeyForSave();
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
            foreach (var roomData in saveData.Rooms)
            {
                if (roomData.RoomIndex < 0 || roomData.RoomIndex >= rooms.Count)
                {
                    continue;
                }

                var room = rooms[roomData.RoomIndex];

                // Re-apply the room's event state *before* trimming enemies: a consumed event may
                // have woken something, and those spawns have to exist again before the saved
                // enemy count decides how many of them the player has since killed.
                RestoreRoomEvent(room, roomData);
                RestoreRoomKind(room, roomData);

                // Remove killed enemies based on saved counts.
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

            // Spawn party in the saved current room. Clamped as well as gated: IsSaveCompatible is the
            // real check, and this is here so a future caller that forgets it lands the party in a real
            // room instead of throwing.
            var currentRoom = rooms[Mathf.Clamp(saveData.CurrentRoomIndex, 0, rooms.Count - 1)];
            var partyObj = Instantiate(_partyPrefab, transform);
            Party = partyObj.GetComponent<Party>();
            Party.Initialize(FieldedHeroes());

            // Health carries across the resume. Party.Initialize derives every hero full, which is
            // right for a fresh level and wrong here: health only refills on entering a *new*
            // dungeon, so restoring it full made quitting to the menu a heal, and undid the damage
            // every room event had already charged for.
            foreach (var hero in Party.Heroes)
            {
                if (hero == null || hero.Stats == null)
                {
                    continue;
                }
                hero.Stats.Health = PartyHealthSnapshot.HealthFor(
                    saveData.HeroHealth, hero.HeroKey, hero.GetEffectiveMaxHealth());
            }

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
            MagicState.Initialize(Party.Heroes);
            if (MagicCatalog.HasInstance)
            {
                MagicState.Restore(saveData.EquippedMagic, MagicCatalog.Instance.GetMagic);
            }

            Afflictions.Restore(saveData.Afflictions);

            // Re-spend the consumables this level had already used. Consumables live in the item
            // collection, which is deferred until level clear, so a resume otherwise came back with
            // a full potion belt - the same free-heal this hero-health restore closes, in the other
            // half of the sustain pool. The reconcile is idempotent, so it is correct whether or not
            // InventoryManager survived the scene change with the potions already gone.
            if (InventoryManager.HasInstance)
            {
                InventoryManager.Instance.ReconcileDungeonConsumption(saveData.ConsumablesSpent);
            }

            DungeonSaveManager.Instance.Initialize(saveData.Seed, LevelKeyForSave(), rooms);
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
                .Where(r => r != null && r != startRoom && !r.IsExit && !r.RoomSO.IsConnectorRoom
                            && r.Kind.AcceptsOtherSpecials())
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
        /// Puts a room's event back the way the save left it. Two jobs: mark a resolved event
        /// consumed so it cannot be re-rolled, and re-spawn whatever its outcome woke up so quitting
        /// to the menu is not an escape from a fight the player started.
        ///
        /// <para>Guarded on the event key. Placement is seed-deterministic, so the same event should
        /// land in the same room - but if the pools have been re-authored since the save, a stale
        /// consumed flag would silently eat a different event, so a mismatch is left alone.</para>
        /// </summary>
        private void RestoreRoomEvent(Room room, RoomSaveData roomData)
        {
            if (!roomData.EventConsumed || room.RoomEvent == null)
            {
                return;
            }

            if (!string.IsNullOrEmpty(roomData.EventKey) && roomData.EventKey != room.RoomEvent.SaveKey)
            {
                Debug.LogWarning($"Room {roomData.RoomIndex} saved event '{roomData.EventKey}' but "
                    + $"regenerated '{room.RoomEvent.SaveKey}'; leaving it unconsumed.");
                return;
            }

            room.MarkEventResolved(roomData.EventOptionIndex, roomData.EventOutcomeIndex, roomData.EventSucceeded);

            var outcome = ResolvedOutcome(room.RoomEvent, roomData);
            if (outcome == null || outcome.AwakenedEnemies == null)
            {
                return;
            }

            foreach (var definition in outcome.AwakenedEnemies)
            {
                if (definition != null)
                {
                    EnemyManager.Instance.SpawnSingle(definition, room);
                }
            }
        }

        /// <summary>
        /// Re-applies a taken payload. The kind itself is regenerated from the seed, so the saved kind
        /// is only a guard: if the quotas or the RNG stream have shifted since, a consumed flag would
        /// otherwise empty a cache the player never opened.
        /// </summary>
        private void RestoreRoomKind(Room room, RoomSaveData roomData)
        {
            if (!roomData.KindConsumed || !room.Kind.HasPayload())
            {
                return;
            }

            if (roomData.Kind != (int)room.Kind)
            {
                Debug.LogWarning($"Room {roomData.RoomIndex} saved kind '{(RoomKind)roomData.Kind}' but "
                    + $"regenerated '{room.Kind}'; leaving its payload unspent.");
                return;
            }

            room.MarkPayloadTaken();
        }

        /// <summary>The outcome the save says resolved, or null when the indices no longer fit.</summary>
        private Rooms.Events.RoomEventOutcome ResolvedOutcome(Rooms.Events.RoomEventSO roomEvent, RoomSaveData roomData)
        {
            if (roomData.EventOptionIndex < 0 || roomData.EventOptionIndex >= roomEvent.Options.Count)
            {
                return null;
            }

            var option = roomEvent.Options[roomData.EventOptionIndex];
            var pool = roomData.EventSucceeded ? option.Success : option.Failure;

            if (pool == null || roomData.EventOutcomeIndex < 0 || roomData.EventOutcomeIndex >= pool.Count)
            {
                return null;
            }

            return pool[roomData.EventOutcomeIndex];
        }

        /// <summary>
        /// Hands out this level's room events. Each room template lists the events it <i>can</i>
        /// offer; the level says how many rooms actually get one, so scarcity is a level-design knob
        /// and a template used three times does not repeat its event three times.
        ///
        /// <para>Skips the start room, the exit room, connectors and any room already holding a
        /// captive - the specials stay spread out. No event is placed twice in one level, so a level
        /// is a set of distinct decisions rather than the same gamble repeated.</para>
        ///
        /// <para>Placement is random but seed-deterministic, exactly like captive placement, which is
        /// what lets the dungeon save record only <i>that</i> an event was consumed and trust
        /// regeneration to put the same event back in the same room.</para>
        /// </summary>
        /// <summary>
        /// Promotes some of the level's ordinary rooms into non-combat kinds - a treasure cache, a
        /// refuge - per the level's quotas. Runs <b>before</b> anything is placed in a room, because
        /// every later pass reads the kind: enemies skip a promoted room, and captives and events
        /// leave it alone so the room offers exactly one thing.
        ///
        /// <para>Drawn from the same seeded RNG stream as the rest of generation, so a resumed level
        /// reproduces its own caches and refuges.</para>
        /// </summary>
        private void PlaceRoomKinds(List<Room> rooms, Room startRoom)
        {
            if (rooms == null)
            {
                return;
            }

            // Every room starts as whatever its template says it is; promotion only moves rooms the
            // template left as ordinary Combat.
            foreach (var room in rooms)
            {
                if (room != null && room.RoomSO != null)
                {
                    room.Kind = room.RoomSO.Kind;
                }
            }

            if (_level == null || (_level.TreasureRooms <= 0 && _level.RestRooms <= 0))
            {
                return;
            }

            var eligible = new List<int>();
            for (int i = 0; i < rooms.Count; i++)
            {
                if (IsKindEligible(rooms[i], startRoom))
                {
                    eligible.Add(i);
                }
            }

            var plan = RoomKindPlanner.Plan(
                eligible, _level.TreasureRooms, _level.RestRooms, count => Random.Range(0, count));

            foreach (var entry in plan)
            {
                var room = rooms[entry.Key];
                room.Kind = entry.Value;
                PlaceKindMarker(room);
            }
        }

        /// <summary>
        /// Whether an ordinary room can be promoted. The start room is out (a reward on turn one is
        /// not a find, and a refuge there is wasted at full health), the exit room is out because it
        /// holds the stairs and possibly the boss, and connectors are out because a hallway with a
        /// treasure chest in it is not a hallway.
        /// </summary>
        private bool IsKindEligible(Room room, Room startRoom)
        {
            return room != null
                   && room != startRoom
                   && !room.IsExit
                   && room.RoomSO != null
                   && room.RoomSO.Kind == RoomKind.Combat;
        }

        /// <summary>
        /// Marks a payload room on the map. Reuses the exit marker sprite under a tint rather than
        /// waiting on art: a room the player cannot see is a reward they walk past.
        /// </summary>
        private void PlaceKindMarker(Room room)
        {
            if (_exitRoomMarkerSprite == null || !room.Kind.HasPayload())
            {
                return;
            }

            var markerObj = new GameObject(room.Kind + "Marker");
            markerObj.transform.SetParent(room.transform, false);
            var center = room.GetCenter();
            center.z = -0.5f;
            markerObj.transform.position = center;
            var sr = markerObj.AddComponent<SpriteRenderer>();
            sr.sprite = _exitRoomMarkerSprite;
            sr.sortingOrder = 3;
            sr.color = room.Kind == RoomKind.Treasure
                ? new Color(1f, 0.85f, 0.25f)
                : new Color(0.4f, 0.9f, 0.75f);
            room.KindMarker = sr;
        }

        private void PlaceRoomEvents(List<Room> rooms, Room startRoom)
        {
            if (rooms == null)
            {
                return;
            }

            // Read once, not per roll: this loads the party save.
            var partyStats = BestRosterStats();

            foreach (var room in rooms)
            {
                if (!IsEventEligible(room, startRoom))
                {
                    continue;
                }

                // Each candidate gets its own roll, in authored order, and the first to pass takes
                // the room - a room only ever offers one thing. Listing two events therefore raises
                // the odds of the room having *something*, which is the intended lever: a common
                // find and a once-a-run find can share a room pool.
                foreach (var candidate in room.RoomSO.PossibleEvents)
                {
                    if (candidate == null)
                    {
                        continue;
                    }

                    // The gate first: no point rolling for a tome nobody can read.
                    if (!Rooms.Events.RoomEventSpawn.MeetsRequirements(candidate.SpawnRequirements, partyStats))
                    {
                        continue;
                    }

                    float chance = Rooms.Events.RoomEventSpawn.ChancePercent(
                        candidate.SpawnChancePercent,
                        partyStats[candidate.SpawnModifierStat],
                        candidate.SpawnModifierRate);

                    if (Rooms.Events.RoomEventSpawn.Spawns(chance, Random.Range(0f, 100f)))
                    {
                        room.RoomEvent = candidate;
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Whether a room can hold an event at all. The start room is skipped (an event on turn one is
        /// not a discovery), as are connectors and any room already holding a captive - the specials
        /// stay spread out. The exit room <i>is</i> eligible: taking the stairs is a button, so the
        /// player gets their turn in the room before the level ends.
        /// </summary>
        private bool IsEventEligible(Room room, Room startRoom)
        {
            return room != null
                   && room != startRoom
                   && room.CaptiveHero == null
                   && room.RoomSO != null
                   && !room.RoomSO.IsConnectorRoom
                   && room.Kind.AcceptsOtherSpecials()
                   && room.RoomSO.PossibleEvents != null
                   && room.RoomSO.PossibleEvents.Count > 0;
        }

        /// <summary>
        /// Party-best effective value per stat, for the party as it enters this level: authored base
        /// stats, plus every sphere-grid node its saved activations have bought, plus equipped gear.
        /// The *fielded* party, not the owned roster - a benched hero's Intelligence cannot open a
        /// tome they are not there to read.
        ///
        /// <para>Built through <see cref="HeroStatCalculator"/> rather than <c>Hero</c>, because
        /// placement runs before the party is instantiated - and has to, since a resumed dungeon
        /// re-applies its saved event state before the party exists. <c>Hero.GetEffectiveStat</c>
        /// needs a live scene; the calculator re-derives the same numbers from the asset, the saved
        /// node keys and the gear loadout.</para>
        ///
        /// <para>Computed once per placement pass, not once per roll: it reads the party save off
        /// disk. Stable across a save and resume, which placement relies on - node activation is
        /// hub-only and gear is committed only on level clear, so mid-dungeon progress cannot move
        /// a spawn threshold.</para>
        /// </summary>
        private StatBlock BestRosterStats()
        {
            var best = new StatBlock();
            var partySave = _fileHandler.Load<PartySaveData>();

            foreach (var heroSO in FieldedHeroes())
            {
                if (heroSO == null)
                {
                    continue;
                }

                var baseStats = HeroStatCalculator.BaseStatsForNodes(heroSO, SavedNodesFor(partySave, heroSO.SaveKey));
                var gear = InventoryManager.HasInstance
                    ? InventoryManager.Instance.GetEquippedItems(heroSO.SaveKey)
                    : null;
                var effective = HeroStatCalculator.WithGear(baseStats, gear);

                foreach (var stat in StatCatalog.Types)
                {
                    if (effective[stat] > best[stat])
                    {
                        best[stat] = effective[stat];
                    }
                }
            }

            return best;
        }

        private static List<string> SavedNodesFor(PartySaveData partySave, string heroKey)
        {
            if (partySave == null || partySave.Heroes == null || string.IsNullOrEmpty(heroKey))
            {
                return new List<string>();
            }

            var entry = partySave.Heroes.Find(h => h != null && h.HeroKey == heroKey);
            return entry != null && entry.ActivatedNodes != null ? entry.ActivatedNodes : new List<string>();
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
                MagicState.AddHero(hero);
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

            CommitMagicLoadout();

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
                    // Run complete — record it permanently (gates non-repeatable runs like the
                    // tutorial out of the New Run button), then clear the run save.
                    MetaProgressManager.Instance.MarkRunCompleted(runSave.RunKey);
                    _fileHandler.Delete(runSave);
                    ActiveRun = null;
                    MainMenuManager.MarkRunCompleted();
                }
            }

            SceneManager.LoadScene("MenuScene");
        }

        /// <summary>
        /// Banks what the party is carrying in its draw slots, so it is there at the start of the
        /// next run. Deferred to level clear like every other in-run gain - XP, loot, banked gold, a
        /// rescued hero - which is what makes magic drawn during a fatal run forfeit: nothing on the
        /// death path writes this file, so the last committed loadout is what survives.
        ///
        /// <para>Merged rather than overwritten, because <c>GetSaveData</c> only knows about the
        /// heroes this run fielded and a benched hero must not lose their kit.</para>
        /// </summary>
        private void CommitMagicLoadout()
        {
            if (MagicState == null)
            {
                return;
            }

            var loadout = _fileHandler.Load<MagicLoadoutSaveData>();
            loadout.Heroes = EquippedMagicState.Merge(loadout.Heroes, MagicState.GetSaveData());
            _fileHandler.Save(loadout);
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
