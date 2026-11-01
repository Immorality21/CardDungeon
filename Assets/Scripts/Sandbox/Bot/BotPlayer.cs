using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Assets.Scripts.Combat;
using Assets.Scripts.Dungeon;
using Assets.Scripts.Heroes;
using Assets.Scripts.Hub;
using Assets.Scripts.Items;
using Assets.Scripts.Rooms;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Assets.Scripts.Sandbox.Bot
{
    /// <summary>
    /// Plays the real game with no one at the keyboard: new save, floors, fights, events, the town,
    /// the next run - across scene loads, for every profile of a batch. It calls what the buttons call
    /// (the <c>Bot</c> region of <see cref="RoomActionUI"/>), so it tests combat, events, saves and the
    /// hub, and not the keyboard. See <c>docs/BOT.md</c>.
    /// </summary>
    public class BotPlayer : MonoBehaviour
    {
        private const string HubScene = "HubScene";
        private const string GameScene = "MainGameScene";

        /// <summary>Party health at or under which a refuge is worth using. One number for resting
        /// and for walking back to rest, or the bot paces between a campfire it will not use and
        /// the stairs.</summary>
        private const float RestBelow = 0.75f;

        private BotSettings _settings;
        private BotReport _report;
        private BotProfile _profile;
        private BotGame _game;
        private BotRun _run;
        private BotFloor _floor;
        private BotModelRun _modelRun;
        private int _potionReserve = -1;
        private readonly List<string> _passedOver = new List<string>();
        private BotFight _fight;
        private float _gameStart;

        private RoomActionUI _ui;
        private CombatManager _subscribed;
        private ICombatUnit _pendingTurn;
        private bool _inHub;
        private bool _gameOver;
        private float _lastActivity;
        private int _roomsSeen;
        private readonly Dictionary<string, int> _attempts = new Dictionary<string, int>();
        private readonly HashSet<Room> _eventsDone = new HashSet<Room>();
        private readonly HashSet<Room> _sideRoomsDone = new HashSet<Room>();
        private readonly HashSet<string> _triedWalks = new HashSet<string>();

        public static void Begin(BotSettings settings)
        {
            var host = new GameObject("BotPlayer");
            DontDestroyOnLoad(host);
            var bot = host.AddComponent<BotPlayer>();
            bot._settings = settings;
            bot.StartCoroutine(bot.PlayBatch());
        }

        private void Update()
        {
            Time.timeScale = _settings != null ? _settings.TimeScale : 1f;
        }

        private IEnumerator PlayBatch()
        {
            // The editor stops ticking a game view without focus otherwise.
            Application.runInBackground = true;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
            AudioListener.volume = 0f;

            _report = new BotReport();
            foreach (var profile in _settings.Profiles)
            {
                for (int i = 0; i < _settings.Games; i++)
                {
                    _profile = profile;
                    if (_settings.ModelRuns.Count == 0)
                    {
                        yield return PlayGame(_settings.FirstSeed + i);
                        continue;
                    }
                    foreach (var model in _settings.ModelRuns)
                    {
                        _modelRun = model;
                        yield return PlayGame(_settings.FirstSeed + i);
                    }
                }
            }

            _report.Status = "done";
            _report.Current = "";
            _report.Write(_settings.ReportName);
            Debug.Log($"[Bot] Finished {_report.Games.Count} games: {BotReport.PathFor(_settings.ReportName)}");
            BotSession.Finish();
        }

        private IEnumerator PlayGame(int seed)
        {
            _game = new BotGame { Profile = _profile.Label, Seed = seed };
            _report.Games.Add(_game);
            _report.Current = $"{_profile.Label} seed {seed}";
            _gameStart = Time.realtimeSinceStartup;
            _gameOver = false;
            _run = null;
            _attempts.Clear();
            _passedOver.Clear();

            // Every manager lives in a scene: leave them all behind before the save goes.
            SceneManager.LoadScene("MenuScene");
            yield return null;
            yield return null;
            BotSession.ClearSave();
            Random.InitState(seed);
            DungeonManager.ActiveRun = null;
            DungeonManager.RunLevelIndex = 0;
            DungeonManager.SeedToLoad = null;
            // A model party skips the tutorial: it walks in on a save the model wrote.
            if (_modelRun == null)
            {
                HubManager.RequestNewGame();
            }
            SceneManager.LoadScene(HubScene);
            _lastActivity = Time.realtimeSinceStartup;
            yield return null;

            while (!_gameOver)
            {
                string scene = SceneManager.GetActiveScene().name;
                if (scene == GameScene)
                {
                    DungeonTick();
                }
                else if (scene == HubScene && !_inHub)
                {
                    yield return VisitTown();
                }
                if (Time.realtimeSinceStartup - _lastActivity > _settings.StuckSeconds)
                {
                    GiveUp(StuckReason());
                }
                yield return null;
            }

            _game.RealSeconds = Time.realtimeSinceStartup - _gameStart;
            _report.Write(_settings.ReportName);
        }

        // ------------------------------------------------------------------
        //  Town
        // ------------------------------------------------------------------

        private IEnumerator VisitTown()
        {
            _inHub = true;
            // A new game walks straight through the hub into the first run.
            for (int i = 0; i < 4; i++)
            {
                yield return null;
            }
            var manager = Object.FindFirstObjectByType<HubManager>();
            if (SceneManager.GetActiveScene().name != HubScene || manager == null)
            {
                _inHub = false;
                yield break;
            }

            CloseFloor();
            if (_run != null && Progression.MetaProgressManager.Instance.GetCompletedRunKeys().Contains(_run.Key))
            {
                _run.Outcome = "cleared";
            }
            if (_modelRun != null)
            {
                FieldModelRun();
                yield break;
            }

            var visit = new BotHubVisit { Before = _run != null ? _run.Run + " " + _run.Outcome : "start" };
            _game.Hub.Add(visit);
            BotHub.Visit(manager.Roster, _profile, visit);
            _run = null;
            _report.Write(_settings.ReportName);

            var next = NextRun(manager.Roster);
            if (next == null)
            {
                _game.Finished = _passedOver.Count > 0
                    ? "gave up on " + string.Join(", ", _passedOver)
                    : "every listed run done";
                _gameOver = true;
                _inHub = false;
                yield break;
            }

            BotHub.BeginRun(next);
            SceneManager.LoadScene(GameScene);
            _lastActivity = Time.realtimeSinceStartup;
            _inHub = false;
            yield return null;
        }

        /// <summary>A model party plays its one run once: town before it, then the game is over.</summary>
        private void FieldModelRun()
        {
            if (_run != null)
            {
                _run = null;
                _game.Finished = "model run played";
                _gameOver = true;
                _inHub = false;
                _report.Write(_settings.ReportName);
                return;
            }
            BotHub.FieldModelParty(_modelRun);
            BotHub.BeginRun(BotHub.RunNamed(_modelRun.Run));
            SceneManager.LoadScene(GameScene);
            _lastActivity = Time.realtimeSinceStartup;
            _inHub = false;
        }

        /// <summary>The next run with tries left. A run without any ends the playthrough, or with
        /// <see cref="BotSettings.PassOverWalls"/> is left behind; both are named when it finishes.</summary>
        private RunDefinitionSO NextRun(PartyRosterSO roster)
        {
            while (true)
            {
                var next = BotHub.NextRun(_settings, roster, _passedOver);
                _attempts.TryGetValue(next != null ? next.name : string.Empty, out int tried);
                if (next == null || tried < _settings.MaxAttemptsPerRun)
                {
                    return next;
                }
                _passedOver.Add(next.name);
                if (!_settings.PassOverWalls)
                {
                    return null;
                }
            }
        }

        // ------------------------------------------------------------------
        //  Floor
        // ------------------------------------------------------------------

        private void DungeonTick()
        {
            if (_ui == null)
            {
                _ui = Object.FindFirstObjectByType<RoomActionUI>();
            }
            if (_ui == null || !DungeonManager.HasInstance || !CombatManager.HasInstance
                || DungeonManager.Instance.CurrentRooms == null || _ui.CurrentRoom == null)
            {
                return;
            }

            OpenFloor();
            Subscribe();

            // Walking is not progress, or a bot pacing between two rooms never counts as stuck.
            int seen = DungeonManager.Instance.CurrentRooms.Count(r => r != null && r.IsExplored);
            if (seen != _roomsSeen)
            {
                _roomsSeen = seen;
                Touch();
            }

            if (_ui.IsVictoryUp)
            {
                Touch();
                _ui.PressVictoryContinue();
            }
            else if (_ui.IsLevelClearUp)
            {
                Touch();
                var ui = _ui;
                CloseFloor();
                ui.PressLevelClear();
            }
            else if (_ui.IsEventUp)
            {
                Touch();
                AnswerEvent();
            }
            else if (_ui.IsDetailUp)
            {
                Touch();
                _ui.PressDetailOk();
            }
            else if (CombatManager.Instance.InCombat)
            {
                TakeTurn();
            }
            else if (_ui.IsFightBarUp)
            {
                Touch();
                _fight = NewFight();
                _ui.PressFight();
            }
            else if (_ui.CanWalk && !_ui.IsWalking)
            {
                Walk();
            }
        }

        private void OpenFloor()
        {
            if (_run == null)
            {
                var run = DungeonManager.ActiveRun;
                string name = run != null ? run.name : "?";
                _attempts.TryGetValue(name, out int tried);
                _attempts[name] = tried + 1;
                _run = new BotRun
                {
                    Run = name,
                    Key = run != null ? CampaignOps.RunKeyOf(run) : "",
                    Attempt = tried + 1,
                    Party = string.Join(" ", Party().Heroes.Where(h => h != null).Select(h => h.HeroKey)),
                };
                _game.Runs.Add(_run);
                _report.Current = $"{_profile.Label} seed {_game.Seed}: {name}, attempt {tried + 1}";
                _report.Write(_settings.ReportName);
            }
            if (_floor == null)
            {
                var entry = DungeonManager.ActiveRun?.Levels[DungeonManager.RunLevelIndex];
                _floor = new BotFloor { Level = entry?.LevelName ?? "?" };
                _potionReserve = -1;
                _run.Floors.Add(_floor);
                _eventsDone.Clear();
                _sideRoomsDone.Clear();
                _triedWalks.Clear();
                Touch();
            }
        }

        private void CloseFloor()
        {
            if (_floor == null)
            {
                return;
            }
            var rooms = DungeonManager.HasInstance ? DungeonManager.Instance.CurrentRooms : null;
            if (rooms != null)
            {
                _floor.RoomsTotal = rooms.Count;
                _floor.RoomsSeen = rooms.Count(r => r != null && r.IsExplored);
                _floor.HealthAtEnd = PartyHealth();
            }
            _floor = null;
            _ui = null;
        }

        private void Touch()
        {
            _lastActivity = Time.realtimeSinceStartup;
        }

        private static Party Party()
        {
            return GameManager.Instance.Party;
        }

        private static float PartyHealth()
        {
            var heroes = GameManager.HasInstance && GameManager.Instance.Party != null
                ? GameManager.Instance.Party.Heroes.Where(h => h != null).ToList()
                : new List<Hero>();
            int max = heroes.Sum(h => h.GetEffectiveMaxHealth());
            return max > 0 ? (float)heroes.Sum(h => Mathf.Max(0, h.Stats.Health)) / max : 0f;
        }

        // ------------------------------------------------------------------
        //  Walking
        // ------------------------------------------------------------------

        /// <summary>What is in this room first, then the next room, then the stairs.</summary>
        private void Walk()
        {
            var room = _ui.CurrentRoom;
            if (room.Enemies.Any(e => e != null && e.IsAlive))
            {
                return;
            }
            if (TryDrink())
            {
                return;
            }

            if (room.HasPendingEvent && room.EventMarker != null && !_eventsDone.Contains(room)
                && TryUse(room, room.EventMarker.transform.position))
            {
                return;
            }
            if (EnterOpenedSideRoom(room))
            {
                return;
            }
            if (room.HasPendingPayload && room.KindMarker != null
                && (room.Kind == RoomKind.Treasure || PartyHealth() <= RestBelow)
                && TryUse(room, room.KindMarker.transform.position))
            {
                return;
            }
            if (room.IsSideRoom)
            {
                if (room.ReturnMarker != null)
                {
                    TryUse(room, room.ReturnMarker.transform.position);
                }
                return;
            }

            var rooms = DungeonManager.Instance.CurrentRooms;
            bool pressOn = _profile.Route == BotRoute.ClearEverything || !room.IsExit;
            if (pressOn && TryNextRoom(room, rooms))
            {
                return;
            }
            if (TryRefuge(room, rooms))
            {
                return;
            }

            var exit = rooms.FirstOrDefault(r => r != null && r.IsExit);
            if (exit == null)
            {
                return;
            }
            if (room != exit)
            {
                _ui.TryWalkTo(exit.RoomIndex);
                return;
            }
            if (room.ExitMarker != null)
            {
                TryUse(room, room.ExitMarker.position);
            }
        }

        /// <summary>The floor's reserve, worked out once the belt is stocked: the model's rule.</summary>
        private int PotionReserve()
        {
            if (_potionReserve < 0)
            {
                var potion = BotCombat.FindPotion();
                int left = potion != null ? InventoryManager.Instance.GetConsumableQuantity(potion.Key) : 0;
                int floors = DungeonManager.ActiveRun != null ? DungeonManager.ActiveRun.Levels.Count : 1;
                _potionReserve = PotionUse.ReserveFor(left, floors - DungeonManager.RunLevelIndex);
            }
            return _potionReserve;
        }

        /// <summary>One potion for the most wounded, while it costs no turn: the model's rule between rooms.</summary>
        private bool TryDrink()
        {
            var wounded = Party().Heroes.Where(h => h != null && h.IsAlive)
                .OrderBy(BotCombat.HealthShare).FirstOrDefault();
            var potion = BotCombat.FindPotion();
            if (wounded == null || potion == null || BotCombat.HealthShare(wounded) > BotCombat.HealThreshold
                || !PotionUse.MayDrink(InventoryManager.Instance.GetConsumableQuantity(potion.Key),
                    PotionReserve(), BotCombat.HealthShare(wounded)))
            {
                return false;
            }
            _ui.DrinkPotion(potion, wounded);
            _floor.PotionsBetweenRooms++;
            Touch();
            return true;
        }

        /// <summary>The nearest room not yet entered; the exit last, when the route clears the floor.</summary>
        private bool TryNextRoom(Room room, IReadOnlyList<Room> rooms)
        {
            var from = (Vector2)room.GetCenter();
            var unseen = rooms.Where(r => r != null && !r.IsExplored && r != room)
                .OrderBy(r => Vector2.Distance(from, r.GetCenter()));
            foreach (var next in unseen)
            {
                // The party cannot tell which dark room holds the stairs; a clearing player only saves
                // the boss for last because the map shows the rest are done, so the exit waits here.
                if (next.IsExit && _profile.Route == BotRoute.ClearEverything && rooms.Any(r => r != null && !r.IsExplored && !r.IsExit))
                {
                    continue;
                }
                if (_ui.TryWalkTo(next.RoomIndex))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>A refuge passed at good health, gone back to before the stairs if the party is hurt.</summary>
        private bool TryRefuge(Room room, IReadOnlyList<Room> rooms)
        {
            if (_profile.Route != BotRoute.ClearEverything || PartyHealth() > RestBelow)
            {
                return false;
            }
            var refuge = rooms.FirstOrDefault(r => r != null && r != room && r.IsExplored
                && r.Kind == RoomKind.Rest && r.HasPendingPayload);
            return refuge != null && _ui.TryWalkTo(refuge.RoomIndex);
        }

        /// <summary>The way into a side room an event opened and left open.</summary>
        private bool EnterOpenedSideRoom(Room room)
        {
            if (room.IsSideRoom || room.EventMarker == null || !room.EventConsumed
                || room.SideRoomCleared || !_sideRoomsDone.Add(room))
            {
                return false;
            }
            var side = DungeonManager.Instance.ResolvedOutcomeOf(room)?.SideRoom;
            return side != null && side.IsSet && !side.Mandatory && TryUse(room, room.EventMarker.transform.position);
        }

        /// <summary>Walks to something and uses it. A use that never lands is not tried for ever.</summary>
        private bool TryUse(Room room, Vector3 position)
        {
            var tile = Vector2Int.RoundToInt(position);
            string key = room.RoomIndex + ":" + tile + ":" + room.HasPendingPayload + room.EventConsumed;
            if (!_triedWalks.Add(key))
            {
                return false;
            }
            Touch();
            _ui.WalkToTile(tile);
            return true;
        }

        // ------------------------------------------------------------------
        //  Events and fights
        // ------------------------------------------------------------------

        private void AnswerEvent()
        {
            var room = _ui.CurrentRoom;
            var roomEvent = _ui.CurrentEvent;
            float chance = _ui.CurrentEventChance;
            int choice = BotEventPolicy.Choose(roomEvent.Options, chance, _profile.Events, _ui.CanTakeEventOption);
            var option = choice >= 0 ? roomEvent.Options[choice] : null;
            bool declined = option == null || option.Kind == Rooms.Events.RoomEventOptionKind.Decline;
            _floor?.Events.Add(new BotEvent
            {
                Event = roomEvent.name,
                Option = option != null ? option.Kind.ToString() : "Back",
                Chance = chance,
                Declined = declined,
            });
            _eventsDone.Add(room);
            _ui.PressEventOption(choice);
        }

        private void Subscribe()
        {
            var combat = CombatManager.Instance;
            if (_subscribed == combat)
            {
                return;
            }
            _subscribed = combat;
            _pendingTurn = null;
            combat.OnHeroTurnStarted += unit => _pendingTurn = unit;
            combat.OnCombatEnded += OnCombatEnded;
        }

        private void TakeTurn()
        {
            if (_pendingTurn == null)
            {
                // Enemy turns and animations: a fight that is still running is not stuck on the bot.
                return;
            }
            Touch();
            var unit = _pendingTurn;
            _pendingTurn = null;
            _ui.HideCommands();
            BotCombat.Act(unit, Party(), _fight ?? (_fight = NewFight()), PotionReserve());
        }

        private BotFight NewFight()
        {
            var room = _ui.CurrentRoom;
            var enemies = room.Enemies.Where(e => e != null && e.IsAlive).ToList();
            return new BotFight
            {
                Room = room.name,
                Boss = enemies.Any(e => e.IsBoss),
                Enemies = string.Join(", ", enemies.Select(e => e.DisplayName)),
                Heroes = Party().Heroes.Count(h => h != null && h.IsAlive),
                HealthBefore = PartyHealth(),
            };
        }

        private void OnCombatEnded(CombatResult result)
        {
            Touch();
            if (_fight == null)
            {
                return;
            }
            _fight.Outcome = result.Outcome.ToString();
            _fight.HealthAfter = PartyHealth();
            _fight.HeroesDown = Party().Heroes.Count(h => h != null && !h.IsAlive);
            _floor?.Fights.Add(_fight);
            if (result.Outcome == CombatOutcome.PlayerDied && _run != null)
            {
                _run.Outcome = "wiped on " + (_floor != null ? _floor.Level : "?");
            }
            _fight = null;
        }

        // ------------------------------------------------------------------
        //  Giving up
        // ------------------------------------------------------------------

        private string StuckReason()
        {
            string scene = SceneManager.GetActiveScene().name;
            if (_ui == null)
            {
                return $"nothing to do in {scene}";
            }
            var room = _ui.CurrentRoom;
            return $"{scene}: {(_floor != null ? _floor.Level : "?")}, room {(room != null ? room.RoomIndex : -2)}, "
                   + $"side {(room != null && room.IsSideRoom)}, event {_ui.IsEventUp}, detail {_ui.IsDetailUp}, "
                   + $"fight {_ui.IsFightBarUp}, "
                   + $"combat {CombatManager.Instance.InCombat}, canWalk {_ui.CanWalk}, walking {_ui.IsWalking}, "
                   + $"rooms {DungeonManager.Instance.CurrentRooms.Count(r => r != null && r.IsExplored)}"
                   + $"/{DungeonManager.Instance.CurrentRooms.Count}";
        }

        private void GiveUp(string reason)
        {
            Debug.LogWarning($"[Bot] Stuck, moving on: {reason}");
            _game.Stuck = reason;
            CloseFloor();
            _gameOver = true;
        }
    }
}
